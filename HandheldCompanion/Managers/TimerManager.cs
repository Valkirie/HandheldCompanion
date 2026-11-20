using HandheldCompanion.Shared;
using PrecisionTiming;
using System;
using System.Diagnostics;

namespace HandheldCompanion.Managers;

public static class TimerManager
{
    private const int DefaultMasterInterval = 8; //ms => 125Hz

    public static event InitializedEventHandler? Initialized;
    public delegate void InitializedEventHandler();

    public static event TickEventHandler? Tick;
    public delegate void TickEventHandler(long ticks, float delta);

    private static int MasterInterval = DefaultMasterInterval;
    private static int ConfiguredMasterInterval = DefaultMasterInterval;
    private static PrecisionTimer? MasterTimer;
    public static Stopwatch Stopwatch;

    private static long PreviousElapsedTicks;

    public static bool IsInitialized;

    static TimerManager()
    {
        Stopwatch = new Stopwatch();
#if DEBUG
        foreach (long uptimeSeconds in new long[] { 0, 86400, 604800 })
        {
            foreach (int intervalMs in new[] { 1, 2, 4, 8 })
            {
                long previousTicks = uptimeSeconds * System.Diagnostics.Stopwatch.Frequency;
                long nextTicks = previousTicks + intervalMs * System.Diagnostics.Stopwatch.Frequency / 1000;
                float delta = GetDelta(nextTicks, ref previousTicks);
                Debug.Assert(Math.Abs(delta - intervalMs / 1000f) < 0.0000001f);
                Debug.Assert(previousTicks == nextTicks);
                Debug.Assert(GetDelta(nextTicks, ref previousTicks) == 0f);
            }
        }
#endif
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        // raise events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        // (re)create timer
        MasterTimer = new PrecisionTimer();
        ConfigureMasterTimer();

        Stopwatch.Start();
        PreviousElapsedTicks = Stopwatch.ElapsedTicks;
        MasterTimer.Start();

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started with Period set to {1}", "TimerManager", GetPeriod());
    }

    private static void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        VirtualManager.MasterIntervalOverrideChanged += VirtualManager_MasterIntervalOverrideChanged;

        // raise events
        SettingsManager_SettingValueChanged("MasterInterval", ManagerFactory.settingsManager.GetString("MasterInterval"), false, true);
        ApplyEffectiveMasterInterval();
    }

    private static void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
    {
        switch (name)
        {
            case "MasterInterval":
                int MasterIntervalIdx = Convert.ToInt32(value);
                switch (MasterIntervalIdx)
                {
                    default:
                    case 0: // 125 Hz
                        ConfiguredMasterInterval = 8;
                        break;
                    case 1: // 250 Hz
                        ConfiguredMasterInterval = 4;
                        break;
                    case 2: // 500 Hz
                        ConfiguredMasterInterval = 2;
                        break;
                    case 3: // 1000 Hz
                        ConfiguredMasterInterval = 1;
                        break;
                }

                ApplyEffectiveMasterInterval();
                break;
        }
    }

    private static void VirtualManager_MasterIntervalOverrideChanged(int? overrideHz)
    {
        ApplyEffectiveMasterInterval();
    }

    private static void ApplyEffectiveMasterInterval()
    {
        MasterInterval = VirtualManager.GetMasterIntervalOverrideHz() switch
        {
            125 => 8,
            250 => 4,
            500 => 2,
            1000 => 1,
            _ => ConfiguredMasterInterval,
        };

        ConfigureMasterTimer();
    }

    private static void ConfigureMasterTimer()
    {
        if (MasterTimer is null)
            return;

        bool wasRunning = MasterTimer.IsRunning();
        if (wasRunning)
            MasterTimer.Stop();

        MasterTimer.SetInterval(new Action(DoWork), MasterInterval, false, GetTimerResolution(), TimerMode.Periodic, true);

        if (wasRunning)
            MasterTimer.Start();
    }

    private static int GetTimerResolution()
    {
        return Math.Max(1, MasterInterval);
    }

    private static void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        VirtualManager.MasterIntervalOverrideChanged -= VirtualManager_MasterIntervalOverrideChanged;

        MasterTimer?.Stop();
        MasterTimer?.Dispose();
        MasterTimer = null;

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
        return GetDelta(Stopwatch.ElapsedTicks, ref PreviousElapsedTicks);
    }

    private static float GetDelta(long elapsedTicks, ref long previousTicks)
    {
        long deltaTicks = elapsedTicks - previousTicks;
        previousTicks = elapsedTicks;

        return (float)((double)deltaTicks / System.Diagnostics.Stopwatch.Frequency);
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