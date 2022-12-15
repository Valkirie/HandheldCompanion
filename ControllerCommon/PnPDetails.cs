using ControllerCommon.Managers.Hid;
using System;
using System.Runtime.InteropServices;

namespace ControllerCommon
{
    [StructLayout(LayoutKind.Sequential)]
    public class PnPDetails
    {
        public string FriendlyName;
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
            return "0x" + attributes.ProductID.ToString("X4");
        }

        public string GetVendorID()
        {
            return "0x" + attributes.VendorID.ToString("X4");
        }
    }
}
