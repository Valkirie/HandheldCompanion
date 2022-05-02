using Microsoft.Extensions.Logging;
using Nefarius.Utilities.DeviceManagement.PnP;
using PInvoke;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Timers;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon
{
    public struct PnPDeviceEx
    {
        public int deviceIndex;
        public PnPDevice device;
        public string path;
        public bool isVirtual;
        public DateTimeOffset arrivalDate;
    }

    public class ControllerEx
    {
        private ILogger logger;

        private PnPDevice device;
        public Controller Controller;
        public string Manufacturer, DeviceDesc;
        public UserIndex UserIndex;

        private XInputCapabilitiesEx CapabilitiesEx;

        public bool isVirtual;
        public string ProductId, VendorId, XID;

        public string deviceInstancePath = "";
        public string baseContainerDeviceInstancePath = "";

        private Vibration IdentifyVibration = new Vibration() { LeftMotorSpeed = ushort.MaxValue, RightMotorSpeed = ushort.MaxValue };
        private Timer IdentifyTimer;

        [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid")]
        static internal extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", EntryPoint = "HidD_GetAttributes")]
        static internal extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref Attributes attributes);

        [StructLayout(LayoutKind.Sequential)]
        public struct Attributes
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public short VersionNumber;
        }

        private readonly Guid hidClassInterfaceGuid;

        public ControllerEx(UserIndex index, ILogger logger)
        {
            this.logger = logger;

            this.Controller = new Controller(index);
            this.UserIndex = index;

            // initialize timers
            IdentifyTimer = new Timer(200) { AutoReset = false };
            IdentifyTimer.Elapsed += IdentifyTimer_Tick;

            // pull data from xinput
            CapabilitiesEx = new XInputCapabilitiesEx();

            if (XInputGetCapabilitiesEx(1, (int)UserIndex, 0, ref CapabilitiesEx) == 0)
            {
                ProductId = CapabilitiesEx.ProductId.ToString("X4");
                VendorId = CapabilitiesEx.VendorId.ToString("X4");
            }
        }

        public ControllerEx(UserIndex index, ILogger logger, ref List<PnPDeviceEx> devices) : this(index, logger)
        {
            if (ProductId is null || VendorId is null)
                return;

            foreach(PnPDeviceEx deviceEx in devices)
            {
                device = deviceEx.device;

                // get attributes
                Attributes device_attributes = new Attributes();
                GetHidAttributes(deviceEx.path, out device_attributes);

                if (device_attributes.ProductID != CapabilitiesEx.ProductId || device_attributes.VendorID != CapabilitiesEx.VendorId)
                    continue;

                isVirtual = deviceEx.isVirtual;

                // update HID
                deviceInstancePath = device.DeviceId;
                devices.Remove(deviceEx);
                break;
            }

            while (device is not null)
            {
                // update device details
                DeviceDesc = device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
                Manufacturer = device.GetProperty<string>(DevicePropertyDevice.Manufacturer);

                baseContainerDeviceInstancePath = device.DeviceId;

                var parentId = device.GetProperty<string>(DevicePropertyDevice.Parent);

                if (parentId.Equals(@"HTREE\ROOT\0", StringComparison.OrdinalIgnoreCase))
                    return;

                if (parentId.Contains(@"USB\ROOT", StringComparison.OrdinalIgnoreCase))
                    return;

                if (parentId.Contains(@"HID\", StringComparison.OrdinalIgnoreCase))
                    return;

                if (!parentId.Contains(ProductId) && !parentId.Contains(VendorId))
                    return;

                device = PnPDevice.GetDeviceByInstanceId(parentId, DeviceLocationFlags.Normal);
            }
        }

        private bool GetHidAttributes(string path, out Attributes attributes)
        {
            attributes = new Attributes();

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

            var ret = HidD_GetAttributes(handle.DangerousGetHandle(), ref attributes);

            if (!ret) return false;

            return true;
        }

        public override string ToString()
        {
            return DeviceDesc;
        }

        public State GetState()
        {
            return Controller.GetState();
        }

        public bool IsConnected()
        {
            return Controller.IsConnected;
        }

        public ushort GetPID()
        {
            return CapabilitiesEx.ProductId;
        }

        public ushort GetVID()
        {
            return CapabilitiesEx.VendorId;
        }

        public void Identify()
        {
            if (!Controller.IsConnected)
                return;

            Controller.SetVibration(IdentifyVibration);
            IdentifyTimer.Stop();
            IdentifyTimer.Start();
        }

        private void IdentifyTimer_Tick(object sender, EventArgs e)
        {
            Controller.SetVibration(new Vibration());
        }
    }
}
