using ControllerCommon.Managers.Hid;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using Microsoft.Win32;
using Nefarius.Utilities.DeviceManagement.PnP;
using PInvoke;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Attributes = ControllerCommon.Managers.Hid.Attributes;
using Capabilities = ControllerCommon.Managers.Hid.Capabilities;

namespace ControllerCommon.Managers
{
    public static class SystemManager
    {
        #region import
        [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid")]
        static internal extern void HidD_GetHidGuidMethod(out Guid hidGuid);
        #endregion

        #region events
        public static event XInputDeviceArrivedEventHandler XUsbDeviceArrived;
        public delegate void XInputDeviceArrivedEventHandler(PnPDetails device);
        public static event XInputDeviceRemovedEventHandler XUsbDeviceRemoved;
        public delegate void XInputDeviceRemovedEventHandler(PnPDetails device);

        public static event GenericDeviceArrivedEventHandler UsbDeviceArrived;
        public delegate void GenericDeviceArrivedEventHandler(PnPDevice device);
        public static event GenericDeviceRemovedEventHandler UsbDeviceRemoved;
        public delegate void GenericDeviceRemovedEventHandler(PnPDevice device);

        public static event DInputDeviceArrivedEventHandler HidDeviceArrived;
        public delegate void DInputDeviceArrivedEventHandler(PnPDetails device);
        public static event DInputDeviceRemovedEventHandler HidDeviceRemoved;
        public delegate void DInputDeviceRemovedEventHandler(PnPDetails device);

        public static event SystemStatusChangedEventHandler SystemStatusChanged;
        public delegate void SystemStatusChangedEventHandler(SystemStatus status);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        public static Guid HidDevice;
        private static DeviceNotificationListener UsbDeviceListener = new();
        private static DeviceNotificationListener XUsbDeviceListener = new();
        private static DeviceNotificationListener HidDeviceListener = new();

        private static ConcurrentDictionary<string, PnPDetails> PnPDevices = new();

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
            HidD_GetHidGuidMethod(out HidDevice);
        }

        public static void Start()
        {
            // listen to system events
            SystemEvents.PowerModeChanged += OnPowerChange;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            UsbDeviceListener.StartListen(DeviceInterfaceIds.UsbDevice);
            UsbDeviceListener.DeviceArrived += UsbDevice_DeviceArrived;
            UsbDeviceListener.DeviceRemoved += UsbDevice_DeviceRemoved;

            XUsbDeviceListener.StartListen(DeviceInterfaceIds.XUsbDevice);
            XUsbDeviceListener.DeviceArrived += XUsbDevice_DeviceArrived;
            XUsbDeviceListener.DeviceRemoved += XUsbDevice_DeviceRemoved;

            HidDeviceListener.StartListen(DeviceInterfaceIds.HidDevice);
            HidDeviceListener.DeviceArrived += HidDevice_DeviceArrived;
            HidDeviceListener.DeviceRemoved += HidDevice_DeviceRemoved;

            RefreshHID();
            RefreshXInput();
            RefreshDInput();

            IsInitialized = true;
            Initialized?.Invoke();
        }

        private static void RefreshXInput()
        {
            int deviceIndex = 0;
            while (Devcon.Find(DeviceInterfaceIds.XUsbDevice, out var path, out var instanceId, deviceIndex++))
                XUsbDevice_DeviceArrived(new DeviceEventArgs() { InterfaceGuid = new Guid(), SymLink = path });
        }

        private static void RefreshDInput()
        {
            int deviceIndex = 0;
            while (Devcon.Find(DeviceInterfaceIds.HidDevice, out var path, out var instanceId, deviceIndex++))
                HidDevice_DeviceArrived(new DeviceEventArgs() { InterfaceGuid = new Guid(), SymLink = path });
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            // stop listening to system events
            SystemEvents.PowerModeChanged -= OnPowerChange;
            SystemEvents.SessionSwitch -= OnSessionSwitch;

            UsbDeviceListener.StopListen(DeviceInterfaceIds.UsbDevice);
            UsbDeviceListener.DeviceArrived -= UsbDevice_DeviceArrived;
            UsbDeviceListener.DeviceRemoved -= UsbDevice_DeviceRemoved;

            XUsbDeviceListener.StopListen(DeviceInterfaceIds.XUsbDevice);
            XUsbDeviceListener.DeviceArrived -= XUsbDevice_DeviceArrived;
            XUsbDeviceListener.DeviceRemoved -= XUsbDevice_DeviceRemoved;

            HidDeviceListener.StopListen(DeviceInterfaceIds.HidDevice);
            HidDeviceListener.DeviceArrived -= HidDevice_DeviceArrived;
            HidDeviceListener.DeviceRemoved -= HidDevice_DeviceRemoved;
        }

