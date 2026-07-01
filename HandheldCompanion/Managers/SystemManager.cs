using HandheldCompanion.Shared;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;
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
    public delegate void PowerLineStatusChangedEventHandler(PowerLineStatus prevPowerLineStatus, PowerLineStatus powerLineStatus);

    public static event InitializedEventHandler? Initialized;
    public delegate void InitializedEventHandler();

    public static event SessionLockChangedEventHandler? SessionLockChanged;
    public delegate void SessionLockChangedEventHandler(bool isLocked);

    public static event PowerModeChangedEventHandler? PowerModeChanged;
    public delegate void PowerModeChangedEventHandler(PowerMode mode, WakeReason wakeReason);

    public enum PowerMode
    {
        Suspend = 0,
        Resume = 1
    }

    public enum WakeReason
    {
        Unknown = 0,
        PowerButton = 1,
        FingerprintReader = 4,
        Joystick = 7,
        ChargerConnected = 28,
        Other = 999
    }

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
    private static PowerLineStatus prevPowerLineStatus = PowerLineStatus.Offline;

    // EventLogWatcher for power mode detection
    private static EventLogWatcher? _powerModeWatcher;
    private static readonly XNamespace _ns = "http://schemas.microsoft.com/win/2004/08/events/event";

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
        // Initialize EventLogWatcher for power mode detection
        try
        {
            // Query for Kernel-Power events: 506 (sleep entry) and 507 (wake)
            string xpath = "*[System[(EventID=506 or EventID=507) and Provider[@Name='Microsoft-Windows-Kernel-Power']]]";
            var query = new EventLogQuery("System", PathType.LogName, xpath);

            _powerModeWatcher = new EventLogWatcher(query);
            _powerModeWatcher.EventRecordWritten += OnEventRecordWritten;
            _powerModeWatcher.Enabled = true;

            LogManager.LogInformation("[SystemManager] Power mode watcher initialized");
        }
        catch (Exception ex)
        {
            LogManager.LogError("[SystemManager] Failed to initialize power mode watcher: {0}", ex.Message);
        }

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
        // Clean up EventLogWatcher
        if (_powerModeWatcher != null)
        {
            try
            {
                _powerModeWatcher.Enabled = false;
                _powerModeWatcher.EventRecordWritten -= OnEventRecordWritten;
                _powerModeWatcher.Dispose();
            }
            catch (Exception ex)
            {
                LogManager.LogError("[SystemManager] Error disposing power mode watcher: {0}", ex.Message);
            }
            finally
            {
                _powerModeWatcher = null;
            }
        }

        // manage events
        SystemEvents.PowerModeChanged -= OnPowerChange;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        PowerManager.BatteryStatusChanged -= BatteryStatusChanged;
        PowerManager.EnergySaverStatusChanged -= BatteryStatusChanged;
        PowerManager.PowerSupplyStatusChanged -= BatteryStatusChanged;
        PowerManager.RemainingChargePercentChanged -= BatteryStatusChanged;
        PowerManager.RemainingDischargeTimeChanged -= BatteryStatusChanged;
    }

    private static void OnPowerChange(object s, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                if (!isPowerSuspended)
                {
                    isPowerSuspended = true;
                    LogManager.LogDebug("Device entering sleep/hibernate (PowerModes.Suspend from SystemEvents)");
                    PowerModeChanged?.Invoke(PowerMode.Suspend, WakeReason.Unknown);
                    PerformSystemRoutine();
                }
                return;

            case PowerModes.Resume:
                if (isPowerSuspended)
                {
                    isPowerSuspended = false;
                    LogManager.LogDebug("Device waking from sleep/hibernate (PowerModes.Resume from SystemEvents). Actual WakeReason will come from Kernel-Power 507 event.");
                    PowerModeChanged?.Invoke(PowerMode.Resume, WakeReason.Unknown);
                    PerformSystemRoutine();
                }
                return;

            case PowerModes.StatusChange:
                {
                    PowerLineStatus powerLineStatus = SystemInformation.PowerStatus.PowerLineStatus;
                    if (prevPowerLineStatus != powerLineStatus)
                    {
                        // raise event
                        PowerLineStatusChanged?.Invoke(prevPowerLineStatus, powerLineStatus);

                        // update status
                        prevPowerLineStatus = powerLineStatus;
                    }
                }
                return;
        }
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

        // Check if current session is locked
        IsSessionLocked = !IsSessionInteractive();

        PerformSystemRoutine();

        IsInitialized = true;

        // listen to system events (after initialization so they won't be called prematurely)
        SubscribeToSystemEvents();

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

    private static void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null)
            return;

        int eventId = e.EventRecord.Id;
        WakeReason wakeReason = ParseWakeReason(e.EventRecord);

        try
        {
            if (eventId == 506 && !isPowerSuspended) // Modern Standby sleep entry
            {
                isPowerSuspended = true;
                LogManager.LogDebug("Device entering sleep (Kernel-Power 506)");
                PowerModeChanged?.Invoke(PowerMode.Suspend, wakeReason);
                PerformSystemRoutine();
            }
            else if (eventId == 507) // Modern Standby wake
            {
                // Always emit the wake reason from Kernel-Power, even if isPowerSuspended is already false.
                // This handles the race where PowerModes.Resume clears isPowerSuspended before this event arrives.
                LogManager.LogDebug("Device waking from sleep (Kernel-Power 507), reason: {0}", wakeReason);
                PowerModeChanged?.Invoke(PowerMode.Resume, wakeReason);

                // Only perform state transition if we're still marked as suspended
                if (isPowerSuspended)
                {
                    isPowerSuspended = false;
                    PerformSystemRoutine();
                }
            }
        }
        catch (Exception ex)
        {
            LogManager.LogError("Exception in OnEventRecordWritten: {0}", ex.Message);
        }
    }

    private static WakeReason ParseWakeReason(EventRecord evt)
    {
        try
        {
            var xml = evt.ToXml();
            var doc = XDocument.Parse(xml);

            var reasonVal = doc
                .Descendants(_ns + "Data")
                .FirstOrDefault(x => x.Attribute("Name")?.Value == "Reason")
                ?.Value;

            if (!int.TryParse(reasonVal, out int code))
                return WakeReason.Unknown;

            return code switch
            {
                1 => WakeReason.PowerButton,
                4 => WakeReason.FingerprintReader,
                7 => WakeReason.Joystick,
                28 => WakeReason.ChargerConnected,
                44 => WakeReason.FingerprintReader,
                0 => WakeReason.Unknown,
                _ => WakeReason.Other
            };
        }
        catch
        {
            return WakeReason.Unknown;
        }
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

        LogManager.LogInformation("System status set to {0} from {1}", currentSystemStatus, previousSystemStatus);
        SystemStatusChanged?.Invoke(currentSystemStatus, previousSystemStatus);

        // update status
        previousSystemStatus = currentSystemStatus;
    }
}