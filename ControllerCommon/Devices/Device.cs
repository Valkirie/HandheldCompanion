using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using System.Collections.Generic;
using System.Numerics;
using Windows.Devices.Sensors;
using WindowsInput.Events;
using static ControllerCommon.OneEuroFilter;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Devices
{
    public class DeviceChord
    {
        public string name;
        public List<KeyCode> chord;

        public DeviceChord(string name, List<KeyCode> chord)
        {
            this.name = name;
            this.chord = chord;
        }
    }

    public abstract class Device
    {
        protected USBDeviceInfo sensor = new USBDeviceInfo();
        public string InternalSensorName = "N/A";
        public string ExternalSensorName = "N/A";
        public bool ProductSupported = false;

        public string ManufacturerName;
        public string ProductName;

        public string ProductIllustration = "device_generic";
        public string ProductModel = "default";

        public bool hasInternal;
        public bool hasExternal;

        // device specific settings
        public float WidthHeightRatio = 1.0f;
        public double DefaultTDP = 15;

        public Vector3 AngularVelocityAxis = new Vector3(1.0f, 1.0f, 1.0f);
        public Dictionary<char, char> AngularVelocityAxisSwap = new()
        {
            { 'X', 'X' },
            { 'Y', 'Y' },
            { 'Z', 'Z' },
        };

        public Vector3 AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
        public Dictionary<char, char> AccelerationAxisSwap = new()
        {
            { 'X', 'X' },
            { 'Y', 'Y' },
            { 'Z', 'Z' },
        };

        // filter settings
        public OneEuroSettings oneEuroSettings = new OneEuroSettings(0.002d, 0.008d);

        // trigger specific settings
        public List<DeviceChord> listeners = new();

        private static Device device;
        public static Device GetDefault()
        {
            if (device != null)
                return device;

            var ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
            var ProductName = MotherboardInfo.Product;
            var SystemName = MotherboardInfo.SystemName;
            var Version = MotherboardInfo.Version;

            switch (ManufacturerName)
            {
                case "AYADEVICE":
                case "AYANEO":
                    {
                        switch (ProductName)
                        {
                            case "AIR":
                            case "AIR Pro":
                            case "AIR Lite":
                                device = new AYANEOAIR();
                                break;
                            case "AYA NEO FOUNDER":
                            case "AYANEO 2021":
                            case "AYANEO 2021 Pro":
                            case "AYANEO 2021 Pro Retro Power":
                                device = new AYANEO2021();
                                break;
                            case "NEXT Pro":
                            case "NEXT Advance":
                            case "NEXT":
                                device = new AYANEONEXT();
                                break;
                        }
                    }
                    break;

                case "GPD":
                    {
                        switch (ProductName)
                        {
                            case "G1619-03":
                                device = new GPDWinMax2();
                                break;
                        }
                    }
                    break;

                case "ONE-NETBOOK TECHNOLOGY CO., LTD.":
                    {
                        switch (ProductName)
                        {
                            case "ONE XPLAYER":
                                {
                                    switch (Version)
                                    {
                                        default:
                                        case "V01":
                                            device = new OneXPlayerMiniAMD();
                                            break;
                                        case "1002-C":
                                            device = new OneXPlayerMiniIntel();
                                            break;
                                    }
                                    break;
                                }
                        }
                    }
                    break;
            }

            if (device is null)
            {
                device = new DefaultDevice();
                LogManager.LogWarning("{0} from {1} is not yet supported. The behavior of the application will be unpredictable.", ProductName, ManufacturerName);
            }

            // get the actual handheld device
            device.ManufacturerName = ManufacturerName;
            device.ProductName = ProductName;

            return device;
        }

        public void PullSensors()
        {
            var gyrometer = Gyrometer.GetDefault();
            var accelerometer = Accelerometer.GetDefault();

            if (gyrometer != null && accelerometer != null)
            {
                // check sensor
                string DeviceId = CommonUtils.Between(gyrometer.DeviceId, @"\\?\", @"#{").Replace(@"#", @"\");
                sensor = GetUSBDevice(DeviceId);
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
            if (USB != null)
            {
                ExternalSensorName = USB.GetName();
                hasExternal = true;
            }
            else
            {
                ExternalSensorName = "N/A";
                hasExternal = false;
            }
        }
    }
}
