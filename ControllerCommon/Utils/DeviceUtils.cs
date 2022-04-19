using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace ControllerCommon.Utils
{
    public class DeviceUtils
    {
        #region imports
        [StructLayout(LayoutKind.Explicit)]
        public struct XInputGamepad
        {
            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(0)]
            public short wButtons;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(2)]
            public byte bLeftTrigger;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(3)]
            public byte bRightTrigger;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(4)]
            public short sThumbLX;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(6)]
            public short sThumbLY;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(8)]
            public short sThumbRX;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(10)]
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputVibration
        {
            [MarshalAs(UnmanagedType.I2)]
            public ushort LeftMotorSpeed;

            [MarshalAs(UnmanagedType.I2)]
            public ushort RightMotorSpeed;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct XInputCapabilities
        {
            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(0)]
            byte Type;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(1)]
            public byte SubType;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(2)]
            public short Flags;

            [FieldOffset(4)]
            public XInputGamepad Gamepad;

            [FieldOffset(16)]
            public XInputVibration Vibration;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputCapabilitiesEx
        {
            public XInputCapabilities Capabilities;
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 VID;
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 PID;
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 REV;
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 XID;
        };

        [DllImport("xinput1_4.dll", EntryPoint = "#108")]
        public static extern int XInputGetCapabilitiesEx
        (
            int a1,            // [in] unknown, should probably be 1
            int dwUserIndex,   // [in] Index of the gamer associated with the device
            int dwFlags,       // [in] Input flags that identify the device type
            ref XInputCapabilitiesEx pCapabilities  // [out] Receives the capabilities
        );
        #endregion

        public class USBDeviceInfo
        {
            public USBDeviceInfo(string deviceId, string name, string description)
            {
                DeviceId = deviceId;
                Name = name;
                Description = description;
            }

            public string DeviceId { get; }
            public string Name { get; }
            public string Description { get; }

            public override string ToString()
            {
                return Name;
            }
        }

        public static USBDeviceInfo GetUSBDevice(string DeviceId)
        {
            try
            {
                using (var mos = new ManagementObjectSearcher($"Select * From Win32_PnPEntity WHERE DeviceId LIKE '%{DeviceId}%'"))
                {
                    using (ManagementObjectCollection collection = mos.Get())
                    {
                        foreach (var device in collection)
                        {

                            var id = device.GetPropertyValue("DeviceId").ToString();
                            var name = device.GetPropertyValue("Name").ToString();
                            var description = device.GetPropertyValue("Description").ToString();
                            return new USBDeviceInfo(id, name, description);
                        }
                    }
                }
            }
            catch (Exception) { }

            return null;
        }
    }
}
