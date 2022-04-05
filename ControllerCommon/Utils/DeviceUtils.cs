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

        public static List<USBDeviceInfo> GetUSBDevices()
        {
            var devices = new List<USBDeviceInfo>();

            using (var mos = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
            {
                using (ManagementObjectCollection collection = mos.Get())
                {
                    foreach (var device in collection)
                    {
                        try
                        {
                            var id = device.GetPropertyValue("DeviceId").ToString();
                            var name = device.GetPropertyValue("Name").ToString();
                            var description = device.GetPropertyValue("Description").ToString();
                            devices.Add(new USBDeviceInfo(id, name, description));
                        }
                        catch (Exception ex) { }
                    }
                }
            }

            return devices;
        }

        public static List<string> SupportedSensors = new List<string>()
        {
            "BMI160"
        };
    }
}
