using Microsoft.Win32;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
        public delegate void SystemStatusChangedEventHandler(SystemStatus status);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        private static bool IsPowerSuspended = false;
        private static bool IsSessionLocked = true;

        private static SystemStatus currentSystemStatus = SystemStatus.None;
        private static SystemStatus previousSystemStatus = SystemStatus.None;

        public static bool IsInitialized;

        public enum SystemStatus
        {
            None = 0,
            SystemPending = 1,
            SystemReady = 2,
        }

        static PowerManager()
        {
            // listen to system events
            SystemEvents.PowerModeChanged += OnPowerChange;
            SystemEvents.SessionSwitch += OnSessionSwitch;
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
                SystemStatusChanged?.Invoke(currentSystemStatus);
            }

            previousSystemStatus = currentSystemStatus;
        }
    }
}
