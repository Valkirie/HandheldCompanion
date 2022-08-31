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

    public class PowerManager : Manager
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
        private Timer powerWatchdog;

        private Timer cpuWatchdog;
        protected object cpuLock = new();

        private Timer gfxWatchdog;
        protected object gfxLock = new();

        public event LimitChangedHandler PowerLimitChanged;
        public delegate void LimitChangedHandler(PowerType type, int limit);

        public event ValueChangedHandler PowerValueChanged;
        public delegate void ValueChangedHandler(PowerType type, float value);

        public event StatusChangedHandler ProcessorStatusChanged;
        public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

        // TDP limits
        private double[] FallbackTDP = new double[3];   // used to store fallback TDP
        private double[] StoredTDP = new double[3];     // used to store TDP
        private double[] CurrentTDP = new double[5];    // used to store current TDP

        // GPU limits
        private double FallbackGfxClock;
        private double StoredGfxClock;
        private double CurrentGfxClock;

        // Power modes
        private Guid RequestedPowerMode;

        public PowerManager() : base()
        {
            // initialize timer(s)
            powerWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            powerWatchdog.Elapsed += powerWatchdog_Elapsed;

            cpuWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            cpuWatchdog.Elapsed += cpuWatchdog_Elapsed;

            gfxWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            gfxWatchdog.Elapsed += gfxWatchdog_Elapsed;

            MainWindow.profileManager.Applied += ProfileManager_Applied;
            MainWindow.profileManager.Updated += ProfileManager_Updated;
            MainWindow.profileManager.Discarded += ProfileManager_Discarded;

            // initialize settings
            double TDPdown = Convert.ToDouble(MainWindow.settingsManager.GetProperty("QuickToolsPerformanceTDPSustainedValue"));
            TDPdown = TDPdown != 0 ? TDPdown : MainWindow.handheldDevice.nTDP[(int)PowerType.Slow];

            double TDPup = Convert.ToDouble(MainWindow.settingsManager.GetProperty("QuickToolsPerformanceTDPBoostValue"));
            TDPup = TDPup != 0 ? TDPup : MainWindow.handheldDevice.nTDP[(int)PowerType.Fast];

            double GPU = Convert.ToDouble(MainWindow.settingsManager.GetProperty("QuickToolsPerformanceGPUValue"));

            // request TDP(s)
            RequestTDP(PowerType.Slow, TDPdown);
            RequestTDP(PowerType.Stapm, TDPdown);
            RequestTDP(PowerType.Fast, TDPup);

            // request GPUclock
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
            RequestTDP(FallbackTDP, false);
        }

        private void ProfileManager_Applied(Profile profile)
        {
            // apply profile defined TDP
            if (profile.TDP_override && profile.TDP_value != null)
                RequestTDP(profile.TDP_value, false);
            else
                RequestTDP(FallbackTDP, false); // redudant with ProfileManager_Discarded ?
        }

        private void powerWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Checking if active power shceme has changed
            if (PowerGetEffectiveOverlayScheme(out Guid activeScheme) == 0)
                if (activeScheme != RequestedPowerMode)
                    PowerSetActiveOverlayScheme(RequestedPowerMode);
        }

        private void cpuWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            lock (cpuLock)
            {
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

                    // not ready yet
                    if (CurrentTDP[idx] == 0)
                        break;

                    // only request an update if current limit is different than stored
                    if (CurrentTDP[idx] != TDP)
                        processor.SetTDPLimit(type, TDP);
                }

                // processor specific
                if (processor.GetType() == typeof(IntelProcessor))
                {
                    // not ready yet
                    if (CurrentTDP[(int)PowerType.MsrSlow] == 0 || CurrentTDP[(int)PowerType.MsrFast] == 0)
                        return;

                    int TDPslow = (int)StoredTDP[(int)PowerType.Slow];
                    int TDPfast = (int)StoredTDP[(int)PowerType.Fast];

                    // only request an update if current limit is different than stored
                    if (CurrentTDP[(int)PowerType.MsrSlow] != TDPslow ||
                        CurrentTDP[(int)PowerType.MsrFast] != TDPfast)
                        ((IntelProcessor)processor).SetMSRLimit(TDPslow, TDPfast);
                }
            }
        }

        private void gfxWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            lock (gfxLock)
            {
                if (processor.GetType() == typeof(AMDProcessor))
                {
                    // not ready yet
                    if (CurrentGfxClock == 0)
                        return;
                }
                else if (processor.GetType() == typeof(IntelProcessor))
                {
                    // not ready yet
                    if (CurrentGfxClock == 12750)
                        return;
                }

                // not ready yet
                if (StoredGfxClock == 0)
                    return;

                // only request an update if current gfx clock is different than stored
                if (CurrentGfxClock != StoredGfxClock)
                    processor.SetGPUClock(StoredGfxClock);
            }
        }

        public void RequestTDP(PowerType type, double value, bool UserRequested = true)
        {
            int idx = (int)type;

            if (UserRequested)
                FallbackTDP[idx] = value;

            // update value read by timer
            StoredTDP[idx] = value;
        }

        public void RequestTDP(double[] values, bool UserRequested = true)
        {
            if (UserRequested)
                FallbackTDP = values;

            // update value read by timer
            StoredTDP = values;
        }

        public void RequestGPUClock(double value, bool UserRequested = true)
        {
            if (UserRequested)
                FallbackGfxClock = value;

            // update value read by timer
            StoredGfxClock = value;
        }

        public void RequestPowerMode(int idx)
        {
            RequestedPowerMode = PowerMode.PowerModes[idx];
            LogManager.LogInformation("User requested power scheme: {0}", RequestedPowerMode);

            PowerSetActiveOverlayScheme(RequestedPowerMode);
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
            CurrentTDP[idx] = limit;

            // raise event
            PowerLimitChanged?.Invoke(type, limit);
        }

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
            cpuWatchdog.Start();
            gfxWatchdog.Start();

            // initialize processor
            processor = Processor.GetCurrent();
            processor.ValueChanged += Processor_ValueChanged;
            processor.StatusChanged += Processor_StatusChanged;
            processor.LimitChanged += Processor_LimitChanged;
            processor.MiscChanged += Processor_MiscChanged;
            processor.Initialize();

            powerWatchdog.Start();

            base.Start();
        }

        public override void Stop()
        {
            processor.Stop();
            powerWatchdog.Stop();

            base.Stop();
        }
    }
}
