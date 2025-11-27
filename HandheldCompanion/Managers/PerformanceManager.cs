using HandheldCompanion.Devices;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static HandheldCompanion.Processors.Intel.KX;
using static HandheldCompanion.Processors.IntelProcessor;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class OSPowerMode
{
    /// <summary>
    ///     Better Battery mode.
    /// </summary>
    public static Guid BetterBattery = IDevice.BetterBatteryGuid;

    /// <summary>
    ///     Better Performance mode.
    /// </summary>
    public static Guid BetterPerformance = Guid.Empty;

    /// <summary>
    ///     Best Performance mode.
    /// </summary>
    public static Guid BestPerformance = IDevice.BestPerformanceGuid;
}

public enum CPUBoostLevel
{
    Disabled = 0,
    Enabled = 1,
    Agressive = 2,
    EfficientEnabled = 3,
    EfficientAgressive = 4,
}

public static class PerformanceManager
{
    private const short INTERVAL_DEFAULT = 3000; // default interval between value scans
    private const short INTERVAL_AUTO = 1010; // default interval between value scans for AutoTDP
    private const short INTERVAL_DEGRADED = 5000; // degraded interval between value scans

    private const int COUNTER_DEFAULT = 3; // default counter value
    private const int COUNTER_AUTO = 5; // default counter value for AutoTDP

    public static readonly Guid[] PowerModes = { OSPowerMode.BetterBattery, OSPowerMode.BetterPerformance, OSPowerMode.BestPerformance };

    private static readonly Timer autotdpWatchdog;
    private static readonly Timer tdpWatchdog;
    private static readonly Timer gfxWatchdog;
    private static readonly Timer cpuWatchdog;

    private static CrossThreadLock autotdpLock = new();
    private static CrossThreadLock tdpLock = new();
    private static CrossThreadLock gfxLock = new();
    private static CrossThreadLock cpuLock = new();

    private static PowerProfile? currentProfile = null;

    // used to determine relevant TDP and MSR values
    private static Processor? processor;

    // AutoTDP
    private static bool AutoTDPFirstRun = true;
    private static double AutoTDPTargetFPS;
    private static int AutoTDPFPSSetpointMetCounter;
    private static int AutoTDPFPSSmallDipCounter;
    private static readonly double[] FPSHistory = new double[6];
    private static double ProcessValueFPSPrevious;
    private static double AutoTDP;
    private static double AutoTDPPrev;
    private static double AutoTDPMax;
    private static bool autotdpWatchdogPendingStop;

    // powercfg
    private static Guid currentPowerMode = Guid.Empty;

    // GPU limits
    private static double FallbackGfxClock;
    private static double StoredGfxClock;
    private static bool gfxWatchdogPendingStop;
    private static int gfxWatchdogCounter;

    // TDP limits
    private static double TDPMin;
    private static double TDPMax;
    private static bool tdpWatchdogPendingStop;
    private static readonly double[] CurrentTDP = new double[5]; // used to store current TDP
    private static readonly double[] StoredTDP = new double[3]; // used to store TDP

    private const string dllName = "WinRing0x64.dll";

    private static bool IsInitialized;
    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler(bool CanChangeTDP, bool CanChangeGPU);

