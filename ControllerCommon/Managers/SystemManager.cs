using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using Microsoft.Win32;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ControllerCommon.Managers
{
    public static class SystemManager
    {
        #region import
        [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid")]
        static internal extern void HidD_GetHidGuidMethod(out Guid hidGuid);
        #endregion

        #region events
        public static event XInputArrivedEventHandler XInputArrived;
        public delegate void XInputArrivedEventHandler(PnPDeviceEx device);

        public static event XInputRemovedEventHandler XInputRemoved;
        public delegate void XInputRemovedEventHandler(PnPDeviceEx device);

        public static event SerialArrivedEventHandler SerialArrived;
        public delegate void SerialArrivedEventHandler(PnPDevice device);

        public static event SerialRemovedEventHandler SerialRemoved;
        public delegate void SerialRemovedEventHandler(PnPDevice device);

        public static event SystemStatusChangedEventHandler SystemStatusChanged;
        public delegate void SystemStatusChangedEventHandler(SystemStatus status);
        #endregion

        public static Guid HidDevice;
        private static DeviceNotificationListener hidListener = new();
        private static DeviceNotificationListener xinputListener = new();
        private static List<PnPDeviceEx> devices = new();

        private static bool IsPowerSuspended;
        private static bool IsSessionLocked;

        private static SystemStatus currentSystemStatus = SystemStatus.Ready;
        private static SystemStatus previousSystemStatus = SystemStatus.Ready;

        public static bool IsInitialized;

        public enum SystemStatus
        {
            Unready = 0,
            Ready = 1,
        }

        static SystemManager()
        {
            // initialize hid
            HidD_GetHidGuidMethod(out var interfaceGuid);
            HidDevice = interfaceGuid;
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            // listen to system events
            SystemEvents.PowerModeChanged += OnPowerChange;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            hidListener.StartListen(DeviceInterfaceIds.UsbDevice);
            hidListener.DeviceArrived += Listener_DeviceArrived;
            hidListener.DeviceRemoved += Listener_DeviceRemoved;

            xinputListener.StartListen(DeviceInterfaceIds.XUsbDevice);
            xinputListener.DeviceArrived += XinputListener_DeviceArrived;
            xinputListener.DeviceRemoved += XinputListener_DeviceRemoved;

            IsInitialized = true;
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            // stop listening to system events
            SystemEvents.PowerModeChanged -= OnPowerChange;
            SystemEvents.SessionSwitch -= OnSessionSwitch;

            hidListener.StopListen(DeviceInterfaceIds.UsbDevice);
            hidListener.DeviceArrived -= Listener_DeviceArrived;
            hidListener.DeviceRemoved -= Listener_DeviceRemoved;

            xinputListener.StopListen(DeviceInterfaceIds.XUsbDevice);
            xinputListener.DeviceArrived -= XinputListener_DeviceArrived;
            xinputListener.DeviceRemoved -= XinputListener_DeviceRemoved;

            IsInitialized = false;
        }

        public static bool IsVirtualDevice(PnPDevice device, bool isRemoved = false)
        {
            while (device is not null)
            {
                var parentId = device.GetProperty<string>(DevicePropertyKey.Device_Parent);

                if (parentId.Equals(@"HTREE\ROOT\0", StringComparison.OrdinalIgnoreCase))
                    break;

                device = PnPDevice.GetDeviceByInstanceId(parentId,
                    isRemoved
                        ? DeviceLocationFlags.Phantom
                        : DeviceLocationFlags.Normal
                );
            }

            //
            // TODO: test how others behave (reWASD, NVIDIA, ...)
            // 
            return device is not null &&
                   (device.InstanceId.StartsWith(@"ROOT\SYSTEM", StringComparison.OrdinalIgnoreCase)
                    || device.InstanceId.StartsWith(@"ROOT\USB", StringComparison.OrdinalIgnoreCase));
        }

        private static PnPDeviceEx GetDeviceEx(PnPDevice owner)
        {
            PnPDeviceEx deviceEx = new PnPDeviceEx()
            {
                deviceUSB = owner
            };

            int deviceIndex = 0;
            while (Devcon.Find(HidDevice, out var path, out var instanceId, deviceIndex++))
            {
                var pnpDevice = PnPDevice.GetDeviceByInterfaceId(path);
                var device = pnpDevice;

                while (device is not null)
                {
                    var parentId = device.GetProperty<string>(DevicePropertyKey.Device_Parent);

                    if (parentId == owner.DeviceId)
                    {
                        deviceEx = new PnPDeviceEx()
                        {
                            deviceUSB = device,
                            deviceHID = pnpDevice,
                            path = path,
                            isVirtual = IsVirtualDevice(pnpDevice),
                            deviceIndex = deviceIndex,
                            arrivalDate = pnpDevice.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate)
                        };
                        devices.Add(deviceEx);
                    }

                    if (parentId.Equals(@"HTREE\ROOT\0", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (parentId.Contains(@"USB\ROOT", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (parentId.Contains(@"HID\", StringComparison.OrdinalIgnoreCase))
                        break;

                    device = PnPDevice.GetDeviceByInstanceId(parentId, DeviceLocationFlags.Normal);
                }
            }

            return deviceEx;
        }

        public static List<PnPDeviceEx> GetDeviceExs()
        {
            devices.Clear();

            int deviceIndex = 0;
            while (Devcon.Find(HidDevice, out var path, out var instanceId, deviceIndex++))
            {
                var pnpDevice = PnPDevice.GetDeviceByInterfaceId(path);
                var device = pnpDevice;

                while (device is not null)
                {
                    var parentId = device.GetProperty<string>(DevicePropertyKey.Device_Parent);

                    if (parentId.Equals(@"HTREE\ROOT\0", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (parentId.Contains(@"USB\ROOT", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (parentId.Contains(@"ROOT\SYSTEM", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (parentId.Contains(@"HID\", StringComparison.OrdinalIgnoreCase))
                        break;

                    device = PnPDevice.GetDeviceByInstanceId(parentId, DeviceLocationFlags.Normal);
                }

                devices.Add(new PnPDeviceEx()
                {
                    deviceUSB = device,
                    deviceHID = pnpDevice,
                    path = path,
                    isVirtual = IsVirtualDevice(pnpDevice),
                    deviceIndex = deviceIndex,
                    arrivalDate = pnpDevice.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate)
                });
            }

            return devices;
        }

        private static PnPDeviceEx GetPnPDeviceEx(string InstanceId)
        {
            return devices.Where(a => a.deviceUSB.InstanceId == InstanceId).FirstOrDefault();
        }

        private static void XinputListener_DeviceRemoved(DeviceEventArgs obj)
        {
            // XInput device removed
            try
            {
                string InstanceId = obj.SymLink.Replace("#", @"\");
                InstanceId = CommonUtils.Between(InstanceId, @"\\?\", @"\{");

                var deviceEx = GetPnPDeviceEx(InstanceId);
                devices.Remove(deviceEx);

                XInputRemoved?.Invoke(deviceEx);
            }
            catch (Exception) { }
        }

        private async static void XinputListener_DeviceArrived(DeviceEventArgs obj)
        {
            // XInput device arrived
            try
            {
                var device = PnPDevice.GetDeviceByInterfaceId(obj.SymLink);
                await Task.Delay(1000);
                var deviceEx = GetDeviceEx(device);
                XInputArrived?.Invoke(deviceEx);
            }
            catch (Exception) { }
        }

        private static void Listener_DeviceRemoved(DeviceEventArgs obj)
        {
            try
            {
                string symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
                string VendorID = CommonUtils.Between(symLink, "VID_", "&");
                string ProductID = CommonUtils.Between(symLink, "PID_", "&");

                if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                    SerialRemoved?.Invoke(null);
            }
            catch (Exception) { }
        }

        private static void Listener_DeviceArrived(DeviceEventArgs obj)
        {
            try
            {
                string symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
                string VendorID = CommonUtils.Between(symLink, "VID_", "&");
                string ProductID = CommonUtils.Between(symLink, "PID_", "&");

                if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                    SerialArrived?.Invoke(null);
            }
            catch (Exception) { }
        }

        private static void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            LogManager.LogInformation("Device power mode set to {0}", e.Mode);

            switch (e.Mode)
            {
                case PowerModes.Resume:
                    IsPowerSuspended = false;
                    break;
                case PowerModes.Suspend:
                    IsPowerSuspended = true;
                    break;
            }

            SystemRoutine();
        }

        private static void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            LogManager.LogInformation("Session switched to {0}", e.Reason);

            switch (e.Reason)
            {
                case SessionSwitchReason.SessionUnlock:
                    IsSessionLocked = false;
                    break;
                case SessionSwitchReason.SessionLock:
                    IsSessionLocked = true;
                    break;
            }

            SystemRoutine();
        }

        private static void SystemRoutine()
        {
            if (!IsPowerSuspended && !IsSessionLocked)
                currentSystemStatus = SystemStatus.Ready;
            else
                currentSystemStatus = SystemStatus.Unready;

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
