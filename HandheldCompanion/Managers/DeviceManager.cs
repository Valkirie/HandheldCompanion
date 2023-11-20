using HandheldCompanion.Managers.Hid;
using HandheldCompanion.Sensors;
using HandheldCompanion.Utils;
using Microsoft.Win32.SafeHandles;
using Nefarius.Utilities.DeviceManagement.PnP;
using PInvoke;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers;

public static class DeviceManager
{
    public static Guid HidDevice;
    private static readonly DeviceNotificationListener UsbDeviceListener = new();
    private static readonly DeviceNotificationListener XUsbDeviceListener = new();
    private static readonly DeviceNotificationListener HidDeviceListener = new();

    private static readonly ConcurrentDictionary<string, PnPDetails> PnPDevices = new();

    const ulong GENERIC_READ = (0x80000000L);
    const ulong GENERIC_WRITE = (0x40000000L);
    const ulong GENERIC_EXECUTE = (0x20000000L);
    const ulong GENERIC_ALL = (0x10000000L);

    const uint FILE_SHARE_READ = 0x00000001;
    const uint FILE_SHARE_WRITE = 0x00000002;
    const uint FILE_SHARE_DELETE = 0x00000004;

    const uint CREATE_NEW = 1;
    const uint CREATE_ALWAYS = 2;
    const uint OPEN_EXISTING = 3;
    const uint OPEN_ALWAYS = 4;
    const uint TRUNCATE_EXISTING = 5;

    const ulong IOCTL_XUSB_GET_LED_STATE = 0x8000E008;

    static byte[] XINPUT_LED_TO_PORT_MAP = new byte[16]
    {
        255,    // All off
        255,    // All blinking, then previous setting
        0,      // 1 flashes, then on
        1,      // 2 flashes, then on
        2,      // 3 flashes, then on
        3,      // 4 flashes, then on
        0,      // 1 on
        1,      // 2 on
        2,      // 3 on
        3,      // 4 on
        255,    // Rotate
        255,    // Blink, based on previous setting
        255,    // Slow blink, based on previous setting
        255,    // Rotate with two lights
        255,    // Persistent slow all blink
        255,    // Blink once, then previous setting
    };

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

