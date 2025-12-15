using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers.Hid;
using HandheldCompanion.Sensors;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Microsoft.Win32.SafeHandles;
using Nefarius.Utilities.DeviceManagement.PnP;
using PInvoke;
using SharpDX.Direct3D9;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Capabilities = HandheldCompanion.Managers.Hid.Capabilities;

namespace HandheldCompanion.Managers;

public class DeviceManager : IManager
{
    public Guid HidDevice;
    private readonly DeviceNotificationListener UsbDeviceListener = new();
    private readonly DeviceNotificationListener XUsbDeviceListener = new();
    private readonly DeviceNotificationListener HidDeviceListener = new();

    public readonly ConcurrentDictionary<string, PnPDetails> PnPDevices = new();

    private Timer adaptersTimer = new(2000) { AutoReset = false };

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

    private static byte[] XINPUT_LED_TO_PORT_MAP = new byte[16]
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

    public DeviceManager()
    {
        // initialize hid
        HidD_GetHidGuidMethod(out HidDevice);

        adaptersTimer.Elapsed += (sender, e) => RefreshDisplayAdapters(true);
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // manage events
        UsbDeviceListener.DeviceArrived += UsbDevice_DeviceArrived;
        UsbDeviceListener.DeviceRemoved += UsbDevice_DeviceRemoved;
        XUsbDeviceListener.DeviceArrived += XUsbDevice_DeviceArrived;
        XUsbDeviceListener.DeviceRemoved += XUsbDevice_DeviceRemoved;
        HidDeviceListener.DeviceArrived += HidDevice_DeviceArrived;
        HidDeviceListener.DeviceRemoved += HidDevice_DeviceRemoved;

        UsbDeviceListener.StartListen(DeviceInterfaceIds.UsbDevice);
        XUsbDeviceListener.StartListen(DeviceInterfaceIds.XUsbDevice);
        HidDeviceListener.StartListen(DeviceInterfaceIds.HidDevice);

        RefreshDrivers();
        RefreshDInputAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        RefreshXInputAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        RefreshDisplayAdapters(true);

        base.Start();
    }

    private void RefreshDrivers()
    {
        // fail-safe: restore drivers from incomplete controller suspend/resume process (if any)
        IEnumerable<string> drivers = DriverStore.GetKnownDrivers().Cast<string>();
        if (drivers.Count() != 0)
        {
            foreach (string InfPath in drivers)
                PnPUtil.StartPnPUtil($@"/add-driver C:\Windows\INF\{InfPath} /install");

            LogManager.LogInformation("Deploying known drivers {0} from driver store.", string.Join(',', drivers));
        }
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // manage events
        UsbDeviceListener.DeviceArrived -= UsbDevice_DeviceArrived;
        UsbDeviceListener.DeviceRemoved -= UsbDevice_DeviceRemoved;
        XUsbDeviceListener.DeviceArrived -= XUsbDevice_DeviceArrived;
        XUsbDeviceListener.DeviceRemoved -= XUsbDevice_DeviceRemoved;
        HidDeviceListener.DeviceArrived -= HidDevice_DeviceArrived;
        HidDeviceListener.DeviceRemoved -= HidDevice_DeviceRemoved;

        UsbDeviceListener.StopListen(DeviceInterfaceIds.UsbDevice);
        XUsbDeviceListener.StopListen(DeviceInterfaceIds.XUsbDevice);
        HidDeviceListener.StopListen(DeviceInterfaceIds.HidDevice);

        adaptersTimer.Stop();

        base.Stop();
    }

