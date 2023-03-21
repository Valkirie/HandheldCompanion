using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.OneEuroFilter;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Devices
{
    [Flags]
    public enum DeviceCapacities : ushort
    {
        None = 0,
        InternalSensor = 1,
        ExternalSensor = 2,
        ControllerSensor = 4,
        Trackpads = 8,
        FanControl = 16,
    }

    public struct FanDetails
    {
        public ushort AddressRegistry;
        public ushort AddressData;
        public ushort AddressControl;
        public ushort AddressDuty;

        public short ValueMin;
        public short ValueMax;
    }

    public abstract class IDevice
    {
        protected USBDeviceInfo sensor = new USBDeviceInfo();
        public string InternalSensorName = string.Empty;
        public string ExternalSensorName = string.Empty;
        public bool ProductSupported = false;

        public string ManufacturerName;
        public string ProductName;

        public string ProductIllustration = "device_generic";
        public string ProductModel = "default";

        public DeviceCapacities Capacities = DeviceCapacities.None;
        public FanDetails FanDetails;

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
        public List<DeviceChord> OEMChords = new();
        public IEnumerable<ButtonFlags> OEMButtons => OEMChords.SelectMany(a => a.state.Buttons).Distinct();

        private static IDevice device;
        public static IDevice GetDefault()
        {
            if (device is not null)
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
                            case "AYANEO 2":
                            case "GEEK":
                                device = new AYANEO2();
                                break;
                        }
                    }
                    break;

                case "GPD":
                    {
                        switch (ProductName)
                        {
                            case "WIN2":
                                device = new GPDWin2();
                                break;
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
                case "ONE-NETBOOK":
                    {
                        switch (ProductName)
                        {
                            case "ONE XPLAYER":
                            case "ONEXPLAYER Mini Pro":
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
                                        case "V03":
                                            device = new OneXPlayerMiniPro();
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
                LogManager.LogWarning("{0} from {1} is not yet supported. The behavior of the application will be unpredictable", ProductName, ManufacturerName);
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

            if (gyrometer is not null && accelerometer is not null)
            {
                // check sensor
                string DeviceId = CommonUtils.Between(gyrometer.DeviceId, @"\\?\", @"#{").Replace(@"#", @"\");
                sensor = GetUSBDevice(DeviceId);
                if (sensor is not null)
                    InternalSensorName = sensor.Name;

                Capacities |= DeviceCapacities.InternalSensor;
            }
            else if (Capacities.HasFlag(DeviceCapacities.InternalSensor))
                Capacities &= ~DeviceCapacities.InternalSensor;

            var USB = SerialUSBIMU.GetDefault();
            if (USB is not null)
            {
                ExternalSensorName = USB.GetName();

                Capacities |= DeviceCapacities.ExternalSensor;
            }
            else if (Capacities.HasFlag(DeviceCapacities.ExternalSensor))
                Capacities &= ~DeviceCapacities.ExternalSensor;
        }

        public IDevice()
        {
            // OEMChords.Add(new DeviceChord("temp", new List<KeyCode>() { KeyCode.F1 }, new List<KeyCode>() { KeyCode.F1 }, false, ButtonFlags.OEM1));
        }

        public string GetButtonName(ButtonFlags button)
        {
            return EnumUtils.GetDescriptionFromEnumValue(button, this.GetType().Name);
        }
    }
}