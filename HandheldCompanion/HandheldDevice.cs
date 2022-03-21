using ControllerCommon;
using ControllerCommon.Utils;
using System.Diagnostics;
using System.Linq;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.ProcessUtils;

namespace HandheldCompanion
{
    public class HandheldDevice
    {
        public USBDeviceInfo sensor;
        public bool sensorSupported;

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

            sensor = GetUSBDevices().FirstOrDefault(device => device.DeviceId.Contains(ACPI));

            if (sensor != null)
            {
                switch (sensor.Name)
                {
                    case "BMI160":
                        sensorSupported = true;
                        break;
                }
            }
        }
    }
}
