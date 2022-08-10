using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Timers;

namespace HandheldCompanion.Managers
{
    public static class PowerMode
    {
        /// <summary>
        /// Better Battery mode.
        /// </summary>
        public static Guid BetterBattery = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");

        /// <summary>
        /// Better Performance mode.
        /// </summary>
        // public static Guid BetterPerformance = new Guid("3af9B8d9-7c97-431d-ad78-34a8bfea439f");
        public static Guid BetterPerformance = new Guid("00000000-0000-0000-0000-000000000000");

        /// <summary>
        /// Best Performance mode.
        /// </summary>
        public static Guid BestPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

        public static List<Guid> PowerModes = new() { BetterBattery, BetterPerformance, BestPerformance };
    }

    public class PowerManager
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

        private Processor processor;

        // timers
        private Timer updateTimer;
        private Timer cpuTimer;
        private Timer gpuTimer;

        public event LimitChangedHandler PowerLimitChanged;
        public delegate void LimitChangedHandler(PowerType type, int limit);

        public event ValueChangedHandler PowerValueChanged;
        public delegate void ValueChangedHandler(PowerType type, float value);

        public event StatusChangedHandler ProcessorStatusChanged;
        public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

        // user requested limits
        private double[] UserRequestedTDP = new double[3];
        private double UserRequestedGPUClock;
        private Guid UserRequestedPowerMode;

        // values
        private double[] TDPvalue = new double[3];
        private double GPUvalue;

        public PowerManager()
        {
            // initialize timer(s)
            updateTimer = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            updateTimer.Elapsed += UpdateTimer_Elapsed;

            cpuTimer = new Timer() { Interval = 3000, AutoReset = false, Enabled = false };
            cpuTimer.Elapsed += cpuTimer_Elapsed;

            gpuTimer = new Timer() { Interval = 3000, AutoReset = false, Enabled = false };
            gpuTimer.Elapsed += gpuTimer_Elapsed;

            // initialize processor
            processor = Processor.GetCurrent();
            processor.ValueChanged += Processor_ValueChanged;
            processor.StatusChanged += Processor_StatusChanged;
            processor.LimitChanged += Processor_LimitChanged;

            MainWindow.profileManager.Applied += ProfileManager_Applied;
            MainWindow.profileManager.Updated += ProfileManager_Updated;
            MainWindow.profileManager.Discarded += ProfileManager_Discarded;

            // initialize settings
            var TDPdown = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled ? Properties.Settings.Default.QuickToolsPerformanceTDPSustainedValue : 0;
            var TDPup = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled ? Properties.Settings.Default.QuickToolsPerformanceTDPBoostValue : 0;

            TDPdown = TDPdown != 0 ? TDPdown : MainWindow.handheldDevice.nTDP[0];
            TDPup = TDPup != 0 ? TDPup : MainWindow.handheldDevice.nTDP[1];

            UserRequestedTDP[0] = TDPdown;  // slow
            UserRequestedTDP[1] = TDPdown;  // stapm
            UserRequestedTDP[2] = TDPup;    // fast

            var GPU = Properties.Settings.Default.QuickToolsPerformanceGPUEnabled ? Properties.Settings.Default.QuickToolsPerformanceGPUValue : 0;
            if (GPU != 0)
                RequestGPUClock(GPU, true);
        }

        private void ProfileManager_Updated(Profile profile, bool backgroundtask, bool isCurrent)
        {
            if (!isCurrent)
                return;

            ProfileManager_Applied(profile);
        }

        private void ProfileManager_Discarded(Profile profile, bool isCurrent)
        {
            if (!isCurrent)
                return;

            // restore user defined TDP
            RequestTDP(UserRequestedTDP);
        }

        private void ProfileManager_Applied(Profile profile)
        {
            // apply profile defined TDP
            if (profile.TDP_override && profile.TDP_value != null)
                RequestTDP(profile.TDP_value, false);
            else
                RequestTDP(UserRequestedTDP);
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Checking if active power shceme has changed
            if (PowerGetEffectiveOverlayScheme(out Guid activeScheme) == 0)
                if (activeScheme != UserRequestedPowerMode)
                    PowerSetActiveOverlayScheme(UserRequestedPowerMode);
        }

        private void cpuTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
            {
                int idx = (int)type;
                if (TDPvalue[idx] != 0)
                    processor.SetTDPLimit(type, TDPvalue[idx]);
            }
        }

        private void gpuTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            processor.SetGPUClock(GPUvalue);
        }

        public void RestoreTDP()
        {
            // we use a timer to prevent too many calls from happening
            cpuTimer.Stop();
            cpuTimer.Start();
        }

        public void RequestTDP(PowerType type, double value, bool UserRequested = true)
        {
            int idx = (int)type;

            if (UserRequested)
                UserRequestedTDP[idx] = value;

            // update value read by timer
            TDPvalue[idx] = value;

            // we use a timer to prevent too many calls from happening
            cpuTimer.Stop();
            cpuTimer.Start();
        }

        public void RequestTDP(double[] values, bool UserRequested = true)
        {
            if (UserRequested)
                UserRequestedTDP = values;

            // update value read by timer
            TDPvalue = values;

            // we use a timer to prevent too many calls from happening
            cpuTimer.Stop();
            cpuTimer.Start();
        }

        public void RestoreGPUClock()
        {
            // we use a timer to prevent too many calls from happening
            gpuTimer.Stop();
            gpuTimer.Start();
        }

        public void RequestGPUClock(double value, bool UserRequested = true)
        {
            if (UserRequested)
                UserRequestedGPUClock = value;

            // update value read by timer
            GPUvalue = value;

            // we use a timer to prevent too many calls from happening
            gpuTimer.Stop();
            gpuTimer.Start();
        }

        public void RequestPowerMode(int idx)
        {
            UserRequestedPowerMode = PowerMode.PowerModes[idx];
            LogManager.LogInformation("User requested power scheme: {0}", UserRequestedPowerMode);

            PowerSetActiveOverlayScheme(UserRequestedPowerMode);
        }

        #region events
        private void Processor_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            ProcessorStatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);
        }

        private void Processor_ValueChanged(PowerType type, float value)
        {
            PowerValueChanged?.Invoke(type, value);
        }

        private void Processor_LimitChanged(PowerType type, int limit)
        {
            int idx = (int)type;
            double TDP = UserRequestedTDP[idx];

            Profile CurrentProfile = MainWindow.profileManager.currentProfile;
            if (CurrentProfile != null && CurrentProfile.TDP_override && CurrentProfile.TDP_value != null)
                TDP = CurrentProfile.TDP_value[idx];

            if (processor.GetType() == typeof(AMDProcessor))
                if (UserRequestedPowerMode == PowerMode.BetterBattery)
                    TDP = (int)Math.Truncate(UserRequestedTDP[idx] * 0.9);

            // only request an update if reported limit is different to expected value
            if (limit != TDP)
                RequestTDP(type, TDP, false);

            // raise event
            PowerLimitChanged?.Invoke(type, limit);
        }
        #endregion

        internal void Start()
        {
            processor.Initialize();
            updateTimer.Start();
        }

        internal void Stop()
        {
            processor.Stop();
            updateTimer.Stop();
        }
    }
}
