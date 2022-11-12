using ControllerCommon.Managers.Hid;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using Microsoft.Win32;
using Nefarius.Utilities.DeviceManagement.PnP;
using PInvoke;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using static WindowsInput.Native.SystemMetrics.Screen;

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
        public delegate void XInputArrivedEventHandler(PnPDetails device);

        public static event XInputRemovedEventHandler XInputRemoved;
        public delegate void XInputRemovedEventHandler(PnPDetails device);

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

        private static Dictionary<string, PnPDetails> devices = new();

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

            RefreshDevices();

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

        private static PnPDetails GetDeviceEx(PnPDevice parent)
        {
            return devices[parent.InstanceId];
        }

        private static void RefreshDevices()
        {
            int deviceIndex = 0;
            while (Devcon.Find(HidDevice, out var path, out var instanceId, deviceIndex++))
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

                while (parent is not null)
                {
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
                    deviceInstancePath = parent.DeviceId,
                    baseContainerDeviceInstancePath = children.DeviceId,

                    DeviceDesc = parent.GetProperty<string>(DevicePropertyKey.Device_DeviceDesc),
                    Manufacturer = parent.GetProperty<string>(DevicePropertyKey.Device_Manufacturer),

                    isVirtual = parent.IsVirtual(),
                    isGaming = IsGaming((Attributes)attributes, (Capabilities)capabilities),

                    arrivalDate = children.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_LastArrivalDate),

                    attributes = (Attributes)attributes,
                    capabilities = (Capabilities)capabilities,
                };
                
                // add or update device
                devices[parent.InstanceId] = details;
            }

            foreach (PnPDetails details in GetDeviceExs())
            {
                if (details.isGaming && !IsInitialized)
                {
                    XInputArrived?.Invoke(details);
                }
            }
        }

        public static List<PnPDetails> GetDeviceExs()
        {
            return devices.Values.OrderBy(a => a.arrivalDate).ToList();
        }

        public static List<PnPDetails> GetDetails(ushort VendorId = 0, ushort ProductId = 0)
        {
            List<PnPDetails> temp = devices.Values.OrderBy(a => a.arrivalDate).Where(a => a.attributes.VendorID == VendorId && a.attributes.ProductID == ProductId).ToList();

            return temp;
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
            return (((attributes.VendorID == 0x28DE) && (attributes.ProductID == 0x1142)) || (0x05 == capabilities.UsagePage) || (0x01 == capabilities.UsagePage) && ((0x04 == capabilities.Usage) || (0x05 == capabilities.Usage)));
        }

        private static PnPDetails GetPnPDeviceEx(string InstanceId)
        {
            return devices[InstanceId];
        }

        private async static void XinputListener_DeviceRemoved(DeviceEventArgs obj)
        {
            // XInput device removed
            try
            {
                string InstanceId = obj.SymLink.Replace("#", @"\");
                InstanceId = CommonUtils.Between(InstanceId, @"\\?\", @"\{");

                var deviceEx = GetPnPDeviceEx(InstanceId);
                devices.Remove(InstanceId);

                XInputRemoved?.Invoke(deviceEx);
            }
            catch (Exception) { }

            // give system at least one second to initialize device
            await Task.Delay(1000);
            RefreshDevices();
        }

        private async static void XinputListener_DeviceArrived(DeviceEventArgs obj)
        {
            // XInput device arrived
            try
            {
                var device = PnPDevice.GetDeviceByInterfaceId(obj.SymLink);

                // give system at least one second to initialize device
                await Task.Delay(1000);
                RefreshDevices();

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
                default:
                case PowerModes.StatusChange:
                    return;
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
