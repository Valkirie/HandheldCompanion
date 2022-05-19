using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
            public ushort VendorId;
            [MarshalAs(UnmanagedType.U2)]
            public ushort ProductId;
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
            static string vidPattern = @"VID_([0-9A-F]{4})";
            static string pidPattern = @"PID_([0-9A-F]{4})";

            public string DeviceId { get; set; } = "0";
            public string Name { get; set; } = "N/A";
            public string Description { get; set; } = "N/A";
            public string Caption { get; set; } = "N/A";
            public string PID { get; set; } = "0x00";
            public string VID { get; set; } = "0x00";

            public USBDeviceInfo(string deviceId = "0", string name = "N/A", string description = "N/A", string caption = "N/A", string pid = "0x00", string vid = "0x00")
            {
                DeviceId = deviceId;
                Name = name;
                Description = description;
                Caption = caption;
                PID = pid;
                VID = vid;
            }

            public USBDeviceInfo(ManagementBaseObject device)
            {
                DeviceId = device.GetPropertyValue("DeviceId").ToString();
                Name = device.GetPropertyValue("Name").ToString();
                Description = device.GetPropertyValue("Description").ToString();
                Caption = device.GetPropertyValue("Caption").ToString();

                Match mVID = Regex.Match(DeviceId, vidPattern, RegexOptions.IgnoreCase);
                Match mPID = Regex.Match(DeviceId, pidPattern, RegexOptions.IgnoreCase);

                if (mVID.Success)
                    VID = mVID.Groups[1].Value;
                if (mPID.Success)
                    PID = mPID.Groups[1].Value;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public static USBDeviceInfo GetUSBDevice(string DeviceId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT * From Win32_PnPEntity WHERE DeviceId LIKE '%{DeviceId}%'"))
                {
                    var devices = searcher.Get().Cast<ManagementBaseObject>().ToList();
                    return new USBDeviceInfo(devices.FirstOrDefault());
                }
            }
            catch (Exception) { }

            return null;
        }

        public static List<USBDeviceInfo> GetSerialDevices()
        {
            List<USBDeviceInfo> serials = new List<USBDeviceInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%' AND PNPClass = 'Ports'"))
                {
                    var devices = searcher.Get().Cast<ManagementBaseObject>().ToList();
                    foreach (var device in devices)
                        serials.Add(new USBDeviceInfo(device));
                }
            }
            catch (Exception) { }

            return serials;
        }
    }
}
