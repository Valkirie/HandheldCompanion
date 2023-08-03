using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using PowerProfileUtils;
using RTSSSharedMemoryNET;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class PowerMode
{
    /// <summary>
    ///     Better Battery mode.
    /// </summary>
    public static Guid BetterBattery = new("961cc777-2547-4f9d-8174-7d86181b8a7a");

    /// <summary>
    ///     Better Performance mode.
    /// </summary>
    // public static Guid BetterPerformance = new Guid("3af9B8d9-7c97-431d-ad78-34a8bfea439f");
    public static Guid BetterPerformance = new("00000000-0000-0000-0000-000000000000");

    /// <summary>
    ///     Best Performance mode.
    /// </summary>
    public static Guid BestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");
}

public class PerformanceManager : Manager
{
    private const short INTERVAL_DEFAULT = 1000; // default interval between value scans
    private const short INTERVAL_AUTO = 1010; // default interval between value scans
    private const short INTERVAL_DEGRADED = 5000; // degraded interval between value scans
    public static int MaxDegreeOfParallelism = 4;

    private static readonly Guid[] PowerModes = new Guid[3]
        { PowerMode.BetterBattery, PowerMode.BetterPerformance, PowerMode.BestPerformance };

    private readonly Timer autoWatchdog;
    private readonly Timer cpuWatchdog;
    private readonly Timer gfxWatchdog;
    private readonly Timer powerWatchdog;

    private bool autoLock;
    private bool cpuLock;
    private bool gfxLock;
    private bool powerLock;

    // AutoTDP
    private double AutoTDP;
    private double AutoTDPPrev;
    private bool AutoTDPFirstRun = true;
    private int AutoTDPFPSSetpointMetCounter;
    private int AutoTDPFPSSmallDipCounter;
    private double TDPMax;
    private double TDPMin;
    private int AutoTDPProcessId;
    private double AutoTDPTargetFPS;
    private bool cpuWatchdogPendingStop;
    private uint currentEPP = 50;
    private double CurrentGfxClock;

