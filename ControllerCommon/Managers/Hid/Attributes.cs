using System;
using System.Runtime.InteropServices;

namespace ControllerCommon.Managers.Hid
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Attributes
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public short VersionNumber;
    }

    public static class GetAttributes
    {
        [DllImport("hid.dll", EntryPoint = "HidD_GetAttributes")]
        static internal extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref Attributes attributes);

        public static Attributes? Get(IntPtr handle)
        {
            var deviceAttributes = new Attributes();
            deviceAttributes.Size = Marshal.SizeOf(deviceAttributes);
            if (HidD_GetAttributes(handle, ref deviceAttributes))
                return deviceAttributes;
            else
                return null;
        }
    }
}
