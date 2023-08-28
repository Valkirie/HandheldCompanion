using PrecisionTiming;
using System;
using System.Diagnostics;

namespace HandheldCompanion.Managers;

public static class TimerManager
{
    public delegate void InitializedEventHandler();

    public delegate void TickEventHandler(long ticks);

    private const int MasterInterval = 10; // 100Hz
    private static readonly PrecisionTimer MasterTimer;
    public static Stopwatch Stopwatch;

    public static bool IsInitialized;

    static TimerManager()
    {
        MasterTimer = new PrecisionTimer();
        MasterTimer.SetInterval(new Action(DoWork), MasterInterval, false, 0, TimerMode.Periodic, true);

        Stopwatch = new Stopwatch();
    }

    private static void DoWork()
    {
        // if (Stopwatch.ElapsedTicks % MasterInterval == 0)
        Tick?.Invoke(Stopwatch.ElapsedTicks);
    }

    public static event TickEventHandler Tick;

    public static event InitializedEventHandler Initialized;

    public static int GetPeriod()
    {
        return MasterInterval;
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

        LogManager.LogInformation("{0} has started with Period set to {1}", "TimerManager", GetPeriod());
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