using ControllerCommon.Utils;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Devices
{
    public abstract class Device
    {
        protected USBDeviceInfo sensor = new USBDeviceInfo();
        public string InternalSensorName = "N/A";
        public string ExternalSensorName = "N/A";
        public bool ProductSupported = false;

        public string ManufacturerName;
        public string ProductName;
        public string ProductIllustration = "device_generic";

        public bool hasInternal;
        public bool hasExternal;

        // device specific settings
        public float WidthHeightRatio = 1.0f;
        public Vector3 AngularVelocityAxis = new Vector3(1.0f, 1.0f, 1.0f);
        public Vector3 AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);

        protected Device()
        {
        }

        public void Initialize(string ManufacturerName, string ProductName)
        {
            this.ManufacturerName = ManufacturerName;
            this.ProductName = ProductName;
        }

        public void PullSensors()
        {
            var gyrometer = Gyrometer.GetDefault();
            var accelerometer = Accelerometer.GetDefault();

            if (gyrometer != null && accelerometer != null)
            {
                // check sensor
                string ACPI = CommonUtils.Between(gyrometer.DeviceId, "ACPI#", "#");
                sensor = GetUSBDevice(ACPI);
                if (sensor != null)
                    InternalSensorName = sensor.Name;

                hasInternal = true;
            }
            else
            {
                InternalSensorName = "N/A";
                hasInternal = false;
            }

            var USB = SerialUSBIMU.GetDefault();
            if (USB != null && USB.device != null)
            {
                ExternalSensorName = USB.device.Name;
                hasExternal = true;
            }
            else
            {
                ExternalSensorName = "N/A";
                hasExternal = false;
            }
        }

        public PipeServerHandheld ToPipe()
        {
            // refresh sensors status
            PullSensors();

            return new PipeServerHandheld()
            {
                ManufacturerName = ManufacturerName,
                ProductName = ProductName,
                ProductIllustration = ProductIllustration,

                InternalSensorName = InternalSensorName,
                ExternalSensorName = ExternalSensorName,
                ProductSupported = ProductSupported,

                hasInternal = hasInternal,
                hasExternal = hasExternal
            };
        }
    }
}
