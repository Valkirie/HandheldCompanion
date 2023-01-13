using ControllerCommon.Managers.Hid;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using Nefarius.Utilities.DeviceManagement.PnP;
using PInvoke;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Attributes = ControllerCommon.Managers.Hid.Attributes;
using Capabilities = ControllerCommon.Managers.Hid.Capabilities;
using Path = System.IO.Path;

namespace ControllerCommon.Managers
{
    public static class DeviceManager
    {
        #region import
        [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid")]
        static internal extern void HidD_GetHidGuidMethod(out Guid hidGuid);

        [DllImport("hid", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool HidD_GetManufacturerString(IntPtr HidDeviceObject, [Out] byte[] Buffer, uint BufferLength);

        [DllImport("hid", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool HidD_GetProductString(IntPtr HidDeviceObject, [Out] byte[] Buffer, uint BufferLength);
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

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        public static Guid HidDevice;
        private static DeviceNotificationListener UsbDeviceListener = new();
        private static DeviceNotificationListener XUsbDeviceListener = new();
        private static DeviceNotificationListener HidDeviceListener = new();

        private static ConcurrentDictionary<string, PnPDetails> PnPDevices = new();

        public static bool IsInitialized;

        static DeviceManager()
        {
            // initialize hid
            HidD_GetHidGuidMethod(out HidDevice);
        }

        public static void Start()
        {
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

            LogManager.LogInformation("{0} has started", "SystemManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            UsbDeviceListener.StopListen(DeviceInterfaceIds.UsbDevice);
            UsbDeviceListener.DeviceArrived -= UsbDevice_DeviceArrived;
            UsbDeviceListener.DeviceRemoved -= UsbDevice_DeviceRemoved;

            XUsbDeviceListener.StopListen(DeviceInterfaceIds.XUsbDevice);
            XUsbDeviceListener.DeviceArrived -= XUsbDevice_DeviceArrived;
            XUsbDeviceListener.DeviceRemoved -= XUsbDevice_DeviceRemoved;

            HidDeviceListener.StopListen(DeviceInterfaceIds.HidDevice);
            HidDeviceListener.DeviceArrived -= HidDevice_DeviceArrived;
            HidDeviceListener.DeviceRemoved -= HidDevice_DeviceRemoved;

            LogManager.LogInformation("{0} has stopped", "SystemManager");
        }

        private static void RefreshXInput()
        {
            int deviceIndex = 0;
            Dictionary<string, DateTimeOffset> devices = new();

            while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.XUsbDevice, out var path, out var instanceId, deviceIndex++))
            {
                PnPDevice device = PnPDevice.GetDeviceByInterfaceId(path);
                DateTimeOffset arrival = device.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate);

                // add new device
                devices.Add(path, arrival);
            }

            // sort devices list
            devices = devices.OrderBy(device => device.Value).ToDictionary(x => x.Key, x => x.Value);
            foreach (var pair in devices)
                XUsbDevice_DeviceArrived(new DeviceEventArgs() { InterfaceGuid = DeviceInterfaceIds.XUsbDevice, SymLink = pair.Key });
        }

        private static void RefreshDInput()
        {
            int deviceIndex = 0;
            Dictionary<string, DateTimeOffset> devices = new();

            while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.HidDevice, out var path, out var instanceId, deviceIndex++))
            {
                PnPDevice device = PnPDevice.GetDeviceByInterfaceId(path);
                DateTimeOffset arrival = device.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate);

                // add new device
                devices.Add(path, arrival);
            }

            // sort devices list
            devices = devices.OrderBy(device => device.Value).ToDictionary(x => x.Key, x => x.Value);
            foreach (var pair in devices)
                HidDevice_DeviceArrived(new DeviceEventArgs() { InterfaceGuid = DeviceInterfaceIds.HidDevice, SymLink = pair.Key });
        }