    // powercfg
    private bool currentPerfBoostMode;
    private Guid currentPowerMode = new("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
    private readonly double[] CurrentTDP = new double[5]; // used to store current TDP

    // GPU limits
    private double FallbackGfxClock;
    private readonly double[] FPSHistory = new double[6];
    private bool gfxWatchdogPendingStop;

    private Processor processor;
    private double ProcessValueFPSPrevious;
    private double StoredGfxClock;

    // TDP limits
    private readonly double[] StoredTDP = new double[3]; // used to store TDP

    public PerformanceManager()
    {
        // initialize timer(s)
        powerWatchdog = new Timer { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
        powerWatchdog.Elapsed += powerWatchdog_Elapsed;

        cpuWatchdog = new Timer { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
        cpuWatchdog.Elapsed += cpuWatchdog_Elapsed;

        gfxWatchdog = new Timer { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
        gfxWatchdog.Elapsed += gfxWatchdog_Elapsed;

        autoWatchdog = new Timer { Interval = INTERVAL_AUTO, AutoReset = true, Enabled = false };
        autoWatchdog.Elapsed += AutoTDPWatchdog_Elapsed;

        ProfileManager.Applied += ProfileManager_Applied;
        ProfileManager.Discarded += ProfileManager_Discarded;

        PlatformManager.HWiNFO.PowerLimitChanged += HWiNFO_PowerLimitChanged;
        PlatformManager.HWiNFO.GPUFrequencyChanged += HWiNFO_GPUFrequencyChanged;

        PlatformManager.RTSS.Hooked += RTSS_Hooked;
        PlatformManager.RTSS.Unhooked += RTSS_Unhooked;

        // initialize settings
        SettingsManager.SettingValueChanged += SettingsManagerOnSettingValueChanged;

        MaxDegreeOfParallelism = Convert.ToInt32(Environment.ProcessorCount / 2);
    }

    private void SettingsManagerOnSettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "ConfigurableTDPOverrideDown":
            {
                TDPMin = Convert.ToDouble(value);
                AutoTDP = (TDPMax + TDPMin) / 2.0d;
            }
                break;
            case "ConfigurableTDPOverrideUp":
            {
                TDPMax = Convert.ToDouble(value);
                AutoTDP = (TDPMax + TDPMin) / 2.0d;
            }
                break;
        }
    }

    private void ProfileManager_Applied(Profile profile, ProfileUpdateSource source)
    {
        // apply profile defined TDP
        if (profile.TDPOverrideEnabled && profile.TDPOverrideValues is not null)
        {
            RequestTDP(profile.TDPOverrideValues);
            StartTDPWatchdog();
        }
        else if (cpuWatchdog.Enabled)
        {
            StopTDPWatchdog(true);

            // restore default TDP (if not AutoTDP is enabled)
            if (!profile.AutoTDPEnabled)
                RestoreTDP(true);
        }

        // apply profile defined AutoTDP
        if (profile.AutoTDPEnabled)
        {
            AutoTDPTargetFPS = profile.AutoTDPRequestedFPS;
            StartAutoTDPWatchdog();
        }
        else if (autoWatchdog.Enabled)
        {
            StopAutoTDPWatchdog(true);

            // restore default TDP (if not manual TDP is enabled)
            if (!profile.TDPOverrideEnabled)
                RestoreTDP(true);
        }

        // apply profile defined GPU
        if (profile.GPUOverrideEnabled)
        {
            RequestGPUClock(profile.GPUOverrideValue);
            StartGPUWatchdog();
        }
        else if (gfxWatchdog.Enabled)
        {
            StopGPUWatchdog(true);
            RestoreGPUClock(true);
        }

        // apply profile defined EPP
        if (profile.EPPOverrideEnabled)
        {
            RequestEPP(profile.EPPOverrideValue);
        }
    }

    private void ProfileManager_Discarded(Profile profile)
    {
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
            StopTDPWatchdog(true);
            RestoreTDP(true);
        }

        // restore default GPU frequency
        if (profile.GPUOverrideEnabled)
        {
            StopGPUWatchdog(true);
            RestoreGPUClock(true);
        }

        // (un)apply profile defined EPP
        if (profile.EPPOverrideEnabled)
        {
            // restore default EPP
            RequestEPP(0x00000032);
        }
    }

    private void RestoreTDP(bool immediate)
    {
        for (PowerType pType = PowerType.Slow; pType <= PowerType.Fast; pType++)
            RequestTDP(pType, MainWindow.CurrentDevice.cTDP[1], immediate);
    }

    private void RestoreGPUClock(bool immediate)
    {
        RequestGPUClock(255 * 50, immediate);
    }

    private void RTSS_Hooked(AppEntry appEntry)
    {
        AutoTDPProcessId = appEntry.ProcessId;
    }

    private void RTSS_Unhooked(int processId)
    {
        AutoTDPProcessId = 0;
    }

    private void AutoTDPWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        // We don't have any hooked process
        if (AutoTDPProcessId == 0)
            return;

        if (!autoLock)
        {
            // set lock
            autoLock = true;

            // todo: Store fps for data gathering from multiple points (OSD, Performance)
            var processValueFPS = PlatformManager.RTSS.GetFramerate(AutoTDPProcessId);

            // Ensure realistic process values, prevent divide by 0
            processValueFPS = Math.Clamp(processValueFPS, 5, 500);

            // Determine error amount, include target, actual and dipper modifier
            var controllerError = AutoTDPTargetFPS - processValueFPS - AutoTDPDipper(processValueFPS, AutoTDPTargetFPS);

            // Clamp error amount corrected within a single cycle
            // Adjust clamp if actual FPS is 2.5x requested FPS
            double clampLowerLimit = processValueFPS >= 2.5 * AutoTDPTargetFPS ? -100 : -5;
            controllerError = Math.Clamp(controllerError, clampLowerLimit, 15);

            var TDPAdjustment = controllerError * AutoTDP / processValueFPS;
            TDPAdjustment *= 0.9; // Always have a little undershoot

            // Determine final setpoint
            if (!AutoTDPFirstRun)
                AutoTDP += TDPAdjustment + AutoTDPDamper(processValueFPS);
            else
                AutoTDPFirstRun = false;

            AutoTDP = Math.Clamp(AutoTDP, TDPMin, TDPMax);

            // Only update if we have a different TDP value to set
            if (AutoTDP != AutoTDPPrev)
            {
                var values = new double[3] { AutoTDP, AutoTDP, AutoTDP };
                RequestTDP(values, true);
            }
            AutoTDPPrev = AutoTDP;

            // LogManager.LogTrace("TDPSet;;;;;{0:0.0};{1:0.000};{2:0.0000};{3:0.0000};{4:0.0000}", AutoTDPTargetFPS, AutoTDP, TDPAdjustment, ProcessValueFPS, TDPDamping);

            // release lock
            autoLock = false;
        }
    }

