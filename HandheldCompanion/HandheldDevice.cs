using ControllerCommon;
using ControllerCommon.Utils;
using System.Diagnostics;
using System.Linq;
using Windows.Devices.Sensors;

namespace HandheldCompanion
{
    public class HandheldDevice
    {
        public bool sensorSupported;
        public string sensorName;

        public string ManufacturerName;
        public string ProductName;

        public bool hasGyrometer;
        public bool hasAccelerometer;
        public bool hasInclinometer;

        public HandheldDevice()
        {
            ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
            ProductName = MotherboardInfo.Product;

            Gyrometer gyrometer = Gyrometer.GetDefault();
            if (gyrometer != null)
                hasGyrometer = true;

            Accelerometer accelerometer = Accelerometer.GetDefault();
            if (accelerometer != null)
                hasAccelerometer = true;

            Inclinometer inclinometer = Inclinometer.GetDefault();
            if (inclinometer != null)
                hasInclinometer = true;

            Debug.WriteLine("DeviceId: {0}", gyrometer.DeviceId);
            string ACPI = CommonUtils.Between(gyrometer.DeviceId, "ACPI#", "#");

            ProcessUtils.USBDeviceInfo Sensor = ProcessUtils.GetUSBDevices().Where(device => device.DeviceId.Contains(ACPI)).FirstOrDefault();

            if (Sensor != null)
            {
                sensorName = Sensor.Name;
                switch (Sensor.Name)
                {
                    case "BMI160":
                        sensorSupported = true;
                        break;
                }
            }
        }
    }
}