        RefreshXInput();
        RefreshDInput();

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "DeviceManager");
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

        LogManager.LogInformation("{0} has stopped", "DeviceManager");
    }

    private static void RefreshXInput()
    {
        var deviceIndex = 0;
        Dictionary<string, DateTimeOffset> devices = new();

        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.XUsbDevice, out var path, out var instanceId,
                   deviceIndex++))
        {
            var device = PnPDevice.GetDeviceByInterfaceId(path);
            var arrival = device.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate);

            // add new device
            devices.Add(path, arrival);
        }

        // sort devices list
        devices = devices.OrderBy(device => device.Value).ToDictionary(x => x.Key, x => x.Value);
        foreach (var pair in devices)
            XUsbDevice_DeviceArrived(new DeviceEventArgs
            { InterfaceGuid = DeviceInterfaceIds.XUsbDevice, SymLink = pair.Key });
    }

    private static void RefreshDInput()
    {
        var deviceIndex = 0;
        Dictionary<string, DateTimeOffset> devices = new();

        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.HidDevice, out var path, out var instanceId,
                   deviceIndex++))
        {
            var device = PnPDevice.GetDeviceByInterfaceId(path);
            var arrival = device.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate);

            // add new device
            devices.Add(path, arrival);
        }

        // sort devices list
        devices = devices.OrderBy(device => device.Value).ToDictionary(x => x.Key, x => x.Value);
        foreach (var pair in devices)
            HidDevice_DeviceArrived(new DeviceEventArgs
            { InterfaceGuid = DeviceInterfaceIds.HidDevice, SymLink = pair.Key });
    }

    private static PnPDetails FindDevice(string InstanceId)
    {
        if (InstanceId.StartsWith(@"USB\"))
            return FindDeviceFromUSB(InstanceId);
        if (InstanceId.StartsWith(@"HID\"))
            return FindDeviceFromHID(InstanceId);
        return null;
    }

    public static PnPDetails FindDeviceFromUSB(string InstanceId)
    {
        PnPDetails details = null;
        while(details is null)
        {
            details = PnPDevices.Values.FirstOrDefault(device => device.baseContainerDeviceInstanceId.Equals(InstanceId, StringComparison.InvariantCultureIgnoreCase));

            if (details is not null)
                return details;

            // look for parent
            try
            {
                PnPDevice pnPDevice = PnPDevice.GetDeviceByInstanceId(InstanceId);
                InstanceId = pnPDevice.Parent.InstanceId;
            }
            catch
            {
                break;
            }
        }

        return details;
    }

    public static PnPDetails FindDeviceFromHID(string InstanceId)
    {
        PnPDevices.TryGetValue(InstanceId, out var device);
        return device;
    }

    private static int GetDeviceIndex(string path)
    {
        int deviceIndex = 0;

        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.HidDevice, out var symlink, out var instanceId, deviceIndex++))
            if (symlink == path)
                return deviceIndex;

        return 0;
    }

    private static PnPDetails GetDetails(string path)
    {
        try
        {
            string SymLink = SymLinkToInstanceId(path, DeviceInterfaceIds.HidDevice.ToString());

            PnPDevice children = PnPDevice.GetDeviceByInterfaceId(path);

            // get attributes
            Attributes? attributes = GetHidAttributes(path);
            Capabilities? capabilities = GetHidCapabilities(path);

            if (attributes is null || capabilities is null)
                return null;

            string ProductID = ((Attributes)attributes).ProductID.ToString("X4");
            string VendorID = ((Attributes)attributes).VendorID.ToString("X4");

            IPnPDevice? parent = children;
            string parentId = parent.InstanceId;

            while (parent.Parent is not null)
            {
                // update parent InstanceId
                parentId = parent.Parent.InstanceId;

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

                // we want the closest parent
                PnPDevice check = PnPDevice.GetDeviceByInstanceId(parentId);
                if (check.Children is not null)
                {
                    if (check.Children.Count() > 1)
                        break;
                }

                // update parent
                parent = check;
            }

            // get details
            PnPDetails details = new PnPDetails
            {
                devicePath = path,
                SymLink = SymLink,
                Name = parent.GetProperty<string>(DevicePropertyKey.Device_DeviceDesc),
                Enumerator = parent.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName),
                deviceInstanceId = children.InstanceId.ToUpper(),
                baseContainerDeviceInstanceId = parent.InstanceId.ToUpper(),
                isVirtual = parent.IsVirtual() || children.IsVirtual(),
                isGaming = IsGaming((Attributes)attributes, (Capabilities)capabilities),
                ProductID = ((Attributes)attributes).ProductID,
                VendorID = ((Attributes)attributes).VendorID,
                isXInput = children.InstanceId.Contains("IG_", StringComparison.InvariantCultureIgnoreCase),
            };

            // get name
            string DeviceDesc = parent.GetProperty<string>(DevicePropertyKey.Device_DeviceDesc);
            string FriendlyName = parent.GetProperty<string>(DevicePropertyKey.Device_FriendlyName);

            if (!string.IsNullOrEmpty(FriendlyName))
                details.Name = FriendlyName;
            else if (!string.IsNullOrEmpty(DeviceDesc))
                details.Name = DeviceDesc;

            // add or update device
            PnPDevices[details.SymLink] = details;

            return details;
        }
        catch { }

        return null;
    }

    public static List<PnPDetails> GetDetails(ushort VendorId = 0, ushort ProductId = 0)
    {
        return PnPDevices.Values.OrderBy(device => device.XInputDeviceIdx).Where(device =>
            device.VendorID == VendorId && device.ProductID == ProductId && !device.isHooked).ToList();
    }

    public static PnPDetails GetDeviceByInterfaceId(string path)
    {
        var device = PnPDevice.GetDeviceByInterfaceId(path);
        if (device is null)
            return null;

        return new PnPDetails
        {
            devicePath = path,
            SymLink = SymLinkToInstanceId(path, DeviceInterfaceIds.UsbDevice.ToString()),

            deviceInstanceId = device.InstanceId,
            baseContainerDeviceInstanceId = device.InstanceId
        };
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

    private static string GetString(IntPtr handle, Func<IntPtr, byte[], uint, bool> proc)
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
            Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ | Kernel32.ACCESS_MASK.GenericRight.GENERIC_WRITE,
            Kernel32.FileShare.FILE_SHARE_READ | Kernel32.FileShare.FILE_SHARE_WRITE,
            IntPtr.Zero,
            Kernel32.CreationDisposition.OPEN_EXISTING,
            Kernel32.CreateFileFlags.FILE_ATTRIBUTE_NORMAL,
            Kernel32.SafeObjectHandle.Null
        );

        return GetAttributes.Get(handle.DangerousGetHandle());
    }

    private static Capabilities? GetHidCapabilities(string path)
    {
        using var handle = Kernel32.CreateFile(path,
            Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ | Kernel32.ACCESS_MASK.GenericRight.GENERIC_WRITE,
            Kernel32.FileShare.FILE_SHARE_READ | Kernel32.FileShare.FILE_SHARE_WRITE,
            IntPtr.Zero,
            Kernel32.CreationDisposition.OPEN_EXISTING,
            Kernel32.CreateFileFlags.FILE_ATTRIBUTE_NORMAL,
            Kernel32.SafeObjectHandle.Null
        );

        return GetCapabilities.Get(handle.DangerousGetHandle());
    }

    private static bool IsGaming(Attributes attributes, Capabilities capabilities)
    {
        return (
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1102)) || // STEAM CONTROLLER
                                                                                     // ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1106)) || // STEAM CONTROLLER BLUETOOTH
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1142)) || // STEAM CONTROLLER WIRELESS
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1205)) || // STEAM DECK
            (0x05 == capabilities.UsagePage) || (0x01 == capabilities.UsagePage) && ((0x04 == capabilities.Usage) || (0x05 == capabilities.Usage)));
    }

    public static PnPDetails GetPnPDeviceEx(string SymLink)
    {
        PnPDevices.TryGetValue(SymLink, out var details);
        return details;
    }

    public static string SymLinkToInstanceId(string SymLink, string InterfaceGuid)
    {
        string InstanceId = SymLink.ToUpper().Replace(InterfaceGuid, "", StringComparison.InvariantCultureIgnoreCase);
        InstanceId = InstanceId.Replace("#", @"\");
        InstanceId = InstanceId.Replace(@"\\?\", "");
        InstanceId = InstanceId.Replace(@"\{}", "");
        return InstanceId;
    }

    private static async void XUsbDevice_DeviceRemoved(DeviceEventArgs obj)
    {
        string InstanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

        PnPDetails deviceEx = FindDevice(InstanceId);
        if (deviceEx is null)
            return;

        if (PnPDevices.TryRemove(deviceEx.SymLink, out var value))
        {
            LogManager.LogDebug("XUsbDevice {1} removed from slot {2}: {0}", deviceEx.Name, deviceEx.isVirtual ? "virtual" : "physical", deviceEx.XInputUserIndex);
            XUsbDeviceRemoved?.Invoke(deviceEx, obj);
        }
        else
        {
            // do something
        }
    }

    private static async void XUsbDevice_DeviceArrived(DeviceEventArgs obj)
    {
        try
        {
            string InstanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            PnPDetails deviceEx = null;

            DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(8));
            while (DateTime.Now < timeout && deviceEx is null)
            {
                deviceEx = FindDevice(InstanceId);
                await Task.Delay(1);
            }

            if (deviceEx is not null && deviceEx.isGaming)
            {
                deviceEx.isXInput = true;
                deviceEx.baseContainerDevicePath = obj.SymLink;

                if (deviceEx.Enumerator.Equals("USB"))
                    deviceEx.XInputUserIndex = GetXInputIndexAsync(obj.SymLink);

                if (deviceEx.XInputUserIndex == byte.MaxValue)
                    deviceEx.XInputDeviceIdx = GetDeviceIndex(obj.SymLink);

                LogManager.LogDebug("XUsbDevice {4} arrived on slot {5}: {0} (VID:{1}, PID:{2}) {3}", deviceEx.Name,
                    deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstanceId, deviceEx.isVirtual ? "virtual" : "physical", deviceEx.XInputUserIndex);

                // raise event
                Event:
                    XUsbDeviceArrived?.Invoke(deviceEx, obj);
            }
            else
            {
                // do something
            }
        }
        catch
        {
        }
    }

    private static async void HidDevice_DeviceRemoved(DeviceEventArgs obj)
    {
        try
        {
            string InstanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            PnPDetails deviceEx = FindDevice(InstanceId);

            // skip if XInput
            if (deviceEx is null || deviceEx.isXInput)
                return;

            if (PnPDevices.TryRemove(deviceEx.SymLink, out var value))
            {
                LogManager.LogDebug("HidDevice removed: {0}", deviceEx.Name);
                HidDeviceRemoved?.Invoke(deviceEx, obj);
            }
            else
            {
                // do something
            }
        }
        catch
        {
        }
    }

    private static async void HidDevice_DeviceArrived(DeviceEventArgs obj)
    {
        string InstanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

        PnPDetails deviceEx = null;

        DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(8));
        while (DateTime.Now < timeout && deviceEx is null)
        {
            deviceEx = GetDetails(obj.SymLink);
            await Task.Delay(1);
        }

        // skip if XInput
        if (deviceEx is null || deviceEx.isXInput)
            return;

        LogManager.LogDebug("HidDevice arrived: {0} (VID:{1}, PID:{2}) {3}", deviceEx.Name, deviceEx.GetVendorID(),
            deviceEx.GetProductID(), deviceEx.deviceInstanceId);
        HidDeviceArrived?.Invoke(deviceEx, obj);
    }

    private static void UsbDevice_DeviceRemoved(DeviceEventArgs obj)
    {
        try
        {
            var symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
            var VendorID = CommonUtils.Between(symLink, "VID_", "&");
            var ProductID = CommonUtils.Between(symLink, "PID_", "&");

            if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                UsbDeviceRemoved?.Invoke(null, obj);
        }
        catch
        {
        }
    }

    private static void UsbDevice_DeviceArrived(DeviceEventArgs obj)
    {
        try
        {
            var symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
            var VendorID = CommonUtils.Between(symLink, "VID_", "&");
            var ProductID = CommonUtils.Between(symLink, "PID_", "&");

            if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                UsbDeviceArrived?.Invoke(null, obj);
        }
        catch
        {
        }
    }

    public static byte GetXInputIndexAsync(string SymLink)
    {
        byte ledState = 0;

        DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(4));
        while (DateTime.Now < timeout && (ledState < 2 || ledState > 9))
        {
            using (SafeFileHandle handle = CreateFileW(SymLink, GENERIC_WRITE | GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0))
            {
                if (handle.IsInvalid)
                    return byte.MaxValue;

                byte[] gamepadStateRequest0101 = new byte[3] { 0x01, 0x01, 0x00 };
                byte[] ledStateData = new byte[3];
                uint len = 0;

                if (!DeviceIoControl(handle, IOCTL_XUSB_GET_LED_STATE, gamepadStateRequest0101, gamepadStateRequest0101.Length, ledStateData, ledStateData.Length, ref len, 0))
                    return byte.MaxValue;

                ledState = ledStateData[2];
            }

            Task.Delay(100);
        }

        return XINPUT_LED_TO_PORT_MAP[ledState];
    }

    public static string[]? GetDevices(Guid? classGuid)
    {
        string? filter = null;
        int flags = CM_GETIDLIST_FILTER_PRESENT;

        if (classGuid is not null)
        {
            filter = classGuid?.ToString("B").ToUpper();
            flags |= CM_GETIDLIST_FILTER_CLASS;
        }

        var res = CM_Get_Device_ID_List_Size(out var size, filter, flags);
        if (res != CR_SUCCESS)
            return null;

        char[] data = new char[size];
        res = CM_Get_Device_ID_List(filter, data, size, flags);
        if (res != CR_SUCCESS)
            return null;

        var result = new string(data);
        var devices = result.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return devices.ToArray();
    }

    public static string? GetDeviceDesc(String PNPString)
    {
        if (CM_Locate_DevNode(out var devInst, PNPString, 0) != 0)
            return null;

        if (!CM_Get_DevNode_Property(devInst, DEVPKEY_Device_DeviceDesc, out var deviceDesc, 0))
            return null;

        return deviceDesc;
    }

    public static IList<Tuple<UIntPtr, UIntPtr>>? GetDeviceMemResources(string PNPString)
    {
        int res = CM_Locate_DevNode(out var devInst, PNPString, 0);
        if (res != CR_SUCCESS)
            return null;

        res = CM_Get_First_Log_Conf(out var logConf, devInst, ALLOC_LOG_CONF);
        if (res != CR_SUCCESS)
            res = CM_Get_First_Log_Conf(out logConf, devInst, BOOT_LOG_CONF);
        if (res != CR_SUCCESS)
            return null;

        var ranges = new List<Tuple<UIntPtr, UIntPtr>>();

        while (CM_Get_Next_Res_Des(out var newResDes, logConf, ResType_Mem, out _, 0) == 0)
        {
            CM_Free_Res_Des_Handle(logConf);
            logConf = newResDes;

            if (!CM_Get_Res_Des_Data<MEM_RESOURCE>(logConf, out var memResource, 0))
                continue;

            ranges.Add(new Tuple<UIntPtr, UIntPtr>(
                memResource.MEM_Header.MD_Alloc_Base, memResource.MEM_Header.MD_Alloc_End));
        }

        CM_Free_Res_Des_Handle(logConf);
        return ranges;
    }

    static bool CM_Get_DevNode_Property(IntPtr devInst, DEVPROPKEY propertyKey, out string result, int flags)
    {
        result = default;

        // int length = 0;
        // int res = CM_Get_DevNode_Property(devInst, ref propertyKey, out var propertyType, null, ref length, flags);
        // if (res != CR_SUCCESS && res != CR_BUFFER_TOO_SMALL)
        //     return false;

        char[] buffer = new char[2048];
        int length = buffer.Length;
        int res = CM_Get_DevNode_Property(devInst, ref propertyKey, out var propertyType, buffer, ref length, flags);
        if (res != CR_SUCCESS)
            return false;
        if (propertyType != DEVPROP_TYPE_STRING)
            return false;

        result = new String(buffer, 0, length).Split('\0').First();
        return true;
    }

    static bool CM_Get_Res_Des_Data<T>(IntPtr rdResDes, out T buffer, int ulFlags) where T : struct
    {
        buffer = default;

        int res = CM_Get_Res_Des_Data_Size(out var size, rdResDes, ulFlags);
        if (res != CR_SUCCESS)
            return false;

        int sizeOf = Marshal.SizeOf<T>();
        if (sizeOf < size)
            return false;

        var addr = Marshal.AllocHGlobal(sizeOf);
        try
        {
            res = CM_Get_Res_Des_Data(rdResDes, addr, size, 0);
            if (res != CR_SUCCESS)
                return false;

            buffer = Marshal.PtrToStructure<T>(addr);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(addr);
        }
    }

    #region struct

    [StructLayout(LayoutKind.Sequential)]
    struct MEM_DES
    {
        internal uint MD_Count;
        internal uint MD_Type;
        internal UIntPtr MD_Alloc_Base;
        internal UIntPtr MD_Alloc_End;
        internal uint MD_Flags;
        internal uint MD_Reserved;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MEM_RANGE
    {
        internal UIntPtr MR_Align;     // specifies mask for base alignment
        internal uint MR_nBytes;    // specifies number of bytes required
        internal UIntPtr MR_Min;       // specifies minimum address of the range
        internal UIntPtr MR_Max;       // specifies maximum address of the range
        internal uint MR_Flags;     // specifies flags describing range (fMD flags)
        internal uint MR_Reserved;
    };

    [StructLayout(LayoutKind.Sequential)]
    struct MEM_RESOURCE
    {
        internal MEM_DES MEM_Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        internal MEM_RANGE[] MEM_Data;
    };

    [StructLayout(LayoutKind.Sequential)]
    struct DEVPROPKEY
    {
        public Guid Guid;
        public uint Pid;

        public DEVPROPKEY(String guid, uint pid)
        {
            this.Guid = new Guid(guid);
            this.Pid = pid;
        }
    };

    const int ALLOC_LOG_CONF = 0x00000002;  // Specifies the Alloc Element.
    const int BOOT_LOG_CONF = 0x00000003;  // Specifies the RM Alloc Element.
    const int ResType_Mem = (0x00000001);  // Physical address resource

    const int CM_GETIDLIST_FILTER_PRESENT = 0x00000100;
    const int CM_GETIDLIST_FILTER_CLASS = 0x00000200;
    const int CR_SUCCESS = 0x0;
    const int CR_BUFFER_TOO_SMALL = 0x1A;

    const int DEVPROP_TYPE_STRING = 0x00000012;

    static readonly DEVPROPKEY DEVPKEY_Device_DeviceDesc = new DEVPROPKEY("a45c254e-df1c-4efd-8020-67d146a850e0", 2);

    internal static readonly Guid GUID_DISPLAY = new Guid("{4d36e968-e325-11ce-bfc1-08002be10318}");

    #endregion

    #region import

    [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid")]
    internal static extern void HidD_GetHidGuidMethod(out Guid hidGuid);

    [DllImport("hid", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetManufacturerString(IntPtr HidDeviceObject, [Out] byte[] Buffer,
        uint BufferLength);

    [DllImport("hid", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetProductString(IntPtr HidDeviceObject, [Out] byte[] Buffer, uint BufferLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFileW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
        ulong dwDesiredAccess,
        ulong dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        ulong ioControlCode,
        byte[] inBuffer,
        int nInBufferSize,
        byte[] outBuffer,
        int nOutBufferSize,
        ref uint pBytesReturned,
        IntPtr overlapped);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
    static extern int CM_Locate_DevNode(out IntPtr pdnDevInst, string pDeviceID, int ulFlags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_ID_List_Size(out int idListlen, string? filter, int ulFlags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_ID_List(string? filter, char[] bffr, int bffrLen, int ulFlags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_DevNode_Property(IntPtr devInst, ref DEVPROPKEY propertyKey, out int propertyType, char[]? bffr, ref int bffrLen, int flags);

    [DllImport("setupapi.dll")]
    static extern int CM_Free_Res_Des_Handle(IntPtr rdResDes);

    [DllImport("setupapi.dll")]
    static extern int CM_Get_First_Log_Conf(out IntPtr rdResDes, IntPtr pdnDevInst, int ulFlags);

    [DllImport("setupapi.dll")]
    static extern int CM_Get_Next_Res_Des(out IntPtr newResDes, IntPtr rdResDes, int resType, out int resourceID, int ulFlags);

    [DllImport("setupapi.dll")]
    static extern int CM_Get_Res_Des_Data_Size(out int size, IntPtr rdResDes, int ulFlags);

    [DllImport("setupapi.dll")]
    static extern int CM_Get_Res_Des_Data(IntPtr rdResDes, IntPtr buffer, int size, int ulFlags);

    #endregion

    #region events

    public static event XInputDeviceArrivedEventHandler XUsbDeviceArrived;
    public delegate void XInputDeviceArrivedEventHandler(PnPDetails device, DeviceEventArgs obj);

    public static event XInputDeviceRemovedEventHandler XUsbDeviceRemoved;
    public delegate void XInputDeviceRemovedEventHandler(PnPDetails device, DeviceEventArgs obj);

    public static event GenericDeviceArrivedEventHandler UsbDeviceArrived;
    public delegate void GenericDeviceArrivedEventHandler(PnPDevice device, DeviceEventArgs obj);

    public static event GenericDeviceRemovedEventHandler UsbDeviceRemoved;
    public delegate void GenericDeviceRemovedEventHandler(PnPDevice device, DeviceEventArgs obj);

    public static event DInputDeviceArrivedEventHandler HidDeviceArrived;
    public delegate void DInputDeviceArrivedEventHandler(PnPDetails device, DeviceEventArgs obj);

    public static event DInputDeviceRemovedEventHandler HidDeviceRemoved;
    public delegate void DInputDeviceRemovedEventHandler(PnPDetails device, DeviceEventArgs obj);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}