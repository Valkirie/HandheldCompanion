using HandheldCompanion.Shared;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.System.Power;

namespace HandheldCompanion.Managers;

public static class SystemManager
{
    #region PInvoke

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    #endregion

    #region Events

    public static event SystemStatusChangedEventHandler? SystemStatusChanged;
    public delegate void SystemStatusChangedEventHandler(SystemStatus status, SystemStatus prevStatus);

    public static event PowerStatusChangedEventHandler? PowerStatusChanged;
    public delegate void PowerStatusChangedEventHandler(PowerStatus status);

    public static event PowerLineStatusChangedEventHandler? PowerLineStatusChanged;
    public delegate void PowerLineStatusChangedEventHandler(PowerLineStatus powerLineStatus);

    public static event InitializedEventHandler? Initialized;
    public delegate void InitializedEventHandler();

    public static event SessionLockChangedEventHandler? SessionLockChanged;
    public delegate void SessionLockChangedEventHandler(bool isLocked);

    #endregion

    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint DESKTOP_SWITCHDESKTOP = 0x0100;

    public enum SystemStatus
    {
        SystemBooting = 0,
        SystemPending = 1,
        SystemReady = 2
    }

    private static bool isPowerSuspended;
    public static bool IsPowerSuspended => isPowerSuspended;
    public static bool IsSessionLocked = true;

    private static SystemStatus currentSystemStatus = SystemStatus.SystemBooting;
    private static SystemStatus previousSystemStatus = SystemStatus.SystemBooting;
    private static PowerLineStatus previousPowerLineStatus = PowerLineStatus.Offline;

    // Used to suppress SystemStatusChanged when Modern Standby auto-resleep is triggered
    private static bool _suppressNextSystemStatusChanged;

    public static bool IsInitialized;

    public static readonly SortedDictionary<string, string> PowerStatusIcon = new()
    {
        { "Battery0", "\uE850" },
        { "Battery1", "\uE851" },
        { "Battery2", "\uE852" },
        { "Battery3", "\uE853" },
        { "Battery4", "\uE854" },
        { "Battery5", "\uE855" },
        { "Battery6", "\uE856" },
        { "Battery7", "\uE857" },
        { "Battery8", "\uE858" },
        { "Battery9", "\uE859" },
        { "Battery10", "\uE83F" },

        { "BatteryCharging0", "\uE85A" },
        { "BatteryCharging1", "\uE85B" },
        { "BatteryCharging2", "\uE85C" },
        { "BatteryCharging3", "\uE85D" },
        { "BatteryCharging4", "\uE85E" },
        { "BatteryCharging5", "\uE85F" },
        { "BatteryCharging6", "\uE860" },
        { "BatteryCharging7", "\uE861" },
        { "BatteryCharging8", "\uE862" },
        { "BatteryCharging9", "\uE83E" },
        { "BatteryCharging10", "\uEA93" },

        { "BatterySaver0", "\uE863" },
        { "BatterySaver1", "\uE864" },
        { "BatterySaver2", "\uE865" },
        { "BatterySaver3", "\uE866" },
        { "BatterySaver4", "\uE867" },
        { "BatterySaver5", "\uE868" },
        { "BatterySaver6", "\uE869" },
        { "BatterySaver7", "\uE86A" },
        { "BatterySaver8", "\uE86B" },
        { "BatterySaver9", "\uEA94" },
        { "BatterySaver10", "\uEA95" }
    };

    static SystemManager() { }

    private static void SubscribeToSystemEvents()
    {
        // manage events
        SystemEvents.PowerModeChanged += OnPowerChange;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        PowerManager.BatteryStatusChanged += BatteryStatusChanged;
        PowerManager.EnergySaverStatusChanged += BatteryStatusChanged;
        PowerManager.PowerSupplyStatusChanged += BatteryStatusChanged;
        PowerManager.RemainingChargePercentChanged += BatteryStatusChanged;
        PowerManager.RemainingDischargeTimeChanged += BatteryStatusChanged;

        // raise events
        BatteryStatusChanged(null, null);
    }

