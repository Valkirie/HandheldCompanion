using ControllerCommon.Managers.Hid;
using System;
using System.Runtime.InteropServices;

namespace ControllerCommon
{
    [StructLayout(LayoutKind.Sequential)]
    public class PnPDetails
    {
        public string FriendlyName;
        public string Manufacturer;
        public string DeviceDesc;
        public string SymLink;

        public bool isVirtual;
        public bool isGaming;
        public bool isHooked;
        public DateTimeOffset arrivalDate;

        public string deviceInstancePath;
        public string baseContainerDeviceInstancePath;

        public Attributes attributes;
        public Capabilities capabilities;

        public string GetProductID()
        {
            return ((Attributes)attributes).ProductID.ToString("X4");
        }

        public string GetVendorID()
        {
            return ((Attributes)attributes).VendorID.ToString("X4");
        }
    }
}