    static PerformanceManager()
    {
        // initialize timer(s)
        cpuWatchdog = new Timer { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
        cpuWatchdog.Elapsed += cpuWatchdog_Elapsed;

        tdpWatchdog = new Timer { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
        tdpWatchdog.Elapsed += tdpWatchdog_Elapsed;

        gfxWatchdog = new Timer { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
        gfxWatchdog.Elapsed += gfxWatchdog_Elapsed;

        autotdpWatchdog = new Timer { Interval = INTERVAL_AUTO, AutoReset = true, Enabled = false };
        autotdpWatchdog.Elapsed += autotdpWatchdog_Elapsed;
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        // initialize watchdog(s)
        cpuWatchdog.Start();

        // initialize processor
        processor = Processor.GetCurrent();

        // raise events
        switch (ManagerFactory.powerProfileManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPowerProfile();
                break;
        }

        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        IsInitialized = true;
        Initialized?.Invoke(processor?.CanChangeTDP ?? false, processor?.CanChangeGPU ?? false);

        LogManager.LogInformation("{0} has started", "PerformanceManager");
    }

    private static void QueryPowerProfile()
    {
        // manage events
        ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Discarded += PowerProfileManager_Discarded;

        PowerProfileManager_Applied(ManagerFactory.powerProfileManager.GetCurrent(), UpdateSource.Background);
    }

    private static void PowerProfileManager_Initialized()
    {
        QueryPowerProfile();
    }

    private static void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private static void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("ConfigurableTDPOverrideDown", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideDown"), false);
        SettingsManager_SettingValueChanged("ConfigurableTDPOverrideUp", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideUp"), false);
        // AMD
        SettingsManager_SettingValueChanged("RyzenAdjCoAll", ManagerFactory.settingsManager.GetString("RyzenAdjCoAll"), false);
        SettingsManager_SettingValueChanged("RyzenAdjCoGfx", ManagerFactory.settingsManager.GetString("RyzenAdjCoGfx"), false);
        // Intel
        SettingsManager_SettingValueChanged("MsrUndervoltCore", ManagerFactory.settingsManager.GetString("MsrUndervoltCore"), false);
        SettingsManager_SettingValueChanged("MsrUndervoltGpu", ManagerFactory.settingsManager.GetString("MsrUndervoltGpu"), false);
        SettingsManager_SettingValueChanged("MsrUndervoltSoc", ManagerFactory.settingsManager.GetString("MsrUndervoltSoc"), false);
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        // halt processor
        if (processor is not null && processor.IsInitialized)
            processor.Stop();

        // halt watchdogs
        autotdpWatchdog.Stop();
        tdpWatchdog.Stop();
        gfxWatchdog.Stop();
        cpuWatchdog.Stop();

        // dismount WinRing0x64.dll, and WinRing0x64.sys hopefully...
        nint Module = GetModuleHandle(dllName);
        if (Module != IntPtr.Zero)
            FreeLibrary(Module);

        // manage events
        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Discarded -= PowerProfileManager_Discarded;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "PerformanceManager");
    }

    public static double GetMinimumTDP()
    {
        return TDPMin;
    }

    public static double GetMaximumTDP()
    {
        return TDPMax;
    }

    private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "ConfigurableTDPOverrideDown":
                {
                    TDPMin = Convert.ToDouble(value);
                    if (AutoTDPMax != 0d && AutoTDPMax < TDPMin)
                        AutoTDPMax = TDPMin;
                }
                break;
            case "ConfigurableTDPOverrideUp":
                {
                    TDPMax = Convert.ToDouble(value);
                    if (AutoTDPMax == 0d || AutoTDPMax > TDPMax)
                        AutoTDPMax = TDPMax;
                }
                break;
            case "RyzenAdjCoAll":
                {
                    if (processor is AMDProcessor AMDProcessor)
                    {
                        int steps = Convert.ToInt32(value);
                        bool output = AMDProcessor.SetCoAll(steps);
                    }
                }
                break;
            case "RyzenAdjCoGfx":
                {
                    if (processor is AMDProcessor AMDProcessor)
                    {
                        int steps = Convert.ToInt32(value);
                        bool output = AMDProcessor.SetCoGfx(steps);
                    }
                }
                break;
            case "MsrUndervoltCore":
                {
                    if (processor is IntelProcessor IntelProcessor)
                    {
                        int offsetMv = Convert.ToInt32(value);
                        bool output = IntelProcessor.SetMSRUndervolt(IntelUndervoltRail.Core, offsetMv) && IntelProcessor.SetMSRUndervolt(IntelUndervoltRail.Cache, offsetMv);
                    }
                }
                break;
            case "MsrUndervoltGpu":
                {
                    if (processor is IntelProcessor IntelProcessor)
                    {
                        int offsetMv = Convert.ToInt32(value);
                        bool output = IntelProcessor.SetMSRUndervolt(IntelUndervoltRail.Gpu, offsetMv);
                    }
                }
                break;
            case "MsrUndervoltSoc":
                {
                    if (processor is IntelProcessor IntelProcessor)
                    {
                        int offsetMv = Convert.ToInt32(value);
                        bool output = IntelProcessor.SetMSRUndervolt(IntelUndervoltRail.SystemAgent, offsetMv);
                    }
                }
                break;
        }
    }

    private static void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        currentProfile = profile;

        // apply profile defined TDP
        if (profile.TDPOverrideEnabled)
        {
            if (!profile.AutoTDPEnabled)
            {
                // AutoTDP is off and manual TDP is set
                // stop AutoTDP watchdog and apply manual TDP
                StopAutoTDPWatchdog(true);
                RequestTDP(profile.TDPOverrideValues);

                if (!tdpWatchdog.Enabled)
                    StartTDPWatchdog();
            }
            else
            {
                // Both manual TDP and AutoTDP are on
                // use AutoTDP watchdog to adjust TDP
                StopTDPWatchdog(true);
                RestoreTDP(true);
            }

            // use manual slider as the starting value
            // and max limit for AutoTDP
            if (profile.TDPOverrideValues is not null)
                AutoTDP = AutoTDPMax = profile.TDPOverrideValues[0];
        }
        else
        {
            if (tdpWatchdog.Enabled)
                StopTDPWatchdog(true);

            if (!profile.AutoTDPEnabled)
            {
                if (autotdpWatchdog.Enabled)
                    StopAutoTDPWatchdog(true);

                // Neither manual TDP nor AutoTDP is enabled, restore default TDP
                RestoreTDP(true);
            }

            // manual TDP override is not set
            // use the settings max limit for AutoTDP
            AutoTDP = AutoTDPMax = ManagerFactory.settingsManager.GetInt("ConfigurableTDPOverrideUp");
        }

        // apply profile defined AutoTDP
        if (profile.AutoTDPEnabled)
        {
            AutoTDPTargetFPS = profile.AutoTDPRequestedFPS;

            if (!autotdpWatchdog.Enabled)
                StartAutoTDPWatchdog();
        }

        // apply profile defined CPU
        if (profile.CPUOverrideEnabled)
        {
            RequestCPUClock(Convert.ToUInt32(profile.CPUOverrideValue));
        }
        else
        {
            // restore default GPU clock
            RestoreCPUClock();
        }

        // apply profile defined GPU
        if (profile.GPUOverrideEnabled)
        {
            RequestGPUClock(profile.GPUOverrideValue);
            StartGPUWatchdog();
        }
        else
        {
            if (gfxWatchdog.Enabled)
                StopGPUWatchdog(true);

            // restore default GPU clock
            RestoreGPUClock(true);
        }

        // apply profile defined CPU Core Parking
        RequestCoreParkingMode(profile.CPUParkingMode);

        // apply profile defined CPU Core Count
        if (profile.CPUCoreEnabled)
        {
            RequestCPUCoreCount(profile.CPUCoreCount);
        }
        else
        {
            // restore default CPU Core Count
            RequestCPUCoreCount(MotherboardInfo.NumberOfCores);
        }

        // apply profile define CPU Boost
        RequestPerfBoostMode((uint)profile.CPUBoostLevel);

        // apply profile Power mode
        RequestPowerMode(profile.OSPowerMode);
    }

