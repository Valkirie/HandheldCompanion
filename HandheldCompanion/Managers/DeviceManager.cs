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

        Refresh();

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

    public static void Refresh()
    {
        RefreshHID();
        RefreshXInput();
        RefreshDInput();
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

    private static PnPDetails FindDevice(string SymLink, bool Removed = false)
    {
        if (SymLink.StartsWith(@"USB\"))
            return FindDeviceFromUSB(SymLink, Removed);
        if (SymLink.StartsWith(@"HID\"))
            return FindDeviceFromHID(SymLink, Removed);
        return null;
    }

    public static PnPDetails FindDeviceFromUSB(string SymLink, bool Removed)
    {
        var details = PnPDevices.Values
            .FirstOrDefault(device => device.baseContainerDeviceInstanceId.Equals(SymLink, StringComparison.InvariantCultureIgnoreCase));

        // backup plan
        if (details is null)
        {
            var deviceIndex = 0;
            while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.UsbDevice, out var path, out var instanceId,
                       deviceIndex++))
            {
                var parent = PnPDevice.GetDeviceByInterfaceId(path);

                path = PathToInstanceId(path, DeviceInterfaceIds.UsbDevice.ToString());
                if (path == SymLink)
                {
                    details = PnPDevices.Values.FirstOrDefault(device => device.baseContainerDeviceInstanceId.Equals(parent.InstanceId,
                        StringComparison.InvariantCultureIgnoreCase));
                    break;
                }
            }
        }

        return details;
    }

    public static PnPDetails FindDeviceFromHID(string SymLink, bool Removed)
    {
        PnPDevices.TryGetValue(SymLink, out var device);
        return device;
    }

    private static void RefreshHID()
    {
        var deviceIndex = 0;
        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.HidDevice, out var path, out var instanceId,
                   deviceIndex++))
        {
            var children = PnPDevice.GetDeviceByInterfaceId(path);

            var parent = children;
            var parentId = string.Empty;

            // get attributes
            var attributes = GetHidAttributes(path);
            var capabilities = GetHidCapabilities(path);

            if (attributes is null || capabilities is null)
                continue;

            var ProductID = ((Attributes)attributes).ProductID.ToString("X4");
            var VendorID = ((Attributes)attributes).VendorID.ToString("X4");
            var FriendlyName = string.Empty;

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

                parent = PnPDevice.GetDeviceByInstanceId(parentId);
            }

            if (string.IsNullOrEmpty(FriendlyName))
            {
                var product = GetProductString(path);
                var vendor = GetManufacturerString(path);

                FriendlyName = string.Join(' ', vendor, product).Trim();
            }

            // get details
            PnPDetails details = new PnPDetails
            {
                Path = path,
                SymLink = PathToInstanceId(path, DeviceInterfaceIds.HidDevice.ToString()),

                deviceInstanceId = children.InstanceId,
                baseContainerDeviceInstanceId = parent.InstanceId,

                Name = FriendlyName,

                isVirtual = parent.IsVirtual() || children.IsVirtual(),
                isGaming = IsGaming((Attributes)attributes, (Capabilities)capabilities),

                arrivalDate = children.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate),

                attributes = (Attributes)attributes,
                capabilities = (Capabilities)capabilities,

                DeviceIdx = deviceIndex
            };

            // add or update device
            if (!PnPDevices.ContainsKey(details.SymLink))
                PnPDevices.TryAdd(details.SymLink, details);
        }
    }

    public static List<PnPDetails> GetDetails(ushort VendorId = 0, ushort ProductId = 0)
    {
        return PnPDevices.Values.OrderBy(a => a.DeviceIdx).Where(a =>
            a.attributes.VendorID == VendorId && a.attributes.ProductID == ProductId && !a.isHooked).ToList();
    }

    public static PnPDetails GetDeviceByInterfaceId(string path)
    {
        var device = PnPDevice.GetDeviceByInterfaceId(path);
        if (device is null)
            return null;

        return new PnPDetails
        {
            Path = path,
            SymLink = PathToInstanceId(path, DeviceInterfaceIds.UsbDevice.ToString()),

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
        return (
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1102)) || // STEAM CONTROLLER
                                                                                     // ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1106)) || // STEAM CONTROLLER BLUETOOTH
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1142)) || // STEAM CONTROLLER WIRELESS
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1205)) || // STEAM DECK
            (0x05 == capabilities.UsagePage) || (0x01 == capabilities.UsagePage) && ((0x04 == capabilities.Usage) || (0x05 == capabilities.Usage)));
    }

    public static PnPDetails GetPnPDeviceEx(string SymLink)
    {
        if (PnPDevices.TryGetValue(SymLink, out var details))
            return details;

        return null;
    }

    public static string PathToInstanceId(string SymLink, string InterfaceGuid)
    {
        var output = SymLink.ToUpper().Replace(InterfaceGuid, "", StringComparison.InvariantCultureIgnoreCase);
        output = output.Replace("#", @"\");
        output = output.Replace(@"\\?\", "");
        output = output.Replace(@"\{}", "");
        return output;
    }

    private static async void XUsbDevice_DeviceRemoved(DeviceEventArgs obj)
    {
        var SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

        var deviceEx = FindDevice(SymLink, true);
        if (deviceEx is null)
            return;

        // give system at least one second to initialize device
        await Task.Delay(1000);
        if (PnPDevices.TryRemove(deviceEx.SymLink, out var value))
        {
            // RefreshHID();
            LogManager.LogDebug("XUsbDevice {1} removed: {0}", deviceEx.Name, deviceEx.isVirtual ? "virtual" : "physical");

            // raise event
            XUsbDeviceRemoved?.Invoke(deviceEx, obj);
        }
    }

    private static async void XUsbDevice_DeviceArrived(DeviceEventArgs obj)
    {
        try
        {
            var SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            if (IsInitialized)
            {
                // give system at least one second to initialize device
                await Task.Delay(1000);
                RefreshHID();
            }

            var deviceEx = FindDevice(SymLink);
            if (deviceEx is not null && deviceEx.isGaming)
            {
                SafeFileHandle handle = CreateFileW(obj.SymLink, GENERIC_WRITE | GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);

                if (handle.IsInvalid)
                    return;

                byte[] gamepadStateRequest0101 = new byte[3] { 0x01, 0x01, 0x00 };
                byte[] ledStateData = new byte[3];
                uint len = 0;

                if (!DeviceIoControl(handle, IOCTL_XUSB_GET_LED_STATE, gamepadStateRequest0101, gamepadStateRequest0101.Length, ledStateData, ledStateData.Length, ref len, 0))
                    return;

                byte ledState = ledStateData[2];

                deviceEx.isXInput = true;
                deviceEx.XInputUserIndex = XINPUT_LED_TO_PORT_MAP[ledState];

                LogManager.LogDebug("XUsbDevice {4} arrived on slot {5}: {0} (VID:{1}, PID:{2}) {3}", deviceEx.Name,
                    deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstanceId, deviceEx.isVirtual ? "virtual" : "physical", deviceEx.XInputUserIndex);

                // raise event
                XUsbDeviceArrived?.Invoke(deviceEx, obj);
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
            var SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            var deviceEx = FindDevice(SymLink, true);
            if (deviceEx is null)
                return;

            // give system at least one second to initialize device (+500 ms to give XInput priority)
            await Task.Delay(1500);
            PnPDevices.TryRemove(deviceEx.SymLink, out var value);

            // RefreshHID();
            LogManager.LogDebug("HidDevice removed: {0}", deviceEx.Name);
            HidDeviceRemoved?.Invoke(deviceEx, obj);
        }
        catch
        {
        }
    }

    private static async void HidDevice_DeviceArrived(DeviceEventArgs obj)
    {
        var SymLink = PathToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

        if (IsInitialized)
        {
            // give system at least one second to initialize device (+500 ms to give XInput priority)
            await Task.Delay(1500);
            RefreshHID();
        }

        var deviceEx = FindDevice(SymLink);
        if (deviceEx is not null && !deviceEx.isXInput)
        {
            LogManager.LogDebug("HidDevice arrived: {0} (VID:{1}, PID:{2}) {3}", deviceEx.Name, deviceEx.GetVendorID(),
                deviceEx.GetProductID(), deviceEx.deviceInstanceId);
            HidDeviceArrived?.Invoke(deviceEx, obj);
        }
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