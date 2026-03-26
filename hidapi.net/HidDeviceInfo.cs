using hidapi.Native;
using System;
using System.Runtime.InteropServices;

namespace hidapi
{
    public class HidDeviceInfo
    {
        private HidDeviceInfoStruct _infoStruct;

        public HidDeviceInfo(IntPtr deviceInfo)
        {
            _infoStruct = Marshal.PtrToStructure<HidDeviceInfoStruct>(deviceInfo);
        }

        public IntPtr PathPtr => _infoStruct.path;
        public string Path => Marshal.PtrToStringAnsi(_infoStruct.path);
        public IntPtr SerialNumberPtr => _infoStruct.serial_number;
        public string SerialNumber => Marshal.PtrToStringUni(_infoStruct.serial_number);
        public IntPtr ManufacturerPtr => _infoStruct.manufacturer_string;
        public string Manufacturer => Marshal.PtrToStringUni(_infoStruct.manufacturer_string);
        public IntPtr ProductPtr => _infoStruct.product_string;
        public string Product => Marshal.PtrToStringUni(_infoStruct.product_string);

        public IntPtr NextDevicePtr => _infoStruct.next;
        public HidDeviceInfo NextDevice => _infoStruct.next != IntPtr.Zero ? new HidDeviceInfo(_infoStruct.next) : null;

    }
}
