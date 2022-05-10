using ControllerCommon.Utils;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Devices
{
    public abstract class Device
    {
        protected USBDeviceInfo sensor = new USBDeviceInfo("0", "N/A", "");
        public string sensorName = "N/A";
        public bool ProductSupported = false;

        public string ManufacturerName;
        public string ProductName;
        public string ProductIllustration = "device_generic";

        protected Gyrometer gyrometer;
        public bool hasGyrometer;

        protected Accelerometer accelerometer;
        public bool hasAccelerometer;

        protected Inclinometer inclinometer;
        public bool hasInclinometer;

        // device specific settings
        public float WidthHeightRatio = 1.0f;
        public Vector3 AngularVelocityAxis = new Vector3(1.0f, 1.0f, 1.0f);
        public Vector3 AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);

        protected Device()
        {
            gyrometer = Gyrometer.GetDefault();
            if (gyrometer != null)
                hasGyrometer = true;

            accelerometer = Accelerometer.GetDefault();
            if (accelerometer != null)
                hasAccelerometer = true;

            inclinometer = Inclinometer.GetDefault();
            if (inclinometer != null)
                hasInclinometer = true;

            if (hasGyrometer)
            {
                // check sensor
                string ACPI = CommonUtils.Between(gyrometer.DeviceId, "ACPI#", "#");
                sensor = GetUSBDevice(ACPI);
                if (sensor != null)
                    sensorName = sensor.Name;
            }
        }
    }
}