        private static PnPDetails FindDeviceFromUSB(string InstanceId)
        {
            return PnPDevices.Values.Where(device => device.baseContainerDeviceInstancePath.Equals(InstanceId, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }

        private static PnPDetails FindDeviceFromUSB(PnPDevice parent)
        {
            return PnPDevices.Values.Where(device => device.baseContainerDeviceInstancePath.Equals(parent.InstanceId, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }

        private static PnPDetails FindDeviceFromHID(PnPDevice children)
        {
            PnPDevices.TryGetValue(children.InstanceId, out var device);
            return device;
        }

        private static void RefreshHID()
        {
            int deviceIndex = 0;
            while (Devcon.Find(DeviceInterfaceIds.HidDevice, out var path, out var instanceId, deviceIndex++))
            {
                var children = PnPDevice.GetDeviceByInterfaceId(path);
                var parent = children;

                // get attributes
                Attributes? attributes = GetHidAttributes(path);
                Capabilities? capabilities = GetHidCapabilities(path);

                if (attributes is null || capabilities is null)
                    continue;

                var ProductID = ((Attributes)attributes).ProductID.ToString("X4");
                var VendorID = ((Attributes)attributes).VendorID.ToString("X4");
                string FriendlyName = string.Empty;

                while (parent is not null)
                {
                    if (string.IsNullOrEmpty(FriendlyName))
                        FriendlyName = parent.GetProperty<string>(DevicePropertyKey.Device_FriendlyName);

                    var parentId = parent.GetProperty<string>(DevicePropertyKey.Device_Parent);

                    if (parentId.Equals(@"HTREE\ROOT\0", StringComparison.InvariantCultureIgnoreCase))
                        break;

                    if (parentId.Contains(@"USB\ROOT", StringComparison.InvariantCultureIgnoreCase))
                        break;

                    if (parentId.Contains(@"ROOT\SYSTEM", StringComparison.InvariantCultureIgnoreCase))
                        break;

                    if (parentId.Contains(@"HID\", StringComparison.InvariantCultureIgnoreCase))
                        break;

                    if (!parentId.Contains(ProductID, StringComparison.InvariantCultureIgnoreCase))
                        break;

                    if (!parentId.Contains(VendorID, StringComparison.InvariantCultureIgnoreCase))
                        break;

                    parent = PnPDevice.GetDeviceByInstanceId(parentId, DeviceLocationFlags.Normal);
                }

                // get details
                PnPDetails details = new PnPDetails()
                {
                    SymLink = path,

                    deviceInstancePath = children.DeviceId,
                    baseContainerDeviceInstancePath = parent.DeviceId,

                    FriendlyName = FriendlyName,
                    DeviceDesc = parent.GetProperty<string>(DevicePropertyKey.Device_DeviceDesc),
                    Manufacturer = parent.GetProperty<string>(DevicePropertyKey.Device_Manufacturer),

                    isVirtual = parent.IsVirtual(),
                    isGaming = IsGaming((Attributes)attributes, (Capabilities)capabilities),

                    arrivalDate = children.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate),

                    attributes = (Attributes)attributes,
                    capabilities = (Capabilities)capabilities,
                };

                // add or update device
                PnPDevices[children.InstanceId] = details;
            }
        }

        public static List<PnPDetails> GetDetails(ushort VendorId = 0, ushort ProductId = 0)
        {
            return PnPDevices.Values.OrderBy(a => a.arrivalDate).Where(a => a.attributes.VendorID == VendorId && a.attributes.ProductID == ProductId && !a.isHooked).ToList();
        }

        private static Attributes? GetHidAttributes(string path)
        {
            using var handle = Kernel32.CreateFile(path,
                Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ |
                Kernel32.ACCESS_MASK.GenericRight.GENERIC_WRITE,
                Kernel32.FileShare.FILE_SHARE_READ | Kernel32.FileShare.FILE_SHARE_WRITE,
                IntPtr.Zero, Kernel32.CreationDisposition.OPEN_EXISTING,
                Kernel32.CreateFileFlags.FILE_ATTRIBUTE_NORMAL
                | Kernel32.CreateFileFlags.FILE_FLAG_NO_BUFFERING
                | Kernel32.CreateFileFlags.FILE_FLAG_WRITE_THROUGH,
                Kernel32.SafeObjectHandle.Null
            );

            return GetAttributes.Get(handle.DangerousGetHandle());
        }

        private static Capabilities? GetHidCapabilities(string path)
        {
            using var handle = Kernel32.CreateFile(path,
                Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ |
                Kernel32.ACCESS_MASK.GenericRight.GENERIC_WRITE,
                Kernel32.FileShare.FILE_SHARE_READ | Kernel32.FileShare.FILE_SHARE_WRITE,
                IntPtr.Zero, Kernel32.CreationDisposition.OPEN_EXISTING,
                Kernel32.CreateFileFlags.FILE_ATTRIBUTE_NORMAL
                | Kernel32.CreateFileFlags.FILE_FLAG_NO_BUFFERING
                | Kernel32.CreateFileFlags.FILE_FLAG_WRITE_THROUGH,
                Kernel32.SafeObjectHandle.Null
            );

            return GetCapabilities.Get(handle.DangerousGetHandle());
        }

        private static bool IsGaming(Attributes attributes, Capabilities capabilities)
        {
            //      STEAM DECK                                                               STEAM CONTROLLER
            return (((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1205)) || ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1142)) || (0x05 == capabilities.UsagePage) || (0x01 == capabilities.UsagePage) && ((0x04 == capabilities.Usage) || (0x05 == capabilities.Usage)));
        }

        private static PnPDetails GetPnPDeviceEx(string InstanceId)
        {
            return PnPDevices[InstanceId];
        }

        private async static void XUsbDevice_DeviceRemoved(DeviceEventArgs obj)
        {
            // XInput device removed
            string InstanceId = obj.SymLink.Replace("#", @"\");
            InstanceId = CommonUtils.Between(InstanceId, @"\\?\", @"\{");

            var deviceEx = FindDeviceFromUSB(InstanceId);

            // give system at least one second to initialize device
            await Task.Delay(1000);
            PnPDevices.TryRemove(InstanceId, out var value);

            RefreshHID();
            XUsbDeviceRemoved?.Invoke(deviceEx);
            LogManager.LogDebug("XUsbDevice removed: {0}:{1}", deviceEx.Manufacturer, deviceEx.DeviceDesc);
        }

        private async static void XUsbDevice_DeviceArrived(DeviceEventArgs obj)
        {
            try
            {
                var device = PnPDevice.GetDeviceByInterfaceId(obj.SymLink);

                if (IsInitialized)
                {
                    // give system at least one second to initialize device
                    await Task.Delay(1000);
                    RefreshHID();
                }

                PnPDetails deviceEx = FindDeviceFromUSB(device);
                if (deviceEx != null && deviceEx.isGaming)
                {
                    XUsbDeviceArrived?.Invoke(deviceEx);
                    LogManager.LogDebug("XUsbDevice arrived: {0}:{1} (VID:{2}, PID:{3}) {4}", deviceEx.Manufacturer, deviceEx.DeviceDesc, deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstancePath);
                }
            }
            catch { }
        }

        private async static void HidDevice_DeviceRemoved(DeviceEventArgs obj)
        {
            try
            {
                string InstanceId = obj.SymLink.Replace("#", @"\");
                InstanceId = CommonUtils.Between(InstanceId, @"\\?\", @"\{");

                var deviceEx = GetPnPDeviceEx(InstanceId);

                // give system at least one second to initialize device
                await Task.Delay(1000);
                PnPDevices.TryRemove(InstanceId, out var value);

                RefreshHID();
                HidDeviceRemoved?.Invoke(deviceEx);
                LogManager.LogDebug("HidDevice removed: {0}:{1}", deviceEx.Manufacturer, deviceEx.DeviceDesc);
            }
            catch { }
        }

        private async static void HidDevice_DeviceArrived(DeviceEventArgs obj)
        {
            var device = PnPDevice.GetDeviceByInterfaceId(obj.SymLink);

            if (IsInitialized)
            {
                // give system at least one second to initialize device
                await Task.Delay(1000);
                RefreshHID();
            }

            PnPDetails deviceEx = FindDeviceFromHID(device);
            if (deviceEx != null && deviceEx.isGaming)
            {
                HidDeviceArrived?.Invoke(deviceEx);
                LogManager.LogDebug("HidDevice arrived: {0}:{1} (VID:{2}, PID:{3}) {4}", deviceEx.Manufacturer, deviceEx.DeviceDesc, deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstancePath);
            }
        }

        private static void UsbDevice_DeviceRemoved(DeviceEventArgs obj)
        {
            try
            {
                string symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
                string VendorID = CommonUtils.Between(symLink, "VID_", "&");
                string ProductID = CommonUtils.Between(symLink, "PID_", "&");

                if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                    UsbDeviceRemoved?.Invoke(null);
            }
            catch { }
        }

        private static void UsbDevice_DeviceArrived(DeviceEventArgs obj)
        {
            try
            {
                string symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
                string VendorID = CommonUtils.Between(symLink, "VID_", "&");
                string ProductID = CommonUtils.Between(symLink, "PID_", "&");

                if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                    UsbDeviceArrived?.Invoke(null);
            }
            catch { }
        }

        private static void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            LogManager.LogDebug("Device power mode set to {0}", e.Mode);

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

            SystemRoutine();
        }

        private static void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            LogManager.LogDebug("Session switched to {0}", e.Reason);

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

        public static void PlayWindowsMedia(string file)
        {
            string path = Path.Combine(@"c:\Windows\Media\", file);
            if (File.Exists(path))
                new SoundPlayer(path).Play();
        }
    }
}
