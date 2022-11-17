using ControllerCommon.Managers.Hid;
using System;
using System.Runtime.InteropServices;

namespace ControllerCommon
{
    [StructLayout(LayoutKind.Sequential)]
    public class PnPDetails
    {
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
    }
}
