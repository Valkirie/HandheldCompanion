using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using System.Collections.Generic;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.OneEuroFilter;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Devices
{
    public abstract class Device
    {
        protected USBDeviceInfo sensor = new USBDeviceInfo();
        public string InternalSensorName = string.Empty;
        public string ExternalSensorName = string.Empty;
        public bool ProductSupported = false;

        public string ManufacturerName;
        public string ProductName;

        public string ProductIllustration = "device_generic";
        public string ProductModel = "default";

        public Dictionary<SensorFamily, bool> hasSensors = new()
        {
            { SensorFamily.Windows, false },
            { SensorFamily.SerialUSBIMU, false },
            { SensorFamily.Controller, false },
        };

        // device nominal TDP (slow, fast)
        public double[] nTDP = { 15, 15, 20 };
        // device configurable TDP (down, up)
        public double[] cTDP = { 10, 25 };
        // device GfxClock frequency limits
        public double[] GfxClock = { 100, 1800 };

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
                case "AOKZOE":
                    {
                        switch (ProductName)
                        {
                            case "AOKZOE A1 AR07":
                                device = new AOKZOEA1();
                                break;
                        }
                    }
                    break;
                case "AYADEVICE":
                case "AYANEO":
                    {
                        switch (ProductName)
                        {
                            case "AIR":
                                device = new AYANEOAIR();
                                break;
                            case "AIR Pro":
                                device = new AYANEOAIRPro();
                                break;
                            case "AIR Lite":
                                device = new AYANEOAIRLite();
                                break;
                            case "AYA NEO FOUNDER":
                            case "AYANEO 2021":
                                device = new AYANEO2021();
                                break;
                            case "AYANEO 2021 Pro":
                            case "AYANEO 2021 Pro Retro Power":
                                device = new AYANEO2021Pro();
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
                            case "G1618-03":
                                device = new GPDWin3();
                                break;
                            case "G1619-03":
                                device = new GPDWinMax2Intel();
                                break;
                            case "G1619-04":
                                device = new GPDWinMax2AMD();
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

                case "VALVE":
                    {
                        switch (ProductName)
                        {
                            case "Jupiter":
                                device = new SteamDeck();
                                break;
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

                hasSensors[SensorFamily.Windows] = true;
            }

            var USB = SerialUSBIMU.GetDefault();
            if (USB != null)
            {
                ExternalSensorName = USB.GetName();
                hasSensors[SensorFamily.SerialUSBIMU] = true;
            }
        }
    }
}
