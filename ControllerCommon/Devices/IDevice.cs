using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using HandheldCompanion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.OneEuroFilter;
using static ControllerCommon.Utils.DeviceUtils;
using static HandheldCompanion.OpenLibSys;

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

        public string ManufacturerName;
        public string ProductName;

        public string ProductIllustration = "device_generic";
        public string ProductModel = "default";

        public DeviceCapacities Capacities = DeviceCapacities.None;

        public FanDetails FanDetails;
        private static OpenLibSys openLibSys;

        // device nominal TDP (slow, fast)
        public double[] nTDP = { 15, 15, 20 };
        // device configurable TDP (down, up)
        public double[] cTDP = { 10, 25 };
        // device GfxClock frequency limits
        public double[] GfxClock = { 100, 1800 };

        public Vector3 AngularVelocityAxis = new Vector3(1.0f, 1.0f, 1.0f);
        public SortedDictionary<char, char> AngularVelocityAxisSwap = new()
        {
            { 'X', 'X' },
            { 'Y', 'Y' },
            { 'Z', 'Z' },
        };

        public Vector3 AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
        public SortedDictionary<char, char> AccelerationAxisSwap = new()
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
                            case "AB05-AMD":
                                device = new AYANEOAIRPlus();
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
                            case "G1618-04":
                                device = new GPDWin4();
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

            LogManager.LogInformation("{0} from {1}", ProductName, ManufacturerName);

            if (device is null)
            {
                device = new DefaultDevice();
                LogManager.LogWarning("Device not yet supported. The behavior of the application will be unpredictable");
            }

            // get the actual handheld device
            device.ManufacturerName = ManufacturerName;
            device.ProductName = ProductName;

            return device;
        }

        public virtual bool IsOpen
        {
            get { return openLibSys is not null; }
        }

        public virtual bool IsSupported
        {
            get
            {
                return true;
            }
        }

        public virtual bool Open()
        {
            if (openLibSys != null)
                return true;

            try
            {
                // initialize OpenLibSys
                openLibSys = new OpenLibSys();

                // Check support library sutatus
                OlsStatus status = openLibSys.GetStatus();
                switch (status)
                {
                    case (uint)OlsStatus.NO_ERROR:
                        break;
                    default:
                        LogManager.LogError("Couldn't initialize OpenLibSys. ErrorCode: {0}", status);
                        return false;
                }

                // Check WinRing0 status
                OlsDllStatus dllstatus = (OlsDllStatus)openLibSys.GetDllStatus();
                switch (dllstatus)
                {
                    case (uint)OlsDllStatus.OLS_DLL_NO_ERROR:
                        break;
                    default:
                        LogManager.LogError("Couldn't initialize OpenLibSys. ErrorCode: {0}", dllstatus);
                        return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't initialize OpenLibSys. ErrorCode: {0}", ex.Message);
                Close();
                return false;
            }

            return true;
        }

        public virtual void Close()
        {
            SetFanControl(false);

            openLibSys.Dispose();
            openLibSys = null;
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

        public string GetButtonName(ButtonFlags button)
        {
            return EnumUtils.GetDescriptionFromEnumValue(button, this.GetType().Name);
        }

        public virtual void SetFanDuty(double percent)
        {
            if (FanDetails.AddressDuty == 0)
                return;

            double duty = percent * (FanDetails.ValueMax - FanDetails.ValueMin) / 100 + FanDetails.ValueMin;
            byte data = Convert.ToByte(duty);

            ECRamDirectWrite(FanDetails.AddressDuty, FanDetails, data);
        }

        public virtual void SetFanControl(bool enable)
        {
            if (FanDetails.AddressControl == 0)
                return;

            byte data = Convert.ToByte(enable);
            ECRamDirectWrite(FanDetails.AddressControl, FanDetails, data);
        }

        public static bool ECRamDirectWrite(ushort address, byte data)
        {
            try
            {
                openLibSys.WriteIoPortByte(address, data);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't write to port using OpenLibSys. ErrorCode: {0}", ex.Message);
                return false;
            }
        }

        public static bool ECRamDirectWrite(ushort address, FanDetails details, byte data)
        {
            byte addr_upper = ((byte)(address >> 8 & byte.MaxValue));
            byte addr_lower = ((byte)(address & byte.MaxValue));

            try
            {
                openLibSys.WriteIoPortByte(details.AddressRegistry, (byte)46);
                openLibSys.WriteIoPortByte(details.AddressData, (byte)17);
                openLibSys.WriteIoPortByte(details.AddressRegistry, (byte)47);
                openLibSys.WriteIoPortByte(details.AddressData, addr_upper);

                openLibSys.WriteIoPortByte(details.AddressRegistry, (byte)46);
                openLibSys.WriteIoPortByte(details.AddressData, (byte)16);
                openLibSys.WriteIoPortByte(details.AddressRegistry, (byte)47);
                openLibSys.WriteIoPortByte(details.AddressData, addr_lower);

                openLibSys.WriteIoPortByte(details.AddressRegistry, (byte)46);
                openLibSys.WriteIoPortByte(details.AddressData, (byte)18);
                openLibSys.WriteIoPortByte(details.AddressRegistry, (byte)47);
                openLibSys.WriteIoPortByte(details.AddressData, data);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't write to port using OpenLibSys. ErrorCode: {0}", ex.Message);
                return false;
            }
        }
    }
}