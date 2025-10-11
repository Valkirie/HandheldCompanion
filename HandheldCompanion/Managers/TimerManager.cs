﻿using HandheldCompanion.Shared;
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
    private static PrecisionTimer MasterTimer;
    public static Stopwatch Stopwatch;

    private static float PreviousTotalMilliseconds;

    public static bool IsInitialized;

    static TimerManager()
    {
        Stopwatch = new Stopwatch();
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        // (re)create timer
        MasterTimer = new PrecisionTimer();
        MasterTimer.SetInterval(new Action(DoWork), MasterInterval, false, 0, TimerMode.Periodic, true);
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
        MasterTimer.Dispose();
        Stopwatch.Stop();

        LogManager.LogInformation("{0} has stopped", "TimerManager");
    }

    private static void DoWork()
    {
        // update timestamp
        float delta = GetDelta();
        Tick?.Invoke(Stopwatch.ElapsedTicks, delta);
    }

    public static float GetDelta()
    {
        float TotalMilliseconds = (float)Stopwatch.Elapsed.TotalMilliseconds;
        float delta = (TotalMilliseconds - PreviousTotalMilliseconds) / 1000.0f;
        PreviousTotalMilliseconds = TotalMilliseconds;

        return delta;
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

    public static long GetElapsedDeciseconds()
    {
        return GetElapsedMilliseconds() * 100L;
    }

    public static long GetElapsedMilliseconds()
    {
        return Stopwatch.ElapsedMilliseconds;
    }

    public static void Restart()
    {
        Stop();
        Start();
    }
}