    private static void UnsubscribeFromSystemEvents()
    {
        // manage events
        SystemEvents.PowerModeChanged -= OnPowerChange;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        PowerManager.BatteryStatusChanged -= BatteryStatusChanged;
        PowerManager.EnergySaverStatusChanged -= BatteryStatusChanged;
        PowerManager.PowerSupplyStatusChanged -= BatteryStatusChanged;
        PowerManager.RemainingChargePercentChanged -= BatteryStatusChanged;
        PowerManager.RemainingDischargeTimeChanged -= BatteryStatusChanged;
    }

    private static void BatteryStatusChanged(object? sender, object? e)
    {
        PowerStatusChanged?.Invoke(SystemInformation.PowerStatus);
    }

    public static bool IsSessionInteractive()
    {
        IntPtr inputDesktop = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
        if (inputDesktop == IntPtr.Zero)
            return false;

        try
        {
            return true;
        }
        finally
        {
            CloseDesktop(inputDesktop);
        }
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        // listen to system events
        SubscribeToSystemEvents();

        // Check if current session is locked
        IsSessionLocked = !IsSessionInteractive();

        PerformSystemRoutine();

        IsInitialized = true;
        Initialized?.Invoke();

        PowerStatusChanged?.Invoke(SystemInformation.PowerStatus);

        LogManager.LogInformation("{0} has started", "PowerManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        // stop listening to system events
        UnsubscribeFromSystemEvents();

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "PowerManager");
    }

    /// <summary>
    /// Suppresses the next SystemStatusChanged event. Used when Modern Standby auto-resleep is triggered
    /// to prevent managers from unnecessarily starting when the system immediately goes back to sleep.
    /// </summary>
    public static void SuppressNextSystemStatusChanged()
    {
        _suppressNextSystemStatusChanged = true;
    }

    private static void OnPowerChange(object s, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Resume:
                isPowerSuspended = false;
                break;

            case PowerModes.Suspend:
                isPowerSuspended = true;
                break;

            default:
            case PowerModes.StatusChange:
                {
                    if (previousPowerLineStatus != SystemInformation.PowerStatus.PowerLineStatus)
                    {
                        // raise event
                        PowerLineStatusChanged?.Invoke(SystemInformation.PowerStatus.PowerLineStatus);

                        // update status
                        previousPowerLineStatus = SystemInformation.PowerStatus.PowerLineStatus;
                    }
                }
                return;
        }

        LogManager.LogDebug("Device power mode set to {0}", e.Mode);

        PerformSystemRoutine();
    }

    private static void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionUnlock:
                IsSessionLocked = false;
                break;
            case SessionSwitchReason.SessionLock:
                IsSessionLocked = true;
                break;
        }

        LogManager.LogDebug("Session switched to {0}", e.Reason);

        SessionLockChanged?.Invoke(IsSessionLocked);
    }

    private static void PerformSystemRoutine()
    {
        // update status
        currentSystemStatus = isPowerSuspended ? SystemStatus.SystemPending : SystemStatus.SystemReady;

        // only raise event is system status has changed
        if (previousSystemStatus == currentSystemStatus)
            return;

        // Check if we should suppress this event (Modern Standby auto-resleep scenario)
        if (_suppressNextSystemStatusChanged)
        {
            _suppressNextSystemStatusChanged = false;
            LogManager.LogInformation("System status set to {0} (event suppressed for auto-resleep)", currentSystemStatus);
            previousSystemStatus = currentSystemStatus;
            return;
        }

        LogManager.LogInformation("System status set to {0}", currentSystemStatus);
        SystemStatusChanged?.Invoke(currentSystemStatus, previousSystemStatus);

        // update status
        previousSystemStatus = currentSystemStatus;
    }
}