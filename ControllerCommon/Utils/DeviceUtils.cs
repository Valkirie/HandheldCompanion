using System;
using System.Collections.Generic;
using System.Management;

namespace ControllerCommon.Utils
{
    public class DeviceUtils
    {
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

        public static List<string> SupportedSensors = new List<string>()
        {
            "BMI160"
        };
    }
}
