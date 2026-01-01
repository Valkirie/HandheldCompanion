using HandheldCompanion.Shared;
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

    private static int MasterInterval = 8; // 125Hz
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
        MasterTimer.SetInterval(new Action(DoWork), MasterInterval, false, 0, TimerMode.Periodic, true);
        MasterTimer.Start();

        Stopwatch.Start();

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started with Period set to {1}", "TimerManager", GetPeriod());
    }

    private static void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("MasterInterval", ManagerFactory.settingsManager.GetString("MasterInterval"), false);
    }

    private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "MasterInterval":
                int MasterIntervalIdx = Convert.ToInt32(value);
                switch (MasterIntervalIdx)
                {
                    default:
                    case 0: // 125 Hz
                        MasterInterval = 8;
                        break;
                    case 1: // 250 Hz
                        MasterInterval = 4;
                        break;
                    case 2: // 500 Hz
                        MasterInterval = 2;
                        break;
                    case 3: // 1000 Hz
                        MasterInterval = 1;
                        break;
                }

                MasterTimer.SetInterval(new Action(DoWork), MasterInterval, false, 0, TimerMode.Periodic, true);
                break;
        }
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