    private double AutoTDPDipper(double FPSActual, double FPSSetpoint)
    {
        // Dipper
        // Add small positive "error" if actual and target FPS are similar for a duration
        var Modifier = 0.0;

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

    private double AutoTDPDamper(double FPSActual)
    {
        // (PI)D derivative control component to dampen FPS fluctuations
        if (double.IsNaN(ProcessValueFPSPrevious)) ProcessValueFPSPrevious = FPSActual;
        var DFactor = -0.1;

        // Calculation
        var deltaError = FPSActual - ProcessValueFPSPrevious;
        var DTerm = deltaError / (INTERVAL_AUTO / 1000.0);
        var TDPDamping = AutoTDP / FPSActual * DFactor * DTerm;

        ProcessValueFPSPrevious = FPSActual;

        return TDPDamping;
    }

    private void powerWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!powerLock)
        {
            // set lock
            powerLock = true;

            // Checking if active power shceme has changed to reflect that
            if (PowerGetEffectiveOverlayScheme(out var activeScheme) == 0)
                if (activeScheme != currentPowerMode)
                {
                    currentPowerMode = activeScheme;
                    var idx = Array.IndexOf(PowerModes, activeScheme);
                    if (idx != -1)
                        PowerModeChanged?.Invoke(idx);
                }

            // read perfboostmode
            var result = ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFBOOSTMODE);
            var perfboostmode = result[(int)PowerIndexType.AC] == (uint)PerfBoostMode.Aggressive &&
                                result[(int)PowerIndexType.DC] == (uint)PerfBoostMode.Aggressive;

            if (perfboostmode != currentPerfBoostMode)
            {
                currentPerfBoostMode = perfboostmode;
                PerfBoostModeChanged?.Invoke(perfboostmode);
            }

