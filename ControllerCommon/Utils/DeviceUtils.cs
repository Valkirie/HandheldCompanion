using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace ControllerCommon.Utils
{
    public class DeviceUtils
    {
        public enum SensorFamily
        {
            None = 0,
            Windows = 1,
            SerialUSBIMU = 2,
            Controller = 3
        }

        public static USBDeviceInfo GetUSBDevice(string DeviceId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT * From Win32_PnPEntity WHERE DeviceId = '{DeviceId.Replace("\\", "\\\\")}'"))
                {
                    var devices = searcher.Get().Cast<ManagementBaseObject>().ToList();
                    return new USBDeviceInfo(devices.FirstOrDefault());
                }
            }
            catch { }

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
            catch { }

            return serials;
        }
    }
}