    public async Task RefreshXInputAsync()
    {
        var tasks = new List<Task>();
        var deviceIndex = 0;
        var devices = new Dictionary<string, DateTimeOffset>();

        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.XUsbDevice, out var path, out var _, deviceIndex++))
        {
            var device = PnPDevice.GetDeviceByInterfaceId(path);
            devices[path] = device.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate);
        }

        foreach (var (path, _) in devices.OrderBy(d => d.Value))
        {
            var args = new DeviceEventArgs { InterfaceGuid = DeviceInterfaceIds.XUsbDevice, SymLink = path };
            XUsbDevice_DeviceArrived(args); // posts a Task into arrivalInProgress

            var instanceId = SymLinkToInstanceId(args.SymLink, args.InterfaceGuid.ToString());
            Task t;
            // wait briefly until the arrival task is visible
            var until = DateTime.UtcNow.AddMilliseconds(500);
            while (!arrivalInProgress.TryGetValue(instanceId, out t) && DateTime.UtcNow < until)
                await Task.Delay(10).ConfigureAwait(false);

            if (t != null) tasks.Add(t);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task RefreshDInputAsync()
    {
        var tasks = new List<Task>();
        var deviceIndex = 0;
        var devices = new Dictionary<string, DateTimeOffset>();

        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.HidDevice, out var path, out var _, deviceIndex++))
        {
            var device = PnPDevice.GetDeviceByInterfaceId(path);
            devices[path] = device.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate);
        }

        foreach (var (path, _) in devices.OrderBy(d => d.Value))
        {
            var args = new DeviceEventArgs { InterfaceGuid = DeviceInterfaceIds.HidDevice, SymLink = path };
            HidDevice_DeviceArrived(args);

            var instanceId = SymLinkToInstanceId(args.SymLink, args.InterfaceGuid.ToString());
            Task t;
            var until = DateTime.UtcNow.AddMilliseconds(500);
            while (!hidArrivalInProgress.TryGetValue(instanceId, out t) && DateTime.UtcNow < until)
                await Task.Delay(10).ConfigureAwait(false);

            if (t != null) tasks.Add(t);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public PnPDetails FindDevice(string InstanceId)
    {
        if (InstanceId.StartsWith(@"USB\"))
            return FindDeviceFromUSB(InstanceId);
        if (InstanceId.StartsWith(@"HID\"))
            return FindDeviceFromHID(InstanceId);
        return null;
    }

    public PnPDetails FindDeviceFromUSB(string InstanceId)
    {
        PnPDetails details = null;
        while (details is null)
        {
            details = PnPDevices.Values.FirstOrDefault(device => device.baseContainerDeviceInstanceId.Equals(InstanceId, StringComparison.InvariantCultureIgnoreCase));

            if (details is not null)
                return details;

            // look for parent
            try
            {
                PnPDevice pnPDevice = PnPDevice.GetDeviceByInstanceId(InstanceId);
                if (pnPDevice.Parent is null)
                    break;

                InstanceId = pnPDevice.Parent.InstanceId;
            }
            catch
            {
                break;
            }
        }

        return details;
    }

    public PnPDetails FindDeviceFromHID(string InstanceId)
    {
        PnPDevices.TryGetValue(InstanceId, out var device);
        return device;
    }

    private int GetDeviceIndex(string path)
    {
        int deviceIndex = 0;
        Guid targetInterface = DeviceInterfaceIds.HidDevice;

        if (path.Contains("USB"))
            targetInterface = DeviceInterfaceIds.XUsbDevice;
        else
        {
            // We are trying to get a XInput HID here
            path = path.Replace(DeviceInterfaceIds.XUsbDevice.ToString(), DeviceInterfaceIds.HidDevice.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        while (Devcon.FindByInterfaceGuid(targetInterface, out var symlink, out var instanceId, deviceIndex++))
            if (symlink.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                return deviceIndex;

        return byte.MaxValue;
    }

    [Flags]
    private enum DeviceStatus
    {
        DN_DISABLEABLE = 0x00002000,
        DN_REMOVABLE = 0x00004000
    }

    private PnPDetails? GetDetails(string path)
    {
        try
        {
            PnPDevice? hidDevice = PnPDevice.GetDeviceByInterfaceId(path);
            if (hidDevice is null)
                return null;

            // get attributes
            Attributes? attributes = GetHidAttributes(path);
            Capabilities? capabilities = GetHidCapabilities(path);

            if (!attributes.HasValue || !capabilities.HasValue)
                return null;

            string ProductID = attributes.Value.ProductID.ToString("X4");
            string VendorID = attributes.Value.VendorID.ToString("X4");

            IPnPDevice? usbDevice = hidDevice;
            string parentId = usbDevice.InstanceId;

            while (usbDevice.Parent is not null)
            {
                // update parent InstanceId
                parentId = usbDevice.Parent.InstanceId;

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
                usbDevice = check;
            }

            // get root
            IPnPDevice? root = usbDevice;
            string rootId = root.InstanceId;
            while (root.Parent is not null)
            {
                if (rootId.Contains(@"USB\ROOT", StringComparison.InvariantCultureIgnoreCase))
                    break;

                if (rootId.Contains(@"ROOT\SYSTEM", StringComparison.InvariantCultureIgnoreCase))
                    break;

                // update root
                root = PnPDevice.GetDeviceByInstanceId(rootId);

                string? Name = root.GetProperty<string>(DevicePropertyKey.Device_DeviceDesc);
                if (!string.IsNullOrEmpty(Name) && Name.Contains(@"USB Hub", StringComparison.InvariantCultureIgnoreCase))
                    break;

                // update parent InstanceId
                if (root.Parent is not null)
                    rootId = root.Parent.InstanceId;
            }

            DeviceStatus Device_DevNodeStatus = (DeviceStatus)root.GetProperty<UInt32>(DevicePropertyKey.Device_DevNodeStatus);
            bool IsDisableable = Device_DevNodeStatus.HasFlag(DeviceStatus.DN_DISABLEABLE);
            bool IsRemovable = Device_DevNodeStatus.HasFlag(DeviceStatus.DN_REMOVABLE);

            // get details
            PnPDetails details = new PnPDetails
            {
                devicePath = path,
                SymLink = SymLinkToInstanceId(path, DeviceInterfaceIds.HidDevice.ToString()),
                Name = usbDevice.GetProperty<string>(DevicePropertyKey.Device_DeviceDesc) ?? string.Empty,
                EnumeratorName = usbDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName) ?? string.Empty,
                deviceInstanceId = hidDevice.InstanceId.ToUpper(),
                baseContainerDeviceInstanceId = usbDevice.InstanceId.ToUpper(),
                isVirtual = (usbDevice.IsVirtual() || hidDevice.IsVirtual()) && !IsMoonlight(attributes.Value),
                isGaming = IsGaming(attributes.Value, capabilities.Value),
                ProductID = attributes.Value.ProductID,
                VendorID = attributes.Value.VendorID,
                isXInput = hidDevice.InstanceId.Contains("IG_", StringComparison.InvariantCultureIgnoreCase),
            };
            details.isExternal = IsDisableable || IsRemovable || details.isBluetooth;

            // get name
            string DeviceDesc = usbDevice.GetProperty<string>(DevicePropertyKey.Device_DeviceDesc) ?? string.Empty;
            string FriendlyName = usbDevice.GetProperty<string>(DevicePropertyKey.Device_FriendlyName) ?? string.Empty;

            if (!string.IsNullOrEmpty(FriendlyName))
                details.Name = FriendlyName;
            else if (!string.IsNullOrEmpty(DeviceDesc))
                details.Name = DeviceDesc;

            // one more check
            details.isXInput |= details.Name.Contains("XINPUT", StringComparison.InvariantCultureIgnoreCase);

            // add or update device
            PnPDevices[details.SymLink] = details;

            return details;
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Check if controller is Moolight's virtual DS4
    /// </summary>
    /// <param name="attributes"></param>
    /// <returns></returns>
    private static bool IsMoonlight(Attributes attributes) => attributes.VendorID == 1356 && attributes.ProductID == 1476;

    public List<PnPDetails> GetDetails(ushort VendorId = 0, ushort ProductId = 0)
    {
        return PnPDevices.Values.OrderBy(device => device.XInputDeviceIdx).Where(device =>
            device.VendorID == VendorId && device.ProductID == ProductId && !device.isHooked).ToList();
    }

    public PnPDetails GetDeviceByInterfaceId(string path)
    {
        var device = PnPDevice.GetDeviceByInterfaceId(path);
        if (device is null)
            return null;

        return new PnPDetails
        {
            devicePath = path,
            SymLink = SymLinkToInstanceId(path, DeviceInterfaceIds.UsbDevice.ToString()),
            deviceInstanceId = device.InstanceId,
            baseContainerDeviceInstanceId = device.InstanceId,
            isVirtual = device.IsVirtual(),
        };
    }

    public string GetManufacturerString(string path)
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

    public string GetProductString(string path)
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

    private string GetString(IntPtr handle, Func<IntPtr, byte[], uint, bool> proc)
    {
        var buf = new byte[256];

        if (!proc(handle, buf, (uint)buf.Length))
            return null;

        var str = Encoding.Unicode.GetString(buf, 0, buf.Length);

        return str.Contains("\0") ? str.Substring(0, str.IndexOf('\0')) : str;
    }

    private Attributes? GetHidAttributes(string path)
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

    private Capabilities? GetHidCapabilities(string path)
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

    private bool IsGaming(Attributes attributes, Capabilities capabilities)
    {
        return (
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1102)) || // STEAM CONTROLLER
                                                                                     // ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1106)) || // STEAM CONTROLLER BLUETOOTH
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1142)) || // STEAM CONTROLLER WIRELESS
            ((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1205)) || // STEAM DECK
            (0x05 == capabilities.UsagePage) || (0x01 == capabilities.UsagePage) && ((0x04 == capabilities.Usage) || (0x05 == capabilities.Usage)));
    }

    public PnPDetails GetPnPDeviceEx(string SymLink)
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

    public static string SymLinkToInstanceId(string SymLink)
    {
        if (TryExtractInterfaceGuid(SymLink, out Guid InterfaceGuid))
            return SymLinkToInstanceId(SymLink, InterfaceGuid.ToString());
        return string.Empty;
    }

    /// <summary>
    /// Tries to pull the interface‐GUID out of a Windows device path.
    /// </summary>
    /// <param name="devicePath">
    /// The full path, e.g.  
    /// \\?\hid#vid_045e&pid_02ff&ig_00#9&...&0000#{ec87f1e3-c13b-4100-b5f7-8b84d54260cb}
    /// </param>
    /// <param name="interfaceGuid">
    /// On success, contains the extracted Guid; otherwise Guid.Empty.
    /// </param>
    /// <returns>True if a GUID was found; false otherwise.</returns>
    public static bool TryExtractInterfaceGuid(string devicePath, out Guid interfaceGuid)
    {
        interfaceGuid = Guid.Empty;

        if (string.IsNullOrEmpty(devicePath))
            return false;

        // Match a brace-enclosed GUID anywhere in the string
        const string pattern = @"\{(?<g>[0-9A-Fa-f]{8}(?:-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12})\}";
        var m = Regex.Match(devicePath, pattern);
        if (m.Success)
        {
            interfaceGuid = new Guid(m.Groups["g"].Value);
            return true;
        }

        interfaceGuid = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Removes the trailing interface‐GUID (e.g. "{ec87f1e3-c13b-4100-b5f7-8b84d54260cb}") from a device path.
    /// </summary>
    public static string RemoveInterfaceGuid(string devicePath)
    {
        if (devicePath == null)
            throw new ArgumentNullException(nameof(devicePath));

        // Matches a brace‐enclosed GUID at the end of the string
        const string pattern = @"\{[0-9A-Fa-f]{8}(?:-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12}\}$";

        // Simply drop it
        return Regex.Replace(devicePath, pattern, string.Empty);
    }

    private readonly ConcurrentDictionary<string, Task> arrivalInProgress = new();
    private readonly ConcurrentDictionary<string, Task> hidArrivalInProgress = new();

    private void XUsbDevice_DeviceArrived(DeviceEventArgs obj)
    {
        var instanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

        var arrivalTask = Task.Run(async () =>
        {
            try
            {
                var deviceEx = await WaitUntilAsync(() => FindDevice(instanceId)).ConfigureAwait(false);
                if (deviceEx is null || !deviceEx.isGaming) return;

                deviceEx.isXInput = true;
                deviceEx.baseContainerDevicePath = obj.SymLink;
                deviceEx.XInputDeviceIdx = GetDeviceIndex(deviceEx.baseContainerDevicePath);

                if (deviceEx.EnumeratorName.Equals("USB", StringComparison.InvariantCultureIgnoreCase))
                    deviceEx.XInputUserIndex = await GetXInputIndexAsync(deviceEx.baseContainerDevicePath, false).ConfigureAwait(false);

                if (deviceEx.XInputUserIndex == byte.MaxValue)
                    deviceEx.XInputUserIndex = (byte)XInputController.TryGetUserIndex(deviceEx);

                deviceEx.InterfaceGuid = obj.InterfaceGuid;

                LogManager.LogDebug("XUsbDevice {4} arrived on slot {5}: {0} (VID:{1}, PID:{2}) {3}",
                    deviceEx.Name, deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstanceId,
                    deviceEx.isVirtual ? "virtual" : "physical", deviceEx.XInputUserIndex);

                XUsbDeviceArrived?.Invoke(deviceEx, obj.InterfaceGuid);
            }
            finally
            {
                arrivalInProgress.TryRemove(instanceId, out _);
            }
        });

        arrivalInProgress[instanceId] = arrivalTask;
    }

    private void XUsbDevice_DeviceRemoved(DeviceEventArgs obj)
    {
        _ = Task.Run(async () =>
        {
            var instanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            if (arrivalInProgress.TryGetValue(instanceId, out var pending))
                try { await pending.ConfigureAwait(false); } catch { /* swallow */ }

            var deviceEx = await WaitUntilAsync(() => FindDevice(instanceId)).ConfigureAwait(false);
            if (deviceEx is null) return;

            if (PnPDevices.TryRemove(deviceEx.SymLink, out _))
            {
                LogManager.LogDebug("XUsbDevice {1} removed from slot {2}: {0}",
                    deviceEx.Name, deviceEx.isVirtual ? "virtual" : "physical", deviceEx.XInputUserIndex);
                XUsbDeviceRemoved?.Invoke(deviceEx, obj.InterfaceGuid);
            }
        });
    }

    private void HidDevice_DeviceArrived(DeviceEventArgs obj)
    {
        var instanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

        var arrivalTask = Task.Run(async () =>
        {
            try
            {
                var deviceEx = await WaitUntilAsync(() => GetDetails(obj.SymLink)).ConfigureAwait(false);

                // skip if XInput (handled by XUSB logic)
                if (deviceEx is null || deviceEx.isXInput) return;

                deviceEx.InterfaceGuid = obj.InterfaceGuid;
                LogManager.LogDebug("HidDevice arrived: {0} (VID:{1}, PID:{2}) {3}",
                    deviceEx.Name, deviceEx.GetVendorID(), deviceEx.GetProductID(), deviceEx.deviceInstanceId);

                HidDeviceArrived?.Invoke(deviceEx, obj.InterfaceGuid);
            }
            finally
            {
                hidArrivalInProgress.TryRemove(instanceId, out _);
            }
        });

        hidArrivalInProgress[instanceId] = arrivalTask;
    }

    private void HidDevice_DeviceRemoved(DeviceEventArgs obj)
    {
        _ = Task.Run(async () =>
        {
            var instanceId = SymLinkToInstanceId(obj.SymLink, obj.InterfaceGuid.ToString());

            if (hidArrivalInProgress.TryGetValue(instanceId, out var pending))
                try { await pending.ConfigureAwait(false); } catch { }

            var deviceEx = await WaitUntilAsync(() => FindDevice(instanceId)).ConfigureAwait(false);
            if (deviceEx is null || deviceEx.isXInput) return;

            if (PnPDevices.TryRemove(deviceEx.SymLink, out _))
            {
                LogManager.LogDebug("HidDevice removed: {0}", deviceEx.Name);
                HidDeviceRemoved?.Invoke(deviceEx, obj.InterfaceGuid);
            }
        });
    }

    private static async Task<T?> WaitUntilAsync<T>(Func<T?> probe, int timeoutMs = 4000, int pollMs = 100) where T : class
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            try { var v = probe(); if (v is not null) return v; } catch { }
            await Task.Delay(pollMs).ConfigureAwait(false);
        }
        return null;
    }

    private void UsbDevice_DeviceRemoved(DeviceEventArgs obj)
    {
        try
        {
            var symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
            var VendorID = CommonUtils.Between(symLink, "VID_", "&");
            var ProductID = CommonUtils.Between(symLink, "PID_", "&");

            if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                UsbDeviceRemoved?.Invoke(null, obj.InterfaceGuid);
        }
        catch { }
    }

    private void UsbDevice_DeviceArrived(DeviceEventArgs obj)
    {
        try
        {
            var symLink = CommonUtils.Between(obj.SymLink, "#", "#") + "&";
            var VendorID = CommonUtils.Between(symLink, "VID_", "&");
            var ProductID = CommonUtils.Between(symLink, "PID_", "&");

            if (SerialUSBIMU.vendors.ContainsKey(new KeyValuePair<string, string>(VendorID, ProductID)))
                UsbDeviceArrived?.Invoke(null, obj.InterfaceGuid);
        }
        catch { }
    }

    public static PnPDetails GetDeviceFromInstanceId(string instanceId)
    {
        PnPDetails? details = null;

        // try to retrieve PnPDetails
        Task timeout = Task.Delay(TimeSpan.FromSeconds(6));
        while (!timeout.IsCompleted && details is null)
        {
            foreach (PnPDetails pnPDetails in ManagerFactory.deviceManager.PnPDevices.Values)
            {
                // devicePath
                string devicePath = SymLinkToInstanceId(pnPDetails.devicePath);
                if (instanceId.Equals(devicePath))
                {
                    details = pnPDetails;
                    break;
                }

                // container devicePath
                string basePath = SymLinkToInstanceId(pnPDetails.baseContainerDevicePath);
                if (instanceId.Equals(basePath))
                {
                    details = pnPDetails;
                    break;
                }
            }

            Task.Delay(250).Wait();
        }

        return details;
    }

    public static string GetPathFromUserIndex(uint userIndex)
    {
        uint size = 520;                 // max chars in buffer (incl. terminating \0)
        StringBuilder sb = new StringBuilder((int)size);

        uint hr = XInputController.XInputGetDevicePath(userIndex, sb, ref size);
        if (hr == 0) // ERROR_SUCCESS
        {
            string newPath = sb.ToString();
            if (!string.IsNullOrEmpty(newPath))
                return newPath;
        }

        return string.Empty;
    }

    public static async Task<byte> GetXInputIndexAsync(string symLink, bool UIthread)
    {
        const int maxAttempts = 4;
        byte ledState = 0;

        for (int i = 0; i < maxAttempts; i++)
        {
            using (SafeFileHandle handle = CreateFileW(symLink, GENERIC_WRITE | GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    return byte.MaxValue;

                byte[] request = new byte[] { 0x01, 0x01, 0x00 };
                byte[] response = new byte[3];
                uint returned = 0;

                if (DeviceIoControl(handle, IOCTL_XUSB_GET_LED_STATE, request, request.Length, response, response.Length, ref returned, IntPtr.Zero))
                {
                    ledState = response[2];
                    if (ledState >= 2 && ledState <= 9)
                        return XINPUT_LED_TO_PORT_MAP[ledState];
                }
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        return byte.MaxValue;
    }

    public ConcurrentDictionary<Guid, AdapterInformation> displayAdapters = [];
    public void RefreshDisplayAdapters(bool elapsed = false)
    {
        if (elapsed)
        {
            // get the current list of adapters with Direct3D capabilities
            AdapterCollection adapters = new Direct3D().Adapters;
            List<Guid> adaptersGuids = adapters.Select(a => a.Details.DeviceIdentifier).ToList();
            List<Guid> adaptersProcessed = []; // keep track of processed adapter entries

            foreach (AdapterInformation adapterInformation in adapters)
            {
                if (displayAdapters.Keys.Contains(adapterInformation.Details.DeviceIdentifier) || adaptersProcessed.Contains(adapterInformation.Details.DeviceIdentifier))
                {
                    // known device
                }
                else
                {
                    // added device
                    DisplayAdapterArrived?.Invoke(adapterInformation);
                    adaptersProcessed.Add(adapterInformation.Details.DeviceIdentifier);
                }
            }

            foreach (Guid deviceIdentifier in displayAdapters.Keys)
            {
                if (adaptersGuids.Contains(deviceIdentifier) || adaptersProcessed.Contains(deviceIdentifier))
                {
                    // known device
                }
                else
                {
                    // removed device
                    AdapterInformation adapterInformation = displayAdapters[deviceIdentifier];
                    DisplayAdapterRemoved?.Invoke(adapterInformation);
                    adaptersProcessed.Add(deviceIdentifier);
                }
            }

            // clear dictionary
            displayAdapters.Clear();

            // use TryAdd because "extended" screen will report the same identifier and might cause a duplicated entry error
            foreach (AdapterInformation adapterInformation in adapters)
                displayAdapters.TryAdd(adapterInformation.Details.DeviceIdentifier, adapterInformation);
        }
        else
        {
            adaptersTimer.Stop();
            adaptersTimer.Start();
        }
    }

    public string[]? GetDevices(Guid? classGuid)
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

    public string? GetDeviceDesc(String PNPString)
    {
        if (CM_Locate_DevNode(out var devInst, PNPString, 0) != 0)
            return null;

        if (!CM_Get_DevNode_Property(devInst, DEVPKEY_Device_DeviceDesc, out var deviceDesc, 0))
            return null;

        return deviceDesc;
    }

    public IList<Tuple<UIntPtr, UIntPtr>>? GetDeviceMemResources(string PNPString)
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

    private bool CM_Get_DevNode_Property(IntPtr devInst, DEVPROPKEY propertyKey, out string result, int flags)
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

    private bool CM_Get_Res_Des_Data<T>(IntPtr rdResDes, out T buffer, int ulFlags) where T : struct
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

    public event XInputDeviceArrivedEventHandler XUsbDeviceArrived;
    public delegate void XInputDeviceArrivedEventHandler(PnPDetails device, Guid InterfaceGuid);

    public event XInputDeviceRemovedEventHandler XUsbDeviceRemoved;
    public delegate void XInputDeviceRemovedEventHandler(PnPDetails device, Guid InterfaceGuid);

    public event GenericDeviceArrivedEventHandler UsbDeviceArrived;
    public delegate void GenericDeviceArrivedEventHandler(PnPDevice device, Guid InterfaceGuid);

    public event GenericDeviceRemovedEventHandler UsbDeviceRemoved;
    public delegate void GenericDeviceRemovedEventHandler(PnPDevice device, Guid InterfaceGuid);

    public event DInputDeviceArrivedEventHandler HidDeviceArrived;
    public delegate void DInputDeviceArrivedEventHandler(PnPDetails device, Guid InterfaceGuid);

    public event DInputDeviceRemovedEventHandler HidDeviceRemoved;
    public delegate void DInputDeviceRemovedEventHandler(PnPDetails device, Guid InterfaceGuid);

    public event DisplayAdapterArrivedEventHandler DisplayAdapterArrived;
    public delegate void DisplayAdapterArrivedEventHandler(AdapterInformation adapterInformation);

    public event DisplayAdapterRemovedEventHandler DisplayAdapterRemoved;
    public delegate void DisplayAdapterRemovedEventHandler(AdapterInformation adapterInformation);

    #endregion
}