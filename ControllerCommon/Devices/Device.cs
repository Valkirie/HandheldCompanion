using ControllerCommon.Utils;
using HidSharp;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Devices
{
    public abstract class Device
    {
        protected USBDeviceInfo sensor = new USBDeviceInfo("0", "N/A", "");
        public string sensorName = "N/A";
        public bool sensorSupported = false;

        public bool controllerSupported = false;

        public string ManufacturerName;
        public string ProductName;

        protected Gyrometer gyrometer;
        public bool hasGyrometer;

        protected Accelerometer accelerometer;
        public bool hasAccelerometer;

        protected Inclinometer inclinometer;
        public bool hasInclinometer;

        public double WidthHeightRatio = 1.0d;

        protected Device(string ManufacturerName, string ProductName)
        {
            this.ManufacturerName = ManufacturerName;
            this.ProductName = ProductName;

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
                {
                    sensorName = sensor.Name;
                    if (SupportedSensors.Contains(sensor.Name))
                        sensorSupported = true;
                }
            }

            controllerSupported = true;
        }
    }
}
