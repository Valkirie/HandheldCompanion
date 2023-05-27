using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using PowerProfileUtils;
using RTSSSharedMemoryNET;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public static class PowerMode
    {
        /// <summary>
        /// Better Battery mode.
        /// </summary>
        public static Guid BetterBattery = new("961cc777-2547-4f9d-8174-7d86181b8a7a");

        /// <summary>
        /// Better Performance mode.
        /// </summary>
        // public static Guid BetterPerformance = new Guid("3af9B8d9-7c97-431d-ad78-34a8bfea439f");
        public static Guid BetterPerformance = new("00000000-0000-0000-0000-000000000000");

        /// <summary>
        /// Best Performance mode.
        /// </summary>
        public static Guid BestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");
    }

    public class PerformanceManager : Manager
    {
        #region imports
        /// <summary>
        /// Retrieves the active overlay power scheme and returns a GUID that identifies the scheme.
        /// </summary>
        /// <param name="EffectiveOverlayPolicyGuid">A pointer to a GUID structure.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
        private static extern uint PowerGetEffectiveOverlayScheme(out Guid EffectiveOverlayPolicyGuid);

        /// <summary>
        /// Sets the active power overlay power scheme.
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
        #endregion

        private Processor processor;
        public static int MaxDegreeOfParallelism = 4;

        private static readonly Guid[] PowerModes = new Guid[3] { PowerMode.BetterBattery, PowerMode.BetterPerformance, PowerMode.BestPerformance };
        private Guid currentPowerMode = new("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
        private readonly Timer powerWatchdog;
        private readonly object powerLock = new();

        // powercfg
        private bool currentPerfBoostMode = false;

        private readonly Timer cpuWatchdog;
        protected readonly object cpuLock = new();
        private bool cpuWatchdogPendingStop;

        private readonly Timer gfxWatchdog;
        protected readonly object gfxLock = new();
        private bool gfxWatchdogPendingStop;

        private readonly Timer AutoTDPWatchdog;
        protected readonly object AutoTDPWatchdogLock = new();

        private const short INTERVAL_DEFAULT = 1000;            // default interval between value scans
        private const short INTERVAL_AUTO = 1000;               // default interval between value scans
        private const short INTERVAL_DEGRADED = 5000;           // degraded interval between value scans

        // TDP limits
        private double[] StoredTDP = new double[3];     // used to store TDP
        private double[] CurrentTDP = new double[5];    // used to store current TDP

        // GPU limits
        private double FallbackGfxClock;
        private double StoredGfxClock;
        private double CurrentGfxClock;

        // AutoTDP
        private bool AutoTDPFirstRun = true;
        private int AutoTDPProcessId;

        private double AutoTDP;
        private double AutoTDPTargetFPS;
        private double AutoTDPMin;
        private double AutoTDPMax;
        private int AutoTDPFPSSetpointMetCounter;
        private int AutoTDPFPSSmallDipCounter;
        private double ProcessValueFPSPrevious;
        private double[] FPSHistory = new double[6];

        public PerformanceManager() : base()
        {
            // initialize timer(s)
            powerWatchdog = new Timer() { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
            powerWatchdog.Elapsed += powerWatchdog_Elapsed;

            cpuWatchdog = new Timer() { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
            cpuWatchdog.Elapsed += cpuWatchdog_Elapsed;

            gfxWatchdog = new Timer() { Interval = INTERVAL_DEFAULT, AutoReset = true, Enabled = false };
            gfxWatchdog.Elapsed += gfxWatchdog_Elapsed;

            AutoTDPWatchdog = new Timer() { Interval = INTERVAL_AUTO, AutoReset = true, Enabled = false };
            AutoTDPWatchdog.Elapsed += AutoTDPWatchdog_Elapsed;

            ProfileManager.Applied += ProfileManager_Applied;

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
                        AutoTDPMin = Convert.ToDouble(value);
                        AutoTDP = (AutoTDPMax + AutoTDPMin) / 2.0d;
                    }
                    break;
                case "ConfigurableTDPOverrideUp":
                    {
                        AutoTDPMax = Convert.ToDouble(value);
                        AutoTDP = (AutoTDPMax + AutoTDPMin) / 2.0d;
                    }
                    break;
            }
        }

        private void ProfileManager_Applied(Profile profile)
        {
            // apply profile defined TDP
            if (profile.TDPOverrideEnabled)
            {
                RequestTDP(profile.TDPOverrideValues);
                StartTDPWatchdog();
            }
            else
            {
                RequestTDP(MainWindow.CurrentDevice.nTDP);
                StopTDPWatchdog();
            }

            // apply profile defined GPU
            if (profile.GPUOverrideEnabled)
            {
                RequestGPUClock(profile.GPUOverrideValue);
                StartGPUWatchdog();
            }
            else
            {
                RequestGPUClock(255 * 50);
                StopGPUWatchdog();
            }

            // AutoTDP
            if (profile.AutoTDPEnabled)
            {
                AutoTDPTargetFPS = profile.AutoTDPRequestedFPS;
                AutoTDPWatchdog.Start();
            }
            else
            {
                AutoTDPWatchdog.Stop();
            }
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

            if (Monitor.TryEnter(AutoTDPWatchdogLock))
            {
                // todo: Store fps for data gathering from multiple points (OSD, Performance)
                double processValueFPS = PlatformManager.RTSS.GetFramerate(AutoTDPProcessId);

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
                }
                else
                {
                    AutoTDPFirstRun = false;
                }

                AutoTDP = Math.Clamp(AutoTDP, AutoTDPMin, AutoTDPMax);

                var values = new double[3] { AutoTDP, AutoTDP, AutoTDP };
                RequestTDP(values, true);

                // LogManager.LogInformation("TDPSet;;;;;{0:0.0};{1:0.000};{2:0.0000};{3:0.0000};{4:0.0000}", AutoTDPTargetFPS, AutoTDP, TDPAdjustment, ProcessValueFPS, TDPDamping);

                Monitor.Exit(AutoTDPWatchdogLock);
            }
        }

        private double AutoTDPDipper(double FPSActual, double FPSSetpoint)
        {
            // Dipper
            // Add small positive "error" if actual and target FPS are similar for a duration
            double Modifier = 0.0;

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
            if (double.IsNaN(ProcessValueFPSPrevious)) { ProcessValueFPSPrevious = FPSActual; }
            double DFactor = -0.1;

            // Calculation
            double deltaError = FPSActual - ProcessValueFPSPrevious;
            double DTerm = deltaError / ((double)INTERVAL_AUTO / 1000.0);
            double TDPDamping = AutoTDP / FPSActual * DFactor * DTerm;
            
            ProcessValueFPSPrevious = FPSActual;

            return TDPDamping;
        }

        private void powerWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(powerLock))
            {
                // Checking if active power shceme has changed to reflect that
                if (PowerGetEffectiveOverlayScheme(out Guid activeScheme) == 0)
                {
                    if (activeScheme != currentPowerMode)
                    {
                        currentPowerMode = activeScheme;
                        int idx = Array.IndexOf(PowerModes, activeScheme);
                        if (idx != -1)
                            PowerModeChanged?.Invoke(idx);
                    }
                }

                // read perfboostmode
                uint[] result = ReadPowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFBOOSTMODE);
                bool perfboostmode = result[(int)PowerIndexType.AC] == (uint)PerfBoostMode.Aggressive && result[(int)PowerIndexType.DC] == (uint)PerfBoostMode.Aggressive;

                if (perfboostmode != currentPerfBoostMode)
                {
                    currentPerfBoostMode = perfboostmode;
                    PerfBoostModeChanged?.Invoke(perfboostmode);
                }

                Monitor.Exit(powerLock);
            }
        }

        private void cpuWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (processor is null || !processor.IsInitialized)
                return;

            if (Monitor.TryEnter(cpuLock))
            {
                bool TDPdone = false;
                bool MSRdone = false;

                // read current values and (re)apply requested TDP if needed
                foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
                {
                    int idx = (int)type;

                    // skip msr
                    if (idx >= StoredTDP.Length)
                        break;

                    double TDP = StoredTDP[idx];

                    if (processor.GetType() == typeof(AMDProcessor))
                    {
                        // AMD reduces TDP by 10% when OS power mode is set to Best power efficiency
                        if (currentPowerMode == PowerMode.BetterBattery)
                            TDP = (int)Math.Truncate(TDP * 0.9);
                    }
                    else if (processor.GetType() == typeof(IntelProcessor))
                    {
                        // Intel doesn't have stapm
                        if (type == PowerType.Stapm)
                            continue;
                    }

                    double ReadTDP = CurrentTDP[idx];

                    if (ReadTDP > byte.MinValue && ReadTDP < byte.MaxValue)
                        cpuWatchdog.Interval = INTERVAL_DEFAULT;
                    else
                        cpuWatchdog.Interval = INTERVAL_DEGRADED;

                    // only request an update if current limit is different than stored
                    if (ReadTDP != TDP)
                        processor.SetTDPLimit(type, TDP);
                }

                // are we done ?
                TDPdone = CurrentTDP[0] == StoredTDP[0] && CurrentTDP[1] == StoredTDP[1] && CurrentTDP[2] == StoredTDP[2];

                // processor specific
                if (processor.GetType() == typeof(IntelProcessor))
                {
                    // not ready yet
                    if (CurrentTDP[(int)PowerType.MsrSlow] == 0 || CurrentTDP[(int)PowerType.MsrFast] == 0)
                    {
                        Monitor.Exit(cpuLock);
                        return;
                    }

                    int TDPslow = (int)StoredTDP[(int)PowerType.Slow];
                    int TDPfast = (int)StoredTDP[(int)PowerType.Fast];

                    // only request an update if current limit is different than stored
                    if (CurrentTDP[(int)PowerType.MsrSlow] != TDPslow ||
                        CurrentTDP[(int)PowerType.MsrFast] != TDPfast)
                        ((IntelProcessor)processor).SetMSRLimit(TDPslow, TDPfast);
                    else
                        MSRdone = true;
                }

                // user requested to halt cpu watchdog
                if (TDPdone && MSRdone && cpuWatchdogPendingStop)
                    cpuWatchdog.Stop();

                Monitor.Exit(cpuLock);
            }
        }

        private void gfxWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (processor is null || !processor.IsInitialized)
                return;

            if (Monitor.TryEnter(gfxLock))
            {
                bool GPUdone = false;

                if (processor.GetType() == typeof(AMDProcessor))
                {
                    // not ready yet
                    if (CurrentGfxClock == 0)
                    {
                        Monitor.Exit(gfxLock);
                        return;
                    }
                }
                else if (processor.GetType() == typeof(IntelProcessor))
                {
                    // not ready yet
                    if (CurrentGfxClock == 0)
                    {
                        Monitor.Exit(gfxLock);
                        return;
                    }
                }

                // not ready yet
                if (StoredGfxClock == 0)
                {
                    Monitor.Exit(gfxLock);
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
                    GPUdone = true;

                // user requested to halt gpu watchdog
                if (GPUdone && gfxWatchdogPendingStop)
                    gfxWatchdog.Stop();

                Monitor.Exit(gfxLock);
            }
        }

        internal void StartGPUWatchdog()
        {
            gfxWatchdogPendingStop = false;
            gfxWatchdog.Start();
        }

        internal void StopGPUWatchdog()
        {
            gfxWatchdogPendingStop = true;
        }

        internal void StopTDPWatchdog()
        {
            cpuWatchdogPendingStop = true;
        }

        internal void StartTDPWatchdog()
        {
            cpuWatchdogPendingStop = false;
            cpuWatchdog.Start();
        }

        public void RequestTDP(PowerType type, double value, bool immediate = false)
        {
            int idx = (int)type;

            // update value read by timer
            StoredTDP[idx] = value;

            // immediately apply
            if (immediate)
                processor.SetTDPLimit((PowerType)idx, value);
        }

        public void RequestTDP(double[] values, bool immediate = false)
        {
            for (int idx = (int)PowerType.Slow; idx <= (int)PowerType.Fast; idx++)
            {
                // update value read by timer
                StoredTDP[idx] = values[idx];

                // immediately apply
                if (immediate)
                    processor.SetTDPLimit((PowerType)idx, values[idx]);
            }
        }

        public void RequestGPUClock(double value, bool immediate = false)
        {
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

        public void RequestPerfBoostMode(bool value)
        {
            currentPerfBoostMode = value;
            WritePowerCfg(PowerSubGroup.SUB_PROCESSOR, PowerSetting.PERFBOOSTMODE, value ? (uint)PerfBoostMode.Aggressive : (uint)PerfBoostMode.Disabled);
            LogManager.LogInformation("User requested perfboostmode: {0}", value);
        }

        private uint[] ReadPowerCfg(Guid SubGroup, Guid Settings)
        {
            uint[] results = new uint[2];

            if (PowerProfile.GetActiveScheme(out Guid currentScheme))
            {
                // read AC/DC values
                PowerProfile.GetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings, out results[(int)PowerIndexType.AC]);
                PowerProfile.GetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings, out results[(int)PowerIndexType.DC]);
            }

            return results;
        }

        private void WritePowerCfg(Guid SubGroup, Guid Settings, uint Value)
        {
            if (PowerProfile.GetActiveScheme(out Guid currentScheme))
            {
                PowerProfile.SetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings, Value);
                PowerProfile.SetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings, Value);
                PowerProfile.SetActiveScheme(currentScheme);
            }
        }

        #region events
        private void HWiNFO_PowerLimitChanged(PowerType type, int limit)
        {
            int idx = (int)type;
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

            LogManager.LogDebug("PowerLimitChanged: {0}\t{1} W", type, limit);
        }

        private void HWiNFO_GPUFrequencyChanged(double value)
        {
            CurrentGfxClock = value;

            LogManager.LogDebug("GPUFrequencyChanged: {0} Mhz", value);
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
            int idx = (int)type;
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

        public override void Start()
        {
            // initialize watchdog(s)
            powerWatchdog.Start();

            // initialize processor
            processor = Processor.GetCurrent();

            // higher interval on Intel CPUs to avoid CPU overload
            if (processor.GetType() == typeof(IntelProcessor))
            {
                // read OS specific values
                bool HypervisorEnforcedCodeIntegrityEnabled = RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
                bool VulnerableDriverBlocklistEnable = RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable");

                if (VulnerableDriverBlocklistEnable || HypervisorEnforcedCodeIntegrityEnabled)
                    LogManager.LogWarning("Core isolation settings are turned on. TDP read/write is disabled");
            }

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
            AutoTDPWatchdog.Stop();

            base.Stop();
        }
    }
}