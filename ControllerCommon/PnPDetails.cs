using ControllerCommon.Managers.Hid;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Runtime.InteropServices;

namespace ControllerCommon
{
    [StructLayout(LayoutKind.Sequential)]
    public class PnPDetails : IDisposable
    {
        public string Name;
        public string Path;
        public string SymLink;

        public bool isVirtual;
        public bool isGaming;
        public bool isHooked;
        public bool isXInput;

        public DateTimeOffset arrivalDate;

        public string deviceInstanceId;
        public string baseContainerDeviceInstanceId;

        public Attributes attributes;
        public Capabilities capabilities;

        public string GetProductID()
        {
            return "0x" + attributes.ProductID.ToString("X4");
        }

        public string GetVendorID()
        {
            return "0x" + attributes.VendorID.ToString("X4");
        }

        public UsbPnPDevice GetUsbPnPDevice()
        {
            var pnpDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
            if (pnpDevice is null)
                return null;

            // is this a USB device
            string enumerator = pnpDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
            if (!Equals(enumerator, "USB"))
                return null;

            return pnpDevice.ToUsbPnPDevice();
        }

        public bool CyclePort()
        {
            UsbPnPDevice usbDevice = GetUsbPnPDevice();

            if (usbDevice is null)
                return false;

            usbDevice.CyclePort();
            return true;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
