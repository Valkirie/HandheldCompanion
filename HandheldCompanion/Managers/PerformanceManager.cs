using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
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

        private Processor processor;
        public static int MaxDegreeOfParallelism = 4;

        private static readonly Guid[] PowerModes = new Guid[3] { PowerMode.BetterBattery, PowerMode.BetterPerformance, PowerMode.BestPerformance };
        private Guid currentPowerMode = new("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
        private readonly Timer powerWatchdog;

        private Timer cpuWatchdog;
        protected object cpuLock = new();
        private bool cpuWatchdogPendingStop;

        private Timer gfxWatchdog;
        protected object gfxLock = new();
        private bool gfxWatchdogPendingStop;

        private Timer AutoTDPWatchdog;
        protected object AutoTDPWatchdogLock = new();

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

        private bool AutoTDPEnabled;
        private double AutoTDP;
        private double AutoTDPTargetFPS;
        private double AutoTDPMin;
        private double AutoTDPMax;
        private int AutoTDPFPSSetpointMetCounter;
        private double ProcessValueFPSPrevious;
		
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
            ProfileManager.Updated += ProfileManager_Updated;
            ProfileManager.Discarded += ProfileManager_Discarded;

            PlatformManager.HWiNFO.PowerLimitChanged += HWiNFO_PowerLimitChanged;
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

                case "QuickToolsPowerModeValue":
                    int power = Convert.ToInt32(value);
                    RequestPowerMode(power);
                    break;
            }
        }

        private void ProfileManager_Updated(Profile profile, ProfileUpdateSource source, bool isCurrent)
        {
            if (!isCurrent)
                return;

            ProfileManager_Applied(profile);
        }

        private void ProfileManager_Discarded(Profile profile, bool isCurrent, bool isUpdate)
        {
            // skip if part of a profile swap
            if (isUpdate)
                return;

            // restore default TDP and halt watchdog
            if (profile.TDPOverrideEnabled || profile.AutoTDPEnabled)
            {
                RequestTDP(MainWindow.CurrentDevice.nTDP);
                StopTDPWatchdog();
            }

            // restore default GPU and halt watchdog
            if (profile.GPUOverrideEnabled)
            {
                RequestGPUClock(255 * 50);
                StopGPUWatchdog();
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
                AutoTDPEnabled = true;
            }
            else
            {
                AutoTDPEnabled = false;
            }
        }

        private void RTSS_Hooked(int processId)
        {
            AutoTDPProcessId = processId;
            AutoTDPWatchdog.Start();
        }

        private void RTSS_Unhooked(int processId)
        {
            AutoTDPProcessId = 0;
            AutoTDPWatchdog.Stop();
        }

        private void AutoTDPWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(AutoTDPWatchdogLock))
            {
                // Auto TDP
                if (AutoTDPEnabled)
                {
                    // todo: we need to store fps somewhere if we are about to gather the data from more than a single point (OSD, Performance)
                    double ProcessValueFPS = PlatformManager.RTSS.GetFramerate(AutoTDPProcessId);

                    if (ProcessValueFPS != 0)
                    {
                        // Be realistic with expectd proces value
                        ProcessValueFPS = Math.Clamp(ProcessValueFPS, 5, 500);

                        // If actual and target FPS are very similar for a certain duration,
                        // add a small amount of positive "error" make controller always try to reduce
                        // range is intentionally a bit wide for framerate limiter margin of error
                        double ProcessValueFPSModifier = 0.0;
                        if (AutoTDPTargetFPS - 0.5 <= ProcessValueFPS && ProcessValueFPS <= AutoTDPTargetFPS + 0.1)
                        {
                            AutoTDPFPSSetpointMetCounter += 1;
                            
                            if (AutoTDPFPSSetpointMetCounter >= 3)
                            { 
                                // Calculate modifier to get target + 0.5 controller error
                                ProcessValueFPSModifier = AutoTDPTargetFPS + 0.5 - ProcessValueFPS;
                            }
                            else if (AutoTDPFPSSetpointMetCounter >= 6)
                            {
                                // Calculate modifier to get target + 1.5 controller error
                                ProcessValueFPSModifier = AutoTDPTargetFPS + 1.5 - ProcessValueFPS;

                                // Prevent overflow
                                AutoTDPFPSSetpointMetCounter = 6;
                            }
                        }    
                        else 
                        { 
                            ProcessValueFPSModifier = 0.0;
                            AutoTDPFPSSetpointMetCounter = 0;
                        }

                        // Determine error amount
                        double ControllerError = AutoTDPTargetFPS - ProcessValueFPS - ProcessValueFPSModifier;

                        // Clamp error amount that is corrected within a single cycle
                        // Adjust clamp in case of actual FPS being 2.5x requested FPS, for example, menu's going to 300+ fps.
                        double ClampLowerLimit = ProcessValueFPS >= 2.5 * AutoTDPTargetFPS ? -100 : -5;
                        // -5 +15, going lower always overshoots (not safe, leads to instability), going higher always undershoots (which is safe)
                        ControllerError = Math.Clamp(ControllerError, ClampLowerLimit, 15);

                        // Todo, use TDP from profile or some average device range value for the initial setpoint to allow continuation from last time?

                        // Based on TDP/FPS ratio, determine how much adjustment is needed
                        double TDPAdjustment = ControllerError * AutoTDP / ProcessValueFPS;
                        // Always have a little bit of undershoot
                        TDPAdjustment *= 0.9;

                        // (PI)D derivate control component to dampen
                        if (ProcessValueFPSPrevious == float.NaN) { ProcessValueFPSPrevious = ProcessValueFPS; } // First time around, initialise previous
                        double DFactor = -0.25;
                        double DeltaError = ProcessValueFPS - ProcessValueFPSPrevious;
                        double DTerm = DeltaError / ((double)INTERVAL_AUTO / 1000.0); // Perhaps improve with actual timer?
                        double TDPDamping = AutoTDP / ProcessValueFPS * DFactor * DTerm;
                        ProcessValueFPSPrevious = ProcessValueFPS; // For next loop

                        // Determine final setpoint
                        // Skip calculating TDP the very first run, first need to set to determine values next round
                        if (!AutoTDPFirstRun)
                        {
                            AutoTDP += TDPAdjustment + TDPDamping;
                        }
                        else { AutoTDPFirstRun = false; }

                        // Prevent run away of TDP setpoint value
                        AutoTDP = Math.Clamp(AutoTDP, AutoTDPMin, AutoTDPMax);

                        var values = new double[3] { AutoTDP, AutoTDP, AutoTDP };
                        RequestTDP(values, true);

                        //LogManager.LogInformation("TDPSet;;;;;{0:0.0};{1:0.000};{2:0.0000};{3:0.0000};{4:0.0000}", AutoTDPTargetFPS, AutoTDP, TDPAdjustment, ProcessValueFPS, TDPDamping);
                    }
                }

                Monitor.Exit(AutoTDPWatchdogLock);
            }
        }

        private void powerWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Checking if active power shceme has changed to reflect that
            if (PowerGetEffectiveOverlayScheme(out Guid activeScheme) == 0)
            {
                if (activeScheme == currentPowerMode)
                    return;

                currentPowerMode = activeScheme;
                int idx = Array.IndexOf(PowerModes, activeScheme);
                if (idx != -1)
                    PowerModeChanged?.Invoke(idx);
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
                        if (RequestedPowerMode == PowerMode.BetterBattery)
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
                    processor.SetGPUClock(StoredGfxClock);
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
            for(int idx = (int)PowerType.Slow; idx <= (int)PowerType.Fast; idx++)
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