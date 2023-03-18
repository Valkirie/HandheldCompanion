using PrecisionTiming;
using System;

namespace ControllerCommon.Managers
{
    public static class TimerManager
    {
        private static PrecisionTimer MasterTimer;
        private const int MasterInterval = 10;
        private static long TickCount;

        public static event TickEventHandler Tick;
        public delegate void TickEventHandler(long ticks);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        public static bool IsInitialized;

        static TimerManager()
        {
            MasterTimer = new PrecisionTimer();
            MasterTimer.SetAutoResetMode(true);
            MasterTimer.SetResolution(0);
            MasterTimer.SetPeriod(MasterInterval);
            MasterTimer.Tick += MasterTimerTicked;
        }

        private static void MasterTimerTicked(object sender, EventArgs e)
        {
            TickCount++;
            Tick?.Invoke(TickCount);
        }

        public static int GetPeriod()
        {
            return MasterTimer.GetPeriod();
        }

        public static int GetResolution()
        {
            return MasterTimer.GetResolution();
        }

        public static float GetPeriodMilliseconds()
        {
            return (float)MasterTimer.GetPeriod() / 1000L;
        }

        public static long GetTickCount()
        {
            return TickCount;
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            MasterTimer.Start();

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started with Period set to {1} and Resolution set to {2}", "TimerManager", GetPeriod(), GetResolution());
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            MasterTimer.Stop();

            LogManager.LogInformation("{0} has stopped", "TimerManager");
        }

        public static void Restart()
        {
            Stop();
            Start();
        }
    }
}