        private static PnPDetails FindDevice(string SymLink, bool Removed = false)
        {
            if (SymLink.StartsWith(@"USB\"))
                return FindDeviceFromUSB(SymLink, Removed);
            else if (SymLink.StartsWith(@"HID\"))
                return FindDeviceFromHID(SymLink, Removed);
            else
                return null;
        }

        private static PnPDetails FindDeviceFromUSB(string SymLink, bool Removed)
        {
            PnPDetails details = PnPDevices.Values.Where(device => device.baseContainerDeviceInstanceId.Equals(SymLink, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

            // backup plan
            if (details is null)
            {
                int deviceIndex = 0;
                while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.UsbDevice, out var path, out var instanceId, deviceIndex++))
                {
                    PnPDevice parent = PnPDevice.GetDeviceByInterfaceId(path);

                    path = PathToInstanceId(path, DeviceInterfaceIds.UsbDevice.ToString());
                    if (path == SymLink)
                    {
                        details = PnPDevices.Values.Where(device => device.baseContainerDeviceInstanceId.Equals(parent.InstanceId, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                        break;
                    }
                }
            }

            return details;
        }

        private static PnPDetails FindDeviceFromHID(string SymLink, bool Removed)
        {
            PnPDevices.TryGetValue(SymLink, out var device);
            return device;
        }

        private static void RefreshHID()
        {
            int deviceIndex = 0;
            while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.HidDevice, out var path, out var instanceId, deviceIndex++))
            {
                PnPDevice children = PnPDevice.GetDeviceByInterfaceId(path);

                PnPDevice parent = children;
                string parentId = string.Empty;

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

                    parentId = parent.GetProperty<string>(DevicePropertyKey.Device_Parent);

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

                if (string.IsNullOrEmpty(FriendlyName))
                {
                    string product = GetProductString(path);
                    string vendor = GetManufacturerString(path);

                    FriendlyName = string.Join(' ', vendor, product).Trim();
                }

                // get details
                PnPDetails details = new PnPDetails()
                {
                    SymLink = PathToInstanceId(path, DeviceInterfaceIds.HidDevice.ToString()),

                    deviceInstanceId = children.InstanceId,
                    baseContainerDeviceInstanceId = parent.InstanceId,

                    Name = FriendlyName,

                    isVirtual = parent.IsVirtual(),
                    isGaming = IsGaming((Attributes)attributes, (Capabilities)capabilities),

                    arrivalDate = children.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate),

                    attributes = (Attributes)attributes,
                    capabilities = (Capabilities)capabilities,
                };

                // add or update device
                if (!PnPDevices.ContainsKey(details.SymLink))
                    PnPDevices.TryAdd(details.SymLink, details);
            }
        }

        public static List<PnPDetails> GetDetails(ushort VendorId = 0, ushort ProductId = 0)
        {
            return PnPDevices.Values.OrderBy(a => a.arrivalDate).Where(a => a.attributes.VendorID == VendorId && a.attributes.ProductID == ProductId && !a.isHooked).ToList();
        }

        public static string GetManufacturerString(string path)
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

            return GetString(handle.DangerousGetHandle(), HidD_GetManufacturerString);
        }

        public static string GetProductString(string path)
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

            return GetString(handle.DangerousGetHandle(), HidD_GetProductString);
        }

        static string GetString(IntPtr handle, Func<IntPtr, byte[], uint, bool> proc)
        {
            var buf = new byte[256];

            if (!proc(handle, buf, (uint)buf.Length))
                return null;

            var str = Encoding.Unicode.GetString(buf, 0, buf.Length);

            return str.Contains("\0") ? str.Substring(0, str.IndexOf('\0')) : str;
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

        private static PnPDetails GetPnPDeviceEx(string SymLink)
        {
            return PnPDevices[SymLink];
        }

        private static string PathToInstanceId(string SymLink, string InterfaceGuid)
        {
            string output = SymLink.ToUpper().Replace(InterfaceGuid, "", StringComparison.InvariantCultureIgnoreCase);
            output = output.Replace("#", @"\");
            output = output.Replace(@"\\?\", "");
            output = output.Replace(@"\{}", "");
            return output;
        }

        private async static void XUsbDevice_DeviceRemoved(DeviceEventArgs obj)
        {
            string SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            var deviceEx = FindDevice(SymLink, true);
            if (deviceEx is null)
                return;

            // give system at least one second to initialize device
            await Task.Delay(1000);
            PnPDevices.TryRemove(deviceEx.SymLink, out var value);

            // RefreshHID();
            LogManager.LogDebug("XUsbDevice removed: {0}", deviceEx.Name);
            XUsbDeviceRemoved?.Invoke(deviceEx);
        }

        private async static void XUsbDevice_DeviceArrived(DeviceEventArgs obj)
        {
            try
            {
                string SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

                if (IsInitialized)
                {
                    // give system at least one second to initialize device
                    await Task.Delay(1000);
                    RefreshHID();
                }

                PnPDetails deviceEx = FindDevice(SymLink);
                if (deviceEx is not null && deviceEx.isGaming)
                {
                    deviceEx.isXInput = true;

                    LogManager.LogDebug("XUsbDevice arrived: {0} (VID:{1}, PID:{2}) {3}", deviceEx.Name, deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstanceId);
                    XUsbDeviceArrived?.Invoke(deviceEx);                    
                }
            }
            catch { }
        }

        private async static void HidDevice_DeviceRemoved(DeviceEventArgs obj)
        {
            try
            {
                string SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

                var deviceEx = FindDevice(SymLink, true);
                if (deviceEx is null)
                    return;

                // give system at least one second to initialize device (+500 ms to give XInput priority)
                await Task.Delay(1500);
                PnPDevices.TryRemove(deviceEx.SymLink, out var value);

                // RefreshHID();
                LogManager.LogDebug("HidDevice removed: {0}", deviceEx.Name);
                HidDeviceRemoved?.Invoke(deviceEx);
            }
            catch { }
        }

        private async static void HidDevice_DeviceArrived(DeviceEventArgs obj)
        {
            string SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            if (IsInitialized)
            {
                // give system at least one second to initialize device (+500 ms to give XInput priority)
                await Task.Delay(1500);
                RefreshHID();
            }

            PnPDetails deviceEx = FindDevice(SymLink);
            if (deviceEx is not null && deviceEx.isGaming && !deviceEx.isXInput)
            {
                LogManager.LogDebug("HidDevice arrived: {0} (VID:{1}, PID:{2}) {3}", deviceEx.Name, deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstanceId);
                HidDeviceArrived?.Invoke(deviceEx);
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
    }
}
