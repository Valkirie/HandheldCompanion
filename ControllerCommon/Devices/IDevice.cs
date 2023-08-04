using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Devices.Sensors;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using HandheldCompanion;
using WindowsInput.Events;
using static ControllerCommon.OneEuroFilter;
using static ControllerCommon.Utils.DeviceUtils;
using static HandheldCompanion.OpenLibSys;

namespace ControllerCommon.Devices;

[Flags]
public enum DeviceCapacities : ushort
{
    None = 0,
    InternalSensor = 1,
    ExternalSensor = 2,
    ControllerSensor = 4,
    Trackpads = 8,
    FanControl = 16
}

public struct ECDetails
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
    public delegate void KeyPressedEventHandler(ButtonFlags button);

    public delegate void KeyReleasedEventHandler(ButtonFlags button);

    private static OpenLibSys openLibSys;

    private static IDevice device;

    protected ushort _vid, _pid;

    public Vector3 AccelerationAxis = new(1.0f, 1.0f, 1.0f);

    public SortedDictionary<char, char> AccelerationAxisSwap = new()
    {
        { 'X', 'X' },
        { 'Y', 'Y' },
        { 'Z', 'Z' }
    };

    public Vector3 AngularVelocityAxis = new(1.0f, 1.0f, 1.0f);

    public SortedDictionary<char, char> AngularVelocityAxisSwap = new()
    {
        { 'X', 'X' },
        { 'Y', 'Y' },
        { 'Z', 'Z' }
    };

    public DeviceCapacities Capacities = DeviceCapacities.None;

    // device configurable TDP (down, up)
    public double[] cTDP = { 10, 25 };

    /// <summary>
    /// Optional default TDP to be applied when restoring device default TDP
    /// </summary>
    /// <remarks>
    /// These values represent [Slow, Stamp, Fast]
    /// </remarks>
    public double[] DefaultTDP;

    public ECDetails ECDetails;

    public string ExternalSensorName = string.Empty;

    // device GfxClock frequency limits
    public double[] GfxClock = { 100, 1800 };
    public string InternalSensorName = string.Empty;

    public string ManufacturerName;

    // device nominal TDP (slow, fast)
    public double[] nTDP = { 15, 15, 20 };

    // trigger specific settings
    public List<DeviceChord> OEMChords = new();

    // filter settings
    public OneEuroSettings oneEuroSettings = new(0.002d, 0.008d);

    public string ProductIllustration = "device_generic";
    public string ProductModel = "default";
    public string ProductName;

    // mininum delay before trying to emulate a virtual controller on system resume (milliseconds)
    public short ResumeDelay = 10000;

    // key press delay to use for certain scenarios
    public short KeyPressDelay = 20;

    protected USBDeviceInfo sensor = new();

    public IDevice()
    {
    }

    public IEnumerable<ButtonFlags> OEMButtons => OEMChords.SelectMany(a => a.state.Buttons).Distinct();

    public virtual bool IsOpen => openLibSys is not null;

    public virtual bool IsSupported => true;

    public event KeyPressedEventHandler KeyPressed;

    public event KeyReleasedEventHandler KeyReleased;

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
                    case "AYANEO 2S":
                    case "GEEK 1S":
                        device = new AYANEO2S();
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

                    case "ONEXPLAYER mini A07":
                        device = new OneXPlayerMiniAMD();
                        break;
                }
            }
                break;
            case "ASUSTEK COMPUTER INC.":
            {
                switch (ProductName)
                {
                    // Todo, figure out if theres a diff between Z1 and Z1 extreme versions
                    case "RC71L":
                        device = new ROGAlly();
                        break;
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

    public virtual bool Open()
    {
        if (openLibSys != null)
            return true;

        try
        {
            // initialize OpenLibSys
            openLibSys = new OpenLibSys();

            // Check support library sutatus
            var status = openLibSys.GetStatus();
            switch (status)
            {
                case (uint)OlsStatus.NO_ERROR:
                    break;
                default:
                    LogManager.LogError("Couldn't initialize OpenLibSys. ErrorCode: {0}", status);
                    return false;
            }

            // Check WinRing0 status
            var dllstatus = (OlsDllStatus)openLibSys.GetDllStatus();
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
        if (openLibSys is null)
            return;

        SetFanControl(false);

        openLibSys.Dispose();
        openLibSys = null;
    }

    public virtual bool IsReady()
    {
        return true;
    }

    public virtual void SetKeyPressDelay(HIDmode controllerMode)
    {
        switch (controllerMode)
        {
            default:
                KeyPressDelay = 20;
                break;
        }
    }

    public void PullSensors()
    {
        var gyrometer = Gyrometer.GetDefault();
        var accelerometer = Accelerometer.GetDefault();

        if (gyrometer is not null && accelerometer is not null)
        {
            // check sensor
            var DeviceId = CommonUtils.Between(gyrometer.DeviceId, @"\\?\", @"#{").Replace(@"#", @"\");
            sensor = GetUSBDevice(DeviceId);
            if (sensor is not null)
                InternalSensorName = sensor.Name;

            Capacities |= DeviceCapacities.InternalSensor;
        }
        else if (Capacities.HasFlag(DeviceCapacities.InternalSensor))
        {
            Capacities &= ~DeviceCapacities.InternalSensor;
        }

        var USB = SerialUSBIMU.GetDefault();
        if (USB is not null)
        {
            ExternalSensorName = USB.GetName();

            Capacities |= DeviceCapacities.ExternalSensor;
        }
        else if (Capacities.HasFlag(DeviceCapacities.ExternalSensor))
        {
            Capacities &= ~DeviceCapacities.ExternalSensor;
        }
    }

    public bool RestartSensor()
    {
        if (sensor is null)
            return false;

        string deviceId = sensor.DeviceId.Replace("\\1", string.Empty);
        return LegacyDevcon.Restart(deviceId);
    }

    public string GetButtonName(ButtonFlags button)
    {
        return EnumUtils.GetDescriptionFromEnumValue(button, GetType().Name);
    }

    public virtual void SetFanDuty(double percent)
    {
        if (ECDetails.AddressDuty == 0)
            return;

        var duty = percent * (ECDetails.ValueMax - ECDetails.ValueMin) / 100 + ECDetails.ValueMin;
        var data = Convert.ToByte(duty);

        ECRamDirectWrite(ECDetails.AddressDuty, ECDetails, data);
    }

    public virtual void SetFanControl(bool enable)
    {
        if (ECDetails.AddressControl == 0)
            return;

        var data = Convert.ToByte(enable);
        ECRamDirectWrite(ECDetails.AddressControl, ECDetails, data);
    }

    [Obsolete("ECRamReadByte is deprecated, please use ECRamReadByte with ECDetails instead.")]
    public static byte ECRamReadByte(ushort address)
    {
        try
        {
            return openLibSys.ReadIoPortByte(address);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't read byte from address {0} using OpenLibSys. ErrorCode: {1}", address,
                ex.Message);
            return 0;
        }
    }

    public static byte ECRamReadByte(ushort address, ECDetails details)
    {
        var addr_upper = (byte)((address >> 8) & byte.MaxValue);
        var addr_lower = (byte)(address & byte.MaxValue);

        try
        {
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2E);
            openLibSys.WriteIoPortByte(details.AddressData, 0x11);
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2F);
            openLibSys.WriteIoPortByte(details.AddressData, addr_upper);

            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2E);
            openLibSys.WriteIoPortByte(details.AddressData, 0x10);
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2F);
            openLibSys.WriteIoPortByte(details.AddressData, addr_lower);

            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2E);
            openLibSys.WriteIoPortByte(details.AddressData, 0x12);
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2F);

            return openLibSys.ReadIoPortByte(details.AddressData);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't read to port using OpenLibSys. ErrorCode: {0}", ex.Message);
            return 0;
        }
    }

    public static bool ECRamDirectWrite(ushort address, ECDetails details, byte data)
    {
        var addr_upper = (byte)((address >> 8) & byte.MaxValue);
        var addr_lower = (byte)(address & byte.MaxValue);

        try
        {
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2E);
            openLibSys.WriteIoPortByte(details.AddressData, 0x11);
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2F);
            openLibSys.WriteIoPortByte(details.AddressData, addr_upper);

            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2E);
            openLibSys.WriteIoPortByte(details.AddressData, 0x10);
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2F);
            openLibSys.WriteIoPortByte(details.AddressData, addr_lower);

            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2E);
            openLibSys.WriteIoPortByte(details.AddressData, 0x12);
            openLibSys.WriteIoPortByte(details.AddressRegistry, 0x2F);
            openLibSys.WriteIoPortByte(details.AddressData, data);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't write to port using OpenLibSys. ErrorCode: {0}", ex.Message);
            return false;
        }
    }

    protected void KeyPress(ButtonFlags button)
    {
        KeyPressed?.Invoke(button);
    }

    protected void KeyRelease(ButtonFlags button)
    {
        KeyReleased?.Invoke(button);
    }
}