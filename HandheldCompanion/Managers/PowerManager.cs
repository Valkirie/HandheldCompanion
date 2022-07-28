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

        public event LimitChangedHandler LimitChanged;
        public delegate void LimitChangedHandler(string type, int limit);

        public event ValueChangedHandler ValueChanged;
        public delegate void ValueChangedHandler(string type, float value);

        public event StatusChangedHandler StatusChanged;
        public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

        // user requested limits
        private double UserRequestedTDP = MainWindow.handheldDevice.DefaultTDP;
        private double UserRequestedGPUClock;
        private Guid UserRequestedPowerMode;

        // values
        private double TDPvalue;
        private double GPUvalue;

        public PowerManager()
        {
            // initialize processor
            processor = Processor.GetCurrent();
            processor.ValueChanged += Processor_ValueChanged;
            processor.StatusChanged += Processor_StatusChanged;
            processor.LimitChanged += Processor_LimitChanged;

            updateTimer = new Timer() { Interval = 4000, AutoReset = true, Enabled = false };
            updateTimer.Elapsed += UpdateTimer_Elapsed;

            cpuTimer = new Timer() { Interval = 3000, AutoReset = false, Enabled = false };
            cpuTimer.Elapsed += cpuTimer_Elapsed;

            gpuTimer = new Timer() { Interval = 3000, AutoReset = false, Enabled = false };
            gpuTimer.Elapsed += gpuTimer_Elapsed;

            MainWindow.profileManager.Applied += ProfileManager_Applied;
            MainWindow.profileManager.Discarded += ProfileManager_Discarded;
        }

        private void ProfileManager_Discarded(Profile profile)
        {
            // restore user defined TDP
            RequestTDP(UserRequestedTDP);
        }

        private void ProfileManager_Applied(Profile profile)
        {
            // apply profile defined TDP
            if (profile.TDP_override && profile.TDP_value != 0)
                RequestTDP(profile.TDP_value, false);
            else if (TDPvalue != UserRequestedTDP)
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
            processor.SetTDPLimit("all", TDPvalue);
            // processor.SetLimit("stapm", value);
            // processor.SetLimit("slow", value + 2);
            // processor.SetLimit("fast", value + 5);

            LogManager.LogInformation("User requested TDP: {0}", TDPvalue);
        }

        private void gpuTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            processor.SetGPUClock(GPUvalue);

            LogManager.LogInformation("User requested GPU clock: {0}", GPUvalue);
        }

        public void RestoreTDP()
        {
            // we use a timer to prevent too many calls from happening
            cpuTimer.Stop();
            cpuTimer.Start();
        }

        public void RequestTDP(double value, bool UserRequested = true)
        {
            if (UserRequested)
                UserRequestedTDP = value;

            // update value read by timer
            TDPvalue = value;

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
            StatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);
        }

        private void Processor_ValueChanged(string type, float value)
        {
            ValueChanged?.Invoke(type, value);
        }

        private void Processor_LimitChanged(string type, int limit)
        {
            double TDP = UserRequestedTDP;

            Profile CurrentProfile = MainWindow.profileManager.CurrentProfile;
            if (CurrentProfile != null && CurrentProfile.TDP_override && CurrentProfile.TDP_value != 0)
                TDP = CurrentProfile.TDP_value;

            if (processor.GetType() == typeof(AMDProcessor))
                if (UserRequestedPowerMode == PowerMode.BetterBattery)
                    TDP = (int)Math.Truncate(UserRequestedTDP * 0.9);

            switch (type)
            {
                default:
                case "slow":
                case "fast":
                    break;
                case "stapm":
                    if (limit != TDP)
                        RequestTDP(TDP, false);
                    break;
            }

            LimitChanged?.Invoke(type, limit);
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
