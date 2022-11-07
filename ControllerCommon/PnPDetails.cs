using ControllerCommon.Managers.Hid;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PnPDetails
    {
        public string Manufacturer;
        public string DeviceDesc;

        public bool isVirtual;
        public bool isGaming;
        public bool isAttributed;
        public DateTimeOffset arrivalDate;

        public string deviceInstancePath;
        public string baseContainerDeviceInstancePath;

        public Attributes attributes;
        public Capabilities capabilities;
    }
}
