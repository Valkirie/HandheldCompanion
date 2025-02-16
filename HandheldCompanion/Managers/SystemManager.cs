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

    #endregion

    #region Events

    public static event SystemStatusChangedEventHandler SystemStatusChanged;
    public delegate void SystemStatusChangedEventHandler(SystemStatus status, SystemStatus prevStatus);

    public static event PowerStatusChangedEventHandler PowerStatusChanged;
    public delegate void PowerStatusChangedEventHandler(PowerStatus status);

    public static event PowerLineStatusChangedEventHandler PowerLineStatusChanged;
    public delegate void PowerLineStatusChangedEventHandler(PowerLineStatus powerLineStatus);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion

    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;

    public enum SystemStatus
    {
        SystemBooting = 0,
        SystemPending = 1,
        SystemReady = 2
    }

    private static bool IsPowerSuspended;
    private static bool IsSessionLocked = true;

    private static SystemStatus currentSystemStatus = SystemStatus.SystemBooting;
    private static SystemStatus previousSystemStatus = SystemStatus.SystemBooting;
    private static PowerLineStatus previousPowerLineStatus = PowerLineStatus.Offline;

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

    private static void BatteryStatusChanged(object sender, object e)
    {
        PowerStatusChanged?.Invoke(SystemInformation.PowerStatus);
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        // listen to system events
        SubscribeToSystemEvents();

        // Check if current session is locked
        IsSessionLocked = OpenInputDesktop(0, false, 0) == IntPtr.Zero;

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

    private static void OnPowerChange(object s, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Resume:
                IsPowerSuspended = false;
                break;

            case PowerModes.Suspend:
                IsPowerSuspended = true;
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
            default:
                return;
        }

        LogManager.LogDebug("Session switched to {0}", e.Reason);

        PerformSystemRoutine();
    }

    private static void PerformSystemRoutine()
    {
        if (!IsPowerSuspended && !IsSessionLocked)
            currentSystemStatus = SystemStatus.SystemReady;
        else
            currentSystemStatus = SystemStatus.SystemPending;

        // only raise event is system status has changed
        if (previousSystemStatus == currentSystemStatus)
            return;

        LogManager.LogInformation("System status set to {0}", currentSystemStatus);
        SystemStatusChanged?.Invoke(currentSystemStatus, previousSystemStatus);

        previousSystemStatus = currentSystemStatus;
    }
}