            // Checking if current EPP value has changed to reflect that
            var EPP = ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP);
            var DCvalue = EPP[(int)PowerIndexType.DC];

            if (DCvalue != currentEPP)
            {
                currentEPP = DCvalue;
                EPPChanged?.Invoke(DCvalue);
            }

            // release lock
            powerLock = false;
        }
    }

    private async void cpuWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        if (!cpuLock)
        {
            // set lock
            cpuLock = true;

            var TDPdone = false;
            var MSRdone = false;

            // read current values and (re)apply requested TDP if needed
            foreach (var type in (PowerType[])Enum.GetValues(typeof(PowerType)))
            {
                var idx = (int)type;

                // skip msr
                if (idx >= StoredTDP.Length)
                    break;

                // Wanted TDP
                var TDP = StoredTDP[idx];

                // Actual TDP
                var ReadTDP = CurrentTDP[idx];

                if (processor is AMDProcessor amdProcessor)
                {
                    // AMD reduces TDP by 10% when OS power mode is set to Best power efficiency
                    if (currentPowerMode == PowerMode.BetterBattery)
                        TDP = (int)Math.Truncate(TDP * 0.9);

                    // Try to use RyzenAdj to read actual TDP
                    if (amdProcessor.TryGetTDPLimit(type, out var tdpLimit))
                    {
                        ReadTDP = tdpLimit;
                    }

                }
                else if (processor.GetType() == typeof(IntelProcessor))
                {
                    // Intel doesn't have stapm
                    if (type == PowerType.Stapm)
                        continue;
                }



                if (ReadTDP != 0)
                    cpuWatchdog.Interval = INTERVAL_DEFAULT;
                else
                    cpuWatchdog.Interval = INTERVAL_DEGRADED;

                // only request an update if current limit is different than stored
                if (Math.Abs(ReadTDP - TDP) > 0.001)
                    processor.SetTDPLimit(type, TDP);

                await Task.Delay(12);
            }

            // are we done ?
            TDPdone = CurrentTDP[0] == StoredTDP[0] && CurrentTDP[1] == StoredTDP[1] && CurrentTDP[2] == StoredTDP[2];

            // processor specific
            if (processor.GetType() == typeof(IntelProcessor))
            {
                var TDPslow = (int)StoredTDP[(int)PowerType.Slow];
                var TDPfast = (int)StoredTDP[(int)PowerType.Fast];

                // only request an update if current limit is different than stored
                if (CurrentTDP[(int)PowerType.MsrSlow] != TDPslow ||
                    CurrentTDP[(int)PowerType.MsrFast] != TDPfast)
                    ((IntelProcessor)processor).SetMSRLimit(TDPslow, TDPfast);
                else
                    MSRdone = true;
            }

            // user requested to halt cpu watchdog
            if (cpuWatchdogPendingStop)
            {
                if (cpuWatchdog.Interval == INTERVAL_DEFAULT)
                {
                    if (TDPdone && MSRdone)
                        cpuWatchdog.Stop();
                }
                else if (cpuWatchdog.Interval == INTERVAL_DEGRADED)
                {
                    cpuWatchdog.Stop();
                }
            }

            // release lock
            cpuLock = false;
        }
    }

    private void gfxWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        if (!gfxLock)
        {
            // set lock
            gfxLock = true;

            var GPUdone = false;

            if (CurrentGfxClock != 0)
                gfxWatchdog.Interval = INTERVAL_DEFAULT;
            else
                gfxWatchdog.Interval = INTERVAL_DEGRADED;

            // not ready yet
            if (StoredGfxClock == 0)
            {
                // release lock
                gfxLock = false;
                return;
            }

            // only request an update if current gfx clock is different than stored
            if (CurrentGfxClock != StoredGfxClock)
            {
                // disabling
                if (StoredGfxClock == 12750)
                    GPUdone = true;
                else
                    processor.SetGPUClock(StoredGfxClock);
            }
            else
            {
                GPUdone = true;
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

            // release lock
            gfxLock = false;
        }
    }

    internal void StartGPUWatchdog()
    {
        gfxWatchdogPendingStop = false;
        gfxWatchdog.Interval = INTERVAL_DEFAULT;
        gfxWatchdog.Start();
    }

    internal void StopGPUWatchdog(bool immediate = false)
    {
        gfxWatchdogPendingStop = true;
        if (immediate)
            gfxWatchdog.Stop();
    }

    internal void StartTDPWatchdog()
    {
        cpuWatchdogPendingStop = false;
        cpuWatchdog.Interval = INTERVAL_DEFAULT;
        cpuWatchdog.Start();
    }

    internal void StopTDPWatchdog(bool immediate = false)
    {
        cpuWatchdogPendingStop = true;
        if (immediate)
            cpuWatchdog.Stop();
    }

    internal void StartAutoTDPWatchdog()
    {
        autoWatchdog.Start();
    }

    internal void StopAutoTDPWatchdog(bool immediate = false)
    {
        autoWatchdog.Stop();
    }

    public void RequestTDP(PowerType type, double value, bool immediate = false)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        // make sure we're not trying to run below or above specs
        value = Math.Min(TDPMax, Math.Max(TDPMin, value));

        // update value read by timer
        var idx = (int)type;
        StoredTDP[idx] = value;

        // immediately apply
        if (immediate)
            processor.SetTDPLimit((PowerType)idx, value);
    }

    public async void RequestTDP(double[] values, bool immediate = false)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        for (var idx = (int)PowerType.Slow; idx <= (int)PowerType.Fast; idx++)
        {
            // make sure we're not trying to run below or above specs
            values[idx] = Math.Min(TDPMax, Math.Max(TDPMin, values[idx]));

            // update value read by timer
            StoredTDP[idx] = values[idx];

            // immediately apply
            if (immediate)
            {
                processor.SetTDPLimit((PowerType)idx, values[idx]);
                await Task.Delay(12);
            }
        }
    }

    public void RequestGPUClock(double value, bool immediate = false)
    {
        if (processor is null || !processor.IsInitialized)
            return;

        // update value read by timer
        StoredGfxClock = value;

        // immediately apply
        if (immediate)
            processor.SetGPUClock(value);
    }

    public void RequestPowerMode(int idx)
    {
        currentPowerMode = PowerModes[idx];
        LogManager.LogInformation("User requested power scheme: {0}", currentPowerMode);
        if (PowerSetActiveOverlayScheme(currentPowerMode) != 0)
            LogManager.LogWarning("Failed to set requested power scheme: {0}", currentPowerMode);
    }

    public void RequestEPP(uint EPPOverrideValue)
    {
        currentEPP = EPPOverrideValue;

        var requestedEPP = new uint[2]
        {
            (uint)Math.Max(0, (int)EPPOverrideValue - 17),
            (uint)Math.Max(0, (int)EPPOverrideValue)
        };

        // Is the EPP value already correct?
        uint[] EPP = ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP);
        if (EPP[0] == requestedEPP[0] && EPP[1] == requestedEPP[1])
            return;

        LogManager.LogInformation("User requested EPP AC: {0}, DC: {1}", requestedEPP[0], requestedEPP[1]);

        // Set profile EPP
        WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP, requestedEPP[0], requestedEPP[1]);
        WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP1, requestedEPP[0], requestedEPP[1]);

        // Has the EPP value been applied?
        EPP = ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFEPP);
        if (EPP[0] != requestedEPP[0] || EPP[1] != requestedEPP[1])
            LogManager.LogWarning("Failed to set requested EPP");
    }

    public void RequestPerfBoostMode(bool value)
    {
        currentPerfBoostMode = value;

        var perfboostmode = value ? (uint)PerfBoostMode.Aggressive : (uint)PerfBoostMode.Disabled;
        WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFBOOSTMODE, perfboostmode, perfboostmode);

        LogManager.LogInformation("User requested perfboostmode: {0}", value);
    }

    private uint[] ReadPowerCfg(Guid SubGroup, Guid Settings)
    {
        var results = new uint[2];

        if (PowerProfile.GetActiveScheme(out var currentScheme))
        {
            // read AC/DC values
            PowerProfile.GetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings,
                out results[(int)PowerIndexType.AC]);
            PowerProfile.GetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings,
                out results[(int)PowerIndexType.DC]);
        }

        return results;
    }

    private void WritePowerCfg(Guid SubGroup, Guid Settings, uint ACValue, uint DCValue)
    {
        if (PowerProfile.GetActiveScheme(out var currentScheme))
        {
            // unhide attribute
            PowerProfile.SetAttribute(SubGroup, Settings, false);

            // set value(s)
            PowerProfile.SetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings, ACValue);
            PowerProfile.SetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings, DCValue);

            // activate scheme
            PowerProfile.SetActiveScheme(currentScheme);
        }
    }

    public override void Start()
    {
        // initialize watchdog(s)
        powerWatchdog.Start();

        // initialize processor
        processor = Processor.GetCurrent();

        // read OS specific values
        var HypervisorEnforcedCodeIntegrityEnabled = RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios",
            "HypervisorEnforcedCodeIntegrity");
        var VulnerableDriverBlocklistEnable = RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\CI\Config",
            "VulnerableDriverBlocklistEnable");

        if (VulnerableDriverBlocklistEnable || HypervisorEnforcedCodeIntegrityEnabled)
            LogManager.LogWarning("Core isolation settings are turned on. TDP read/write and fan control might be disabled");

        if (processor.IsInitialized)
        {
            processor.StatusChanged += Processor_StatusChanged;
            processor.Initialize();
        }

        // deprecated
        /*
        processor.ValueChanged += Processor_ValueChanged;
        processor.LimitChanged += Processor_LimitChanged;
        processor.MiscChanged += Processor_MiscChanged;
        */

        base.Start();
    }

    public override void Stop()
    {
        if (!IsInitialized)
            return;

        processor.Stop();

        powerWatchdog.Stop();
        cpuWatchdog.Stop();
        gfxWatchdog.Stop();
        autoWatchdog.Stop();

        base.Stop();
    }

    public Processor GetProcessor()
    {
        return processor;
    }

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

    #endregion

    #region events

    public event LimitChangedHandler PowerLimitChanged;

    public delegate void LimitChangedHandler(PowerType type, int limit);

    public event ValueChangedHandler PowerValueChanged;

    public delegate void ValueChangedHandler(PowerType type, float value);

    public event StatusChangedHandler ProcessorStatusChanged;

    public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

    public event PowerModeChangedEventHandler PowerModeChanged;

    public delegate void PowerModeChangedEventHandler(int idx);

    public event PerfBoostModeChangedEventHandler PerfBoostModeChanged;

    public delegate void PerfBoostModeChangedEventHandler(bool value);

    public event EPPChangedEventHandler EPPChanged;

    public delegate void EPPChangedEventHandler(uint EPP);

    #endregion

    #region events

    private void HWiNFO_PowerLimitChanged(PowerType type, int limit)
    {
        var idx = (int)type;
        CurrentTDP[idx] = limit;

        // workaround, HWiNFO doesn't have the ability to report MSR
        switch (type)
        {
            case PowerType.Slow:
                CurrentTDP[(int)PowerType.Stapm] = limit;
                CurrentTDP[(int)PowerType.MsrSlow] = limit;
                break;
            case PowerType.Fast:
                CurrentTDP[(int)PowerType.MsrFast] = limit;
                break;
        }

        // raise event
        PowerLimitChanged?.Invoke(type, limit);

        LogManager.LogTrace("PowerLimitChanged: {0}\t{1} W", type, limit);
    }

    private void HWiNFO_GPUFrequencyChanged(double value)
    {
        CurrentGfxClock = value;

        LogManager.LogTrace("GPUFrequencyChanged: {0} Mhz", value);
    }

    private void Processor_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
    {
        ProcessorStatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);
    }

    [Obsolete("Method is deprecated.")]
    private void Processor_ValueChanged(PowerType type, float value)
    {
        PowerValueChanged?.Invoke(type, value);
    }

    [Obsolete("Method is deprecated.")]
    private void Processor_LimitChanged(PowerType type, int limit)
    {
        var idx = (int)type;
        CurrentTDP[idx] = limit;

        // raise event
        PowerLimitChanged?.Invoke(type, limit);
    }

    [Obsolete("Method is deprecated.")]
    private void Processor_MiscChanged(string misc, float value)
    {
        switch (misc)
        {
            case "gfx_clk":
            {
                CurrentGfxClock = value;
            }
                break;
        }
    }

    #endregion
}