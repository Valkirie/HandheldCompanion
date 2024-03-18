using PrecisionTiming;
using System;
using System.Diagnostics;

namespace HandheldCompanion.Managers;

public static class TimerManager
{
    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    public static event TickEventHandler Tick;
    public delegate void TickEventHandler(long ticks, float delta);

    private const int MasterInterval = 10; // 100Hz
    private static readonly PrecisionTimer MasterTimer;
    public static Stopwatch Stopwatch;

    private static float PreviousTotalMilliseconds;
    private static float DeltaSeconds;

    public static bool IsInitialized;

    static TimerManager()
    {
        MasterTimer = new PrecisionTimer();
        MasterTimer.SetInterval(new Action(DoWork), MasterInterval, false, 0, TimerMode.Periodic, true);

        Stopwatch = new Stopwatch();
    }

    private static void DoWork()
    {
        // update timestamp
        float TotalMilliseconds = (float)Stopwatch.Elapsed.TotalMilliseconds;
        DeltaSeconds = (TotalMilliseconds - PreviousTotalMilliseconds) / 1000.0f;
        PreviousTotalMilliseconds = TotalMilliseconds;

        Tick?.Invoke(Stopwatch.ElapsedTicks, DeltaSeconds);
    }

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