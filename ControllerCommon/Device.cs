using System.Collections.Generic;

namespace ControllerCommon
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Device
    {
        public bool present { get; set; }
        public bool gamingDevice { get; set; }
        public string symbolicLink { get; set; }
        public string vendor { get; set; }
        public string product { get; set; }
        public string serialNumber { get; set; }
        public string usage { get; set; }
        public string description { get; set; }
        public string deviceInstancePath { get; set; }
        public string baseContainerDeviceInstancePath { get; set; }
        public string baseContainerClassGuid { get; set; }
        public int baseContainerDeviceCount { get; set; }
    }

    public class RootDevice
    {
        public string friendlyName { get; set; }
        public List<Device> devices { get; set; }
    }
}
