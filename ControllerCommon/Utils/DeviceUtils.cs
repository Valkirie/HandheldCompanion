using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

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

        private static List<USBDeviceInfo> USBDevices = new();
        public static List<USBDeviceInfo> GetUSBDevices(string DeviceId)
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
                            USBDevices.Add(new USBDeviceInfo(id, name, description));
                        }
                    }
                }
            }
            catch (Exception) { }

            return USBDevices;
        }

        public static List<string> SupportedSensors = new List<string>()
        {
            "BMI160"
        };
    }
}