    private static void PowerProfileManager_Discarded(PowerProfile profile, bool swapped)
    {
        // don't bother discarding settings, new one will be enforce shortly
        if (swapped)
            return;

        currentProfile = null;

        // restore default TDP
        if (profile.TDPOverrideEnabled)
        {
            StopTDPWatchdog(true);
            RestoreTDP(true);
        }

        // restore default TDP
        if (profile.AutoTDPEnabled)
        {
            StopAutoTDPWatchdog(true);
            RestoreTDP(true);
        }

        // restore default CPU frequency
        if (profile.CPUOverrideEnabled)
        {
            RestoreCPUClock();
        }

        // restore default GPU frequency
        if (profile.GPUOverrideEnabled)
        {
            StopGPUWatchdog(true);
            RestoreGPUClock(true);
        }

        // restore default CPU Core Parking
        RequestCoreParkingMode(CoreParkingMode.AllCoresAuto);

        // unapply profile defined CPU Core Count
        if (profile.CPUCoreEnabled)
        {
            RequestCPUCoreCount(MotherboardInfo.NumberOfCores);
        }

        // restore profile define CPU Boost
        RequestPerfBoostMode((uint)PerfBoostMode.Disabled);

        // restore OSPowerMode.BetterPerformance 
        RequestPowerMode(OSPowerMode.BetterPerformance);
    }

    private static void RestoreTDP(bool immediate)
    {
        // On power status change, force refresh TDP and AutoTDP
        PowerProfile profile = ManagerFactory.powerProfileManager.GetDefault();
        RequestTDP(profile.TDPOverrideValues, immediate);

        if (profile.TDPOverrideValues is not null)
            AutoTDP = profile.TDPOverrideValues[0];
    }

    private static void RestoreCPUClock()
    {
        RequestCPUClock(0);
    }

    private static void RestoreGPUClock(bool immediate)
    {
        RequestGPUClock(255 * 50, immediate);
    }

    private static void autotdpWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        // we're not ready yet
        if (!ManagerFactory.platformManager.IsReady)
            return;

        bool hasHook = PlatformManager.RTSS?.HasHook() ?? false;
        if (!hasHook)
        {
            autotdpWatchdog.Interval = INTERVAL_DEGRADED;
            RestoreTDP(true);
            return;
        }
        else
            autotdpWatchdog.Interval = INTERVAL_AUTO;

