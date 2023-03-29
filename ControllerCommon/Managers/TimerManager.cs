using PrecisionTiming;
using System;
using System.Diagnostics;
using System.Threading;

namespace ControllerCommon.Managers
{
    public static class TimerManager
    {
        private static PrecisionTimer MasterTimer;
        public static Stopwatch Stopwatch;

        private const int MasterInterval = 8;   // 125Hz

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

            Stopwatch = new Stopwatch();
        }

        private static void MasterTimerTicked(object sender, EventArgs e)
        {
            // if (Stopwatch.ElapsedTicks % MasterInterval == 0)
            Tick?.Invoke(Stopwatch.ElapsedTicks);
        }

        public static int GetPeriod()
        {
            return MasterInterval;
        }

        public static int GetResolution()
        {
            return MasterTimer.GetResolution();
        }

        public static float GetPeriodMilliseconds()
        {
            return (float)MasterInterval / 1000L;
        }

        public static long GetTickCount()
        {
            return Stopwatch.ElapsedTicks;
        }

        public static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        public static long GetElapsedSeconds()
        {
            return GetElapsedMilliseconds() * 1000L;
        }

        public static long GetElapsedMilliseconds()
        {
            return Stopwatch.ElapsedMilliseconds;
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            MasterTimer.Start();
            Stopwatch.Start();

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
            Stopwatch.Stop();

            LogManager.LogInformation("{0} has stopped", "TimerManager");
        }

        public static void Restart()
        {
            Stop();
            Start();
        }
    }
}
