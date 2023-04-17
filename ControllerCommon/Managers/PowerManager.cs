using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SystemPowerManager = Windows.System.Power.PowerManager;

namespace ControllerCommon.Managers
{
    public static class PowerManager
    {
        #region import
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);
        #endregion

        #region events
        public static event SystemStatusChangedEventHandler SystemStatusChanged;
        public delegate void SystemStatusChangedEventHandler(SystemStatus status, SystemStatus prevStatus);

        public static event PowerStatusChangedEventHandler PowerStatusChanged;
        public delegate void PowerStatusChangedEventHandler(PowerStatus status);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        private static bool IsPowerSuspended = false;
        private static bool IsSessionLocked = true;

        private static SystemStatus currentSystemStatus = SystemStatus.SystemBooting;
        private static SystemStatus previousSystemStatus = SystemStatus.SystemBooting;

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
            { "BatterySaver10", "\uEA95" },
        };

        public enum SystemStatus
        {
            SystemBooting = 0,
            SystemPending = 1,
            SystemReady = 2,
        }

        static PowerManager()
        {
            // listen to system events
            SystemEvents.PowerModeChanged += OnPowerChange;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            SystemPowerManager.BatteryStatusChanged += BatteryStatusChanged;
            SystemPowerManager.EnergySaverStatusChanged += BatteryStatusChanged;
            SystemPowerManager.PowerSupplyStatusChanged += BatteryStatusChanged;
            SystemPowerManager.RemainingChargePercentChanged += BatteryStatusChanged;
            SystemPowerManager.RemainingDischargeTimeChanged += BatteryStatusChanged;
        }

        private static void BatteryStatusChanged(object sender, object e)
        {
            PowerStatusChanged?.Invoke(SystemInformation.PowerStatus);
        }

        public static void Start(bool service = false)
        {
            // check if current session is locked
            if (!service)
            {
                IntPtr handle = OpenInputDesktop(0, false, 0);
                IsSessionLocked = handle == IntPtr.Zero;
            }
            else
            {
                // bypass session lock check when running as a service
                IsSessionLocked = false;
            }

            SystemRoutine();

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "PowerManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            // stop listening to system events
            SystemEvents.PowerModeChanged -= OnPowerChange;
            SystemEvents.SessionSwitch -= OnSessionSwitch;

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
                    PowerStatusChanged?.Invoke(SystemInformation.PowerStatus);
                    return;
            }

            LogManager.LogDebug("Device power mode set to {0}", e.Mode);

            SystemRoutine();
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

            SystemRoutine();
        }

        private static void SystemRoutine()
        {
            if (!IsPowerSuspended && !IsSessionLocked)
                currentSystemStatus = SystemStatus.SystemReady;
            else
                currentSystemStatus = SystemStatus.SystemPending;

            // only raise event is system status has changed
            if (previousSystemStatus != currentSystemStatus)
            {
                LogManager.LogInformation("System status set to {0}", currentSystemStatus);
                SystemStatusChanged?.Invoke(currentSystemStatus, previousSystemStatus);

                previousSystemStatus = currentSystemStatus;
            }
        }
    }
}