        if (autotdpLock.TryEnter())
        {
            try
            {
                bool TDPdone = false;
                bool MSRdone = true;
                bool forcedUpdate = false;
                double damper = 0.0;
                double unclampedProcessValueFPS = 0.0;

                // todo: Store fps for data gathering from multiple points (OSD, Performance)
                double framerate = PlatformManager.RTSS?.GetFramerate(true) ?? 0.0d;
                double processValueFPS = unclampedProcessValueFPS = framerate;

                // Ensure realistic process values, prevent divide by 0
                processValueFPS = Math.Clamp(processValueFPS, 5, 500);

                // Determine error amount, include target, actual and dipper modifier
                double controllerError = AutoTDPTargetFPS - processValueFPS - AutoTDPDipper(processValueFPS, AutoTDPTargetFPS);

                // Clamp error amount corrected within a single cycle
                // Adjust clamp if actual FPS is 2.5x requested FPS
                double clampLowerLimit = processValueFPS >= 2.5 * AutoTDPTargetFPS ? -100 : -5;
                controllerError = Math.Clamp(controllerError, clampLowerLimit, 15);

                double TDPAdjustment = controllerError * AutoTDP / processValueFPS;
                TDPAdjustment *= 0.9; // Always have a little undershoot

                // Determine final setpoint
                if (!AutoTDPFirstRun)
                {
                    AutoTDP += TDPAdjustment + AutoTDPDamper(processValueFPS);
                    damper = AutoTDPDamper(processValueFPS);
                }
                else
                    AutoTDPFirstRun = false;

                AutoTDP = Math.Clamp(AutoTDP, TDPMin, AutoTDPMax);

                // Only update if we have a different TDP value to set
                if (AutoTDP != AutoTDPPrev)
                {
                    int TDPBump = 0;

                    if (GetProcessor() is IntelProcessor intelProcessor)
                    {
                        switch (intelProcessor.MicroArch)
                        {
                            // Official specification for Lunar Lake states that PL2 should always be at least 1 W higher than PL1
                            case IntelMicroArch.LunarLake:
                                TDPBump = 1;
                                break;
                        }
                    }

                    double[] values = new double[3] { AutoTDP, AutoTDP, AutoTDP + TDPBump };
                    RequestTDP(values, true);
                    AutoTDPPrev = AutoTDP;

                    // Reset interval to default after a TDP change
                    autotdpWatchdog.Interval = INTERVAL_AUTO;
                }
                else
                {
                    // Reduce interval to 100ms for quicker reaction next time a change is requierd
                    autotdpWatchdog.Interval = 115;
                }

                // are we done ?
                TDPdone = CurrentTDP[0] == StoredTDP[0] && CurrentTDP[1] == StoredTDP[1] && CurrentTDP[2] == StoredTDP[2];

                // processor specific
                if (processor is IntelProcessor)
                {
                    double TDPslow = StoredTDP[(int)PowerType.Slow];
                    double TDPfast = StoredTDP[(int)PowerType.Fast];

                    if (TDPslow != 0.0d && TDPfast != 0.0d)
                        // only request an update if current limit is different than stored
                        if (CurrentTDP[(int)PowerType.MsrSlow] != TDPslow || CurrentTDP[(int)PowerType.MsrFast] != TDPfast || forcedUpdate)
                        {
                            MSRdone = false;
                            RequestMSR(TDPslow, TDPfast);
                        }
                }

                // user requested to halt AutoTDP watchdog
                if (autotdpWatchdogPendingStop)
                {
                    if (autotdpWatchdog.Interval == INTERVAL_AUTO)
                    {
                        if (TDPdone && MSRdone)
                            autotdpWatchdog.Stop();
                    }
                    else if (autotdpWatchdog.Interval == INTERVAL_DEGRADED)
                    {
                        autotdpWatchdog.Stop();
                    }
                }
            }
            catch { }
            finally
            {
                // release lock
                autotdpLock.Exit();
            }
        }
    }

    private static double AutoTDPDipper(double FPSActual, double FPSSetpoint)
    {
        // Dipper
        // Add small positive "error" if actual and target FPS are similar for a duration
        double Modifier = 0.0d;

        // Track previous FPS values for average calculation using a rolling array
        Array.Copy(FPSHistory, 0, FPSHistory, 1, FPSHistory.Length - 1);
        FPSHistory[0] = FPSActual; // Add current FPS at the start

        // Activate around target range of 1 FPS as games can fluctuate
        if (FPSSetpoint - 1 <= FPSActual && FPSActual <= FPSSetpoint + 1)
        {
            AutoTDPFPSSetpointMetCounter++;

            // First wait for three seconds of stable FPS arount target, then perform small dip
            // Reduction only happens if average FPS is on target or slightly below
            if (AutoTDPFPSSetpointMetCounter >= 3 && AutoTDPFPSSetpointMetCounter < 6 &&
                FPSSetpoint - 0.5 <= FPSHistory.Take(3).Average() && FPSHistory.Take(3).Average() <= FPSSetpoint + 0.1)
            {
                AutoTDPFPSSmallDipCounter++;
                Modifier = FPSSetpoint + 0.5 - FPSActual;
            }
            // After three small dips, perform larger dip 
            // Reduction only happens if average FPS is on target or slightly below
            else if (AutoTDPFPSSmallDipCounter >= 3 &&
                     FPSSetpoint - 0.5 <= FPSHistory.Average() && FPSHistory.Average() <= FPSSetpoint + 0.1)
            {
                Modifier = FPSSetpoint + 1.5 - FPSActual;
                AutoTDPFPSSetpointMetCounter = 6;
            }
        }
        // Perform dips until FPS is outside of limits around target
        else
        {
            Modifier = 0.0;
            AutoTDPFPSSetpointMetCounter = 0;
            AutoTDPFPSSmallDipCounter = 0;
        }

        return Modifier;
    }

    private static double AutoTDPDamper(double FPSActual)
    {
        // (PI)D derivative control component to dampen FPS fluctuations
        if (double.IsNaN(ProcessValueFPSPrevious)) ProcessValueFPSPrevious = FPSActual;
        double DFactor = -0.1d;

        // Calculation
        double deltaError = FPSActual - ProcessValueFPSPrevious;
        double DTerm = deltaError / (INTERVAL_AUTO / 1000.0);
        double TDPDamping = AutoTDP / FPSActual * DFactor * DTerm;

        ProcessValueFPSPrevious = FPSActual;

        return TDPDamping;
    }

    private static void cpuWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (cpuLock.TryEnter())
        {
            try
            {
                if (currentProfile is not null)
                {
                    // Check if CPU clock speed has changed and apply if needed
                    if (currentProfile.CPUOverrideEnabled)
                        RequestCPUClock(Convert.ToUInt32(currentProfile.CPUOverrideValue));

                    // Check if CPU core count has changed and apply if needed
                    if (currentProfile.CPUCoreEnabled)
                        RequestCPUCoreCount(currentProfile.CPUCoreCount);

                    // Check if active power shceme has changed and apply if needed
                    RequestPowerMode(currentProfile.OSPowerMode);

                    // Check if PerfBoostMode value has changed and apply if needed
                    RequestPerfBoostMode((uint)currentProfile.CPUBoostLevel);
                }
            }
            catch { }
            finally
            {
                // release lock
                cpuLock.Exit();
            }
        }
    }

    private static void tdpWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        if (tdpLock.TryEnter())
        {
            try
            {
                bool TDPdone = false;
                bool MSRdone = true;

                // read current values and (re)apply requested TDP if needed
                for (int idx = (int)PowerType.Slow; idx <= (int)PowerType.Fast; idx++)
                {
                    double TDP = StoredTDP[idx];
                    if (TDP == 0.0d)
                        continue;

                    // AMD reduces TDP by 10% when OS power mode is set to Best power efficiency
                    if (processor is AMDProcessor && currentPowerMode == OSPowerMode.BetterBattery)
                        TDP = (int)Math.Truncate(TDP * 0.9);

                    // todo: find a way to read TDP limits
                    double ReadTDP = CurrentTDP[idx];
                    if (ReadTDP != 0)
                        tdpWatchdog.Interval = INTERVAL_DEFAULT;
                    else
                        tdpWatchdog.Interval = INTERVAL_DEGRADED;

                    // only request an update if current limit is different than stored
                    if (ReadTDP != TDP)
                        RequestTDP((PowerType)idx, TDP, true);

                    Thread.Sleep(200);
                }

                // are we done ?
                TDPdone = CurrentTDP[0] == StoredTDP[0] && CurrentTDP[1] == StoredTDP[1] && CurrentTDP[2] == StoredTDP[2];

                // processor specific
                if (processor is IntelProcessor)
                {
                    double TDPslow = StoredTDP[(int)PowerType.Slow];
                    double TDPfast = StoredTDP[(int)PowerType.Fast];

                    if (TDPslow != 0.0d && TDPfast != 0.0d)
                        // only request an update if current limit is different than stored
                        if (CurrentTDP[(int)PowerType.MsrSlow] != TDPslow || CurrentTDP[(int)PowerType.MsrFast] != TDPfast)
                        {
                            MSRdone = false;
                            RequestMSR(TDPslow, TDPfast);
                        }
                }

                // user requested to halt TDP watchdog
                if (tdpWatchdogPendingStop)
                {
                    if (tdpWatchdog.Interval == INTERVAL_DEFAULT)
                    {
                        if (TDPdone && MSRdone)
                            tdpWatchdog.Stop();
                    }
                    else if (tdpWatchdog.Interval == INTERVAL_DEGRADED)
                    {
                        tdpWatchdog.Stop();
                    }
                }
            }
            catch { }
            finally
            {
                // release lock
                tdpLock.Exit();
            }
        }
    }

    private static void gfxWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        GPU GPU = GPUManager.GetCurrent();
        if (GPU is null || !GPU.IsInitialized)
            return;

        if (gfxLock.TryEnter())
        {
            try
            {
                gfxWatchdogCounter++;

                bool GPUdone = true;
                bool forcedUpdate = false;

                // not ready yet
                if (StoredGfxClock == 0)
                    return;

                float CurrentGfxClock = GPUManager.GetCurrent().GetClock();

                if (CurrentGfxClock != 0)
                    gfxWatchdog.Interval = INTERVAL_DEFAULT;
                else
                    gfxWatchdog.Interval = INTERVAL_DEGRADED;

                if (gfxWatchdogCounter > COUNTER_DEFAULT)
                {
                    forcedUpdate = true;
                    gfxWatchdogCounter = 0;
                }

                // only request an update if current gfx clock is different than stored
                // or a forced update is requested
                if (CurrentGfxClock != StoredGfxClock || forcedUpdate)
                {
                    // disabling
                    if (StoredGfxClock != 12750)
                    {
                        GPUdone = false;
                        RequestGPUClock(StoredGfxClock, true);
                    }
                }

                // user requested to halt gpu watchdog
                if (gfxWatchdogPendingStop)
                {
                    if (gfxWatchdog.Interval == INTERVAL_DEFAULT)
                    {
                        if (GPUdone)
                            gfxWatchdog.Stop();
                    }
                    else if (gfxWatchdog.Interval == INTERVAL_DEGRADED)
                    {
                        gfxWatchdog.Stop();
                    }
                }
            }
            catch { }
            finally
            {
                // release lock
                gfxLock.Exit();
            }
        }
    }

    private static void StartGPUWatchdog()
    {
        gfxWatchdogPendingStop = false;
        gfxWatchdog.Interval = INTERVAL_DEFAULT;
        gfxWatchdog.Start();
    }

    private static void StopGPUWatchdog(bool immediate = false)
    {
        gfxWatchdogPendingStop = true;
        if (immediate)
            gfxWatchdog.Stop();
    }

    private static void StartTDPWatchdog()
    {
        tdpWatchdogPendingStop = false;
        tdpWatchdog.Interval = INTERVAL_DEFAULT;
        tdpWatchdog.Start();
    }

    private static void StopTDPWatchdog(bool immediate = false)
    {
        tdpWatchdogPendingStop = true;
        if (immediate)
            tdpWatchdog.Stop();
    }

    private static void StartAutoTDPWatchdog()
    {
        autotdpWatchdogPendingStop = false;
        autotdpWatchdog.Interval = INTERVAL_AUTO;
        autotdpWatchdog.Start();
    }

    private static void StopAutoTDPWatchdog(bool immediate = false)
    {
        autotdpWatchdogPendingStop = true;
        if (immediate)
            autotdpWatchdog.Stop();
    }

    private static void RequestTDP(PowerType type, double value, bool immediate = false)
    {
        // make sure we're not trying to run below or above specs
        value = Math.Min(TDPMax, Math.Max(TDPMin, value));

        // skip if value is invalid
        if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            return;

        // update value read by timer
        int idx = (int)type;
        StoredTDP[idx] = value;

        // skip if processor is not ready
        if (processor is null || !processor.IsInitialized)
            return;

        // immediately apply
        if (immediate)
        {
            CurrentTDP[idx] = value;

            if (processor is IntelProcessor)
                // Intel doesn't have stapm
                if (type == PowerType.Stapm)
                    return;

            processor.SetTDPLimit((PowerType)idx, value, immediate);
        }
    }

    private static async void RequestTDP(double[] values, bool immediate = false)
    {
        // Handle null or empty array scenario
        if (values == null || values.Length == 0)
            return;

        for (int idx = (int)PowerType.Slow; idx <= (int)PowerType.Fast; idx++)
        {
            RequestTDP((PowerType)idx, values[idx], immediate);
            await Task.Delay(200).ConfigureAwait(false); // Avoid blocking the synchronization context
        }
    }

    private static void RequestMSR(double PL1, double PL2)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        if (processor is IntelProcessor)
        {
            // make sure we're not trying to run below or above specs
            double TDPslow = Math.Min(TDPMax, Math.Max(TDPMin, PL1));
            double TDPfast = Math.Min(TDPMax, Math.Max(TDPMin, PL2));

            CurrentTDP[(int)PowerType.MsrSlow] = TDPslow;
            CurrentTDP[(int)PowerType.MsrFast] = TDPfast;
            ((IntelProcessor)processor).SetMSRLimit(TDPslow, TDPfast);
        }
    }

    private static void RequestGPUClock(double value, bool immediate = false)
    {
        // update value read by timer
        StoredGfxClock = value;

        if (processor is null || !processor.IsInitialized)
            return;

        // immediately apply
        if (immediate)
            processor.SetGPUClock(StoredGfxClock);
    }

    private static void RequestPowerMode(Guid guid)
    {
        if (PowerGetEffectiveOverlayScheme(out Guid activeScheme) == 0)
            if (activeScheme == guid)
                return;

        LogManager.LogDebug("User requested power scheme: {0}", currentPowerMode);

        if (PowerSetActiveOverlayScheme(currentPowerMode) != 0)
            LogManager.LogWarning("Failed to set requested power scheme: {0}", currentPowerMode);
        else
        {
            currentPowerMode = guid;

            int idx = Array.IndexOf(PowerModes, currentPowerMode);
            if (idx != -1)
                PowerModeChanged?.Invoke(idx);
        }
    }

    private static void RequestCoreParkingMode(CoreParkingMode coreParkingMode)
    {
        /*
         * HETEROGENEOUS_POLICY values:
         * 0: Default (no explicit preference)
         * 1: Prefer heterogeneous scheduling (allows mixed cores based on scheduling hints)
         * 2: Prefer E-cores exclusively (favor efficiency and battery life)
         * 3: Prefer P-cores exclusively (favor performance at all costs)
         
         * HETEROGENEOUS_THREAD_SCHEDULING_POLICY and HETEROGENEOUS_SHORT_THREAD_SCHEDULING_POLICY values: These settings instruct Windows Scheduler about how aggressively it should favor either core type for regular or short-lived threads:
         * 1: Strongly Prefer P-Cores (high-performance cores only)
         * 2: Prefer P-Cores (favor P-Cores but allow E-Cores occasionally)
         * 3: Strongly Prefer E-Cores (efficiency cores only)
         * 4: Prefer E-Cores (favor E-Cores but allow P-Cores occasionally)
         * 5: No specific preference (Windows decides automatically)
         */

        switch (coreParkingMode)
        {
            case CoreParkingMode.AllCoresPrefPCore:
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_POLICY, 1U, 1U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_THREAD_SCHEDULING_POLICY, 2U, 2U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_SHORT_THREAD_SCHEDULING_POLICY, 2U, 2U);
                break;
            case CoreParkingMode.AllCoresPrefECore:
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_POLICY, 1U, 1U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_THREAD_SCHEDULING_POLICY, 4U, 4U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_SHORT_THREAD_SCHEDULING_POLICY, 4U, 4U);
                break;
            case CoreParkingMode.OnlyPCore:
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_POLICY, 3U, 3U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_THREAD_SCHEDULING_POLICY, 1U, 1U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_SHORT_THREAD_SCHEDULING_POLICY, 1U, 1U);
                break;
            case CoreParkingMode.OnlyECore:
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_POLICY, 2U, 2U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_THREAD_SCHEDULING_POLICY, 3U, 3U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_SHORT_THREAD_SCHEDULING_POLICY, 3U, 3U);
                break;
            default:
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_POLICY, 0U, 0U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_THREAD_SCHEDULING_POLICY, 5U, 5U);
                PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.HETEROGENEOUS_SHORT_THREAD_SCHEDULING_POLICY, 5U, 5U);
                break;
        }

        LogManager.LogDebug("User requested Core Parking Mode: {0}", coreParkingMode);
    }

    [Obsolete("This function is deprecated and will be removed in future versions.")]
    private static void RequestEPP(uint EPPOverrideValue)
    {
        var requestedEPP = new uint[2]
        {
            (uint)Math.Max(0, (int)EPPOverrideValue - 17),
            (uint)Math.Max(0, (int)EPPOverrideValue)
        };

        // Is the EPP value already correct?
        uint[] EPP = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP);
        if (EPP[0] == requestedEPP[0] && EPP[1] == requestedEPP[1])
            return;

        LogManager.LogDebug("User requested EPP AC: {0}, DC: {1}", requestedEPP[0], requestedEPP[1]);

        // Set profile EPP
        PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP, requestedEPP[0], requestedEPP[1]);
        PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP1, requestedEPP[0], requestedEPP[1]);

        // Has the EPP value been applied?
        EPP = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP);
        if (EPP[0] != requestedEPP[0] || EPP[1] != requestedEPP[1])
            LogManager.LogWarning("Failed to set requested EPP");
        else
            EPPChanged?.Invoke(EPPOverrideValue);
    }

    private static void RequestCPUCoreCount(int CoreCount)
    {
        uint currentCoreCountPercent = (uint)((100.0d / MotherboardInfo.NumberOfCores) * CoreCount);

        // Is the CPMINCORES value already correct?
        uint[] CPMINCORES = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.CPMINCORES);
        bool CPMINCORESReady = (CPMINCORES[0] == currentCoreCountPercent && CPMINCORES[1] == currentCoreCountPercent);

        // Is the CPMAXCORES value already correct?
        uint[] CPMAXCORES = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.CPMAXCORES);
        bool CPMAXCORESReady = (CPMAXCORES[0] == currentCoreCountPercent && CPMAXCORES[1] == currentCoreCountPercent);

        if (CPMINCORESReady && CPMAXCORESReady)
            return;

        // Set profile CPMINCORES and CPMAXCORES
        PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.CPMINCORES, currentCoreCountPercent, currentCoreCountPercent);
        PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.CPMAXCORES, currentCoreCountPercent, currentCoreCountPercent);

        LogManager.LogDebug("User requested CoreCount: {0} ({1}%)", CoreCount, currentCoreCountPercent);

        // Has the CPMINCORES value been applied?
        CPMINCORES = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.CPMINCORES);
        if (CPMINCORES[0] != currentCoreCountPercent || CPMINCORES[1] != currentCoreCountPercent)
            LogManager.LogWarning("Failed to set requested CPMINCORES");

        // Has the CPMAXCORES value been applied?
        CPMAXCORES = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.CPMAXCORES);
        if (CPMAXCORES[0] != currentCoreCountPercent || CPMAXCORES[1] != currentCoreCountPercent)
            LogManager.LogWarning("Failed to set requested CPMAXCORES");
    }

    private static void RequestPerfBoostMode(uint value)
    {
        // Is the PerfBoostMode value already correct?
        uint[] perfBoostMode = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFBOOSTMODE);
        bool IsReady = (perfBoostMode[0] == value && perfBoostMode[1] == value);

        if (IsReady)
            return;

        // Set profile PerfBoostMode
        PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFBOOSTMODE, value, value);

        LogManager.LogDebug("User requested perfboostmode: {0}", value);

        // Has the value been applied?
        perfBoostMode = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFBOOSTMODE);
        if (perfBoostMode[0] != value || perfBoostMode[1] != value)
            LogManager.LogWarning("Failed to set requested perfboostmode");
    }

    private static void RequestCPUClock(uint cpuClock)
    {
        // Is the PROCFREQMAX value already correct?
        uint[] currentClock = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PROCFREQMAX);
        bool IsReady = (currentClock[0] == cpuClock && currentClock[1] == cpuClock);

        if (IsReady)
            return;

        // Set profile max processor frequency
        PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PROCFREQMAX, cpuClock, cpuClock);
        PowerScheme.WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PROCFREQMAX1, cpuClock, cpuClock);

        double maxClock = MotherboardInfo.ProcessorMaxTurboSpeed;
        double cpuPercentage = cpuClock / maxClock * 100.0d;
        LogManager.LogDebug("User requested PROCFREQMAX: {0} ({1}%)", cpuClock, cpuPercentage);

        // Has the value been applied?
        currentClock = PowerScheme.ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PROCFREQMAX);
        if (currentClock[0] != cpuClock || currentClock[1] != cpuClock)
            LogManager.LogWarning("Failed to set requested PROCFREQMAX");
    }

    public static void Resume(bool OS)
    {
        foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
        {
            int idx = (int)type;
            CurrentTDP[idx] = 0;
        }
    }

    public static Processor? GetProcessor() => processor;

    #region imports

    /// <summary>
    ///     Retrieves the active overlay power scheme and returns a GUID that identifies the scheme.
    /// </summary>
    /// <param name="EffectiveOverlayPolicyGuid">A pointer to a GUID structure.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
    private static extern uint PowerGetEffectiveOverlayScheme(out Guid EffectiveOverlayPolicyGuid);

    /// <summary>
    ///     Sets the active power overlay power scheme.
    /// </summary>
    /// <param name="OverlaySchemeGuid">The identifier of the overlay power scheme.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImportAttribute("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
    private static extern uint PowerSetActiveOverlayScheme(Guid OverlaySchemeGuid);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    #endregion

    #region events

    public static event LimitChangedHandler PowerLimitChanged;
    public delegate void LimitChangedHandler(PowerType type, int limit);

    public static event ValueChangedHandler PowerValueChanged;
    public delegate void ValueChangedHandler(PowerType type, float value);

    public static event PowerModeChangedEventHandler PowerModeChanged;
    public delegate void PowerModeChangedEventHandler(int idx);

    public static event PerfBoostModeChangedEventHandler PerfBoostModeChanged;
    public delegate void PerfBoostModeChangedEventHandler(uint value);

    public static event EPPChangedEventHandler EPPChanged;
    public delegate void EPPChangedEventHandler(uint EPP);

    #endregion
}
