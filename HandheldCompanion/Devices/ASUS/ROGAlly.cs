using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;
using Task = System.Threading.Tasks.Task;

namespace HandheldCompanion.Devices;

public class ROGAlly : IDevice
{
    private readonly Dictionary<byte, ButtonFlags> keyMapping = new()
    {
        { 0, ButtonFlags.None },
        { 166, ButtonFlags.OEM1 },
        { 56, ButtonFlags.OEM2 },
        { 165, ButtonFlags.OEM3 },
        { 167, ButtonFlags.OEM4 },
        { 168, ButtonFlags.OEM4 },
    };

    private Dictionary<byte, HidDevice> hidDevices = new();
    private AsusACPI asusACPI;

    private const byte INPUT_HID_ID = 0x5a;
    private const byte AURA_HID_ID = 0x5d;
    private const int ASUS_ID = 0x0b05;

    public static readonly byte[] LED_INIT1 = new byte[] { AURA_HID_ID, 0xb9 };
    public static readonly byte[] LED_INIT2 = Encoding.ASCII.GetBytes("]ASUS Tech.Inc.");
    public static readonly byte[] LED_INIT3 = new byte[] { AURA_HID_ID, 0x05, 0x20, 0x31, 0, 0x1a };
    public static readonly byte[] LED_INIT4 = Encoding.ASCII.GetBytes("^ASUS Tech.Inc.");
    public static readonly byte[] LED_INIT5 = new byte[] { 0x5e, 0x05, 0x20, 0x31, 0, 0x1a };

    static byte[] MESSAGE_APPLY = { AURA_HID_ID, 0xb4 };
    static byte[] MESSAGE_SET = { AURA_HID_ID, 0xb5, 0, 0, 0 };

    public override bool IsOpen => hidDevices.ContainsKey(INPUT_HID_ID) && hidDevices[INPUT_HID_ID].IsOpen && asusACPI is not null && asusACPI.IsOpen();

    private enum AuraMode
    {
        SolidColor = 0,
        Breathing = 1,
        Wheel = 2,
        Rainbow = 3,
        Wave = 4,
    }

    private enum AuraSpeed
    {
        Slow = 0xeb,
        Medium = 0xf5,
        Fast = 0xe1,
    }

    private enum AuraDirection
    {
        Forward = 0,
        Reverse = 1,
    }

    private enum LEDZone
    {
        All = 0,
        JoystickLeftSideLeft = 1,
        JoystickLeftSideRight = 2,
        JoystickRightSideLeft = 3,
        JoystickRightSideRight = 4,
    }

    public ROGAlly()
    {
        // device specific settings
        ProductIllustration = "device_rog_ally";

        // used to monitor OEM specific inputs
        _vid = 0x0B05;
        _pid = 0x1ABE;

        // https://www.amd.com/en/products/apu/amd-ryzen-z1
        // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2700 };

        AngularVelocityAxis = new Vector3(-1.0f, 1.0f, 1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Breathing;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;
        // DynamicLightingCapabilities |= LEDLevel.Wave;
        DynamicLightingCapabilities |= LEDLevel.Wheel;
        DynamicLightingCapabilities |= LEDLevel.Ambilight;

        powerProfileQuiet = new(Properties.Resources.PowerProfileSilentName, Properties.Resources.PowerProfileSilentDescription)
        {
            Default = true,
            OEMPowerMode = (int)AsusMode.Silent,
            OSPowerMode = PowerMode.BetterBattery,
            Guid = PowerMode.BetterBattery
        };

        powerProfileBalanced = new(Properties.Resources.PowerProfilePerformanceName, Properties.Resources.PowerProfilePerformanceDescription)
        {
            Default = true,
            OEMPowerMode = (int)AsusMode.Performance,
            OSPowerMode = PowerMode.BetterPerformance,
            Guid = PowerMode.BetterPerformance
        };

        powerProfileCool = new(Properties.Resources.PowerProfileTurboName, Properties.Resources.PowerProfileTurboDescription)
        {
            Default = true,
            OEMPowerMode = (int)AsusMode.Turbo,
            OSPowerMode = PowerMode.BestPerformance,
            Guid = PowerMode.BestPerformance
        };

        OEMChords.Add(new DeviceChord("CC",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("AC",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("M1/M2",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM3
        ));
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;        

        // try open asus ACPI
        asusACPI = new AsusACPI();
        if (asusACPI is null)
            return false;

        return true;
    }

    public override void Close()
    {
        // close Asus ACPI
        if (asusACPI is not null)
            asusACPI.Close();

        // close devices
        foreach(HidDevice hidDevice in hidDevices.Values)
            hidDevice.CloseDevice();        

        base.Close();
    }

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (device.ReadFeatureData(out byte[] data, INPUT_HID_ID))
            {
                device.OpenDevice();
                device.MonitorDeviceEvents = true;

                hidDevices[INPUT_HID_ID] = device;

                Task<HidReport> ReportDevice = Task.Run(async () => await device.ReadReportAsync());
                ReportDevice.ContinueWith(t => OnReport(ReportDevice.Result, device));
            }
            else if (device.ReadFeatureData(out data, AURA_HID_ID))
            {
                hidDevices[AURA_HID_ID] = device;
            }
        }

        hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice hidDevice);
        if (hidDevice is null || !hidDevice.IsConnected)
            return false;

        PnPDevice pnpDevice = PnPDevice.GetDeviceByInterfaceId(hidDevice.DevicePath);
        string device_parent = pnpDevice.GetProperty<string>(DevicePropertyKey.Device_Parent);

        PnPDevice pnpParent = PnPDevice.GetDeviceByInstanceId(device_parent);
        Guid parent_guid = pnpParent.GetProperty<Guid>(DevicePropertyKey.Device_ClassGuid);
        string parent_instanceId = pnpParent.GetProperty<string>(DevicePropertyKey.Device_InstanceId);

        return DeviceHelper.IsDeviceAvailable(parent_guid, parent_instanceId);
    }

    private void OnReport(HidReport result, HidDevice device)
    {
        Task<HidReport> ReportDevice = Task.Run(async () => await device.ReadReportAsync());
        ReportDevice.ContinueWith(t => OnReport(ReportDevice.Result, device));

        // get key
        byte key = result.Data[0];
        HandleEvent(key);
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        if (!IsOpen)
            return;

        switch (enable)
        {
            case false:
                asusACPI.DeviceSet(AsusACPI.PerformanceMode, mode);
                return;
        }
    }

    public override void SetFanDuty(double percent)
    {
        if (!IsOpen)
            return;

        asusACPI.SetFanSpeed(AsusFan.CPU, Convert.ToByte(percent));
        asusACPI.SetFanSpeed(AsusFan.GPU, Convert.ToByte(percent));
    }

    public override float ReadFanDuty()
    {
        if (!IsOpen)
            return 100.0f;

        int cpuFan = asusACPI.DeviceGet(AsusACPI.CPU_Fan);
        int gpuFan = asusACPI.DeviceGet(AsusACPI.GPU_Fan);
        return (cpuFan + gpuFan) / 2 * 100;
    }

    public override void SetKeyPressDelay(HIDmode controllerMode)
    {
        switch (controllerMode)
        {
            case HIDmode.DualShock4Controller:
                KeyPressDelay = 180;
                break;
            default:
                KeyPressDelay = 20;
                break;
        }
    }

    private void HandleEvent(byte key)
    {
        if (!keyMapping.ContainsKey(key))
            return;

        // get button
        ButtonFlags button = keyMapping[key];
        switch (key)
        {
            case 0:   // Back paddles: Release
                {
                    KeyRelease(ButtonFlags.OEM3);
                }
                return;

            case 165:   // Back paddles: Press
            case 167:   // Armory crate: Hold
                KeyPress(button);
                break;

            case 168:   // Armory crate: Hold, released
                KeyRelease(button);
                break;

            default:
            case 56:    // Armory crate: Click
            case 166:   // Command center: Click
                {
                    Task.Factory.StartNew(async () =>
                    {
                        KeyPress(button);
                        await Task.Delay(KeyPressDelay);
                        KeyRelease(button);
                    });
                }
                break;
        }
    }

    public override bool SetLedBrightness(int brightness) 
    {
        //ROG ALly brightness range is: 0 - 3 range, 0 is off, convert from 0 - 100 % range
        brightness = (int)Math.Round(brightness / 33.33);

        Task.Run(async () =>
        {
            byte[] msg = { AURA_HID_ID, 0xba, 0xc5, 0xc4, (byte)brightness };

            hidDevices.TryGetValue(AURA_HID_ID, out HidDevice hidDevice);
            if (hidDevice is null || !hidDevice.IsConnected)
                return false;

            hidDevice.WriteFeatureData(msg);

            return true;
        });

        return false;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed)
    {
        if (!DynamicLightingCapabilities.HasFlag(level))
            return false;

        // Apply the color for the left and right LED
        AuraMode auraMode = AuraMode.SolidColor;

        switch (level)
        {
            case LEDLevel.SolidColor:
                auraMode = AuraMode.SolidColor;
                break;
            case LEDLevel.Breathing:
                auraMode = AuraMode.Breathing;
                break;
            case LEDLevel.Rainbow:
                auraMode = AuraMode.Rainbow;
                break;
            case LEDLevel.Wave:
                auraMode = AuraMode.Wave;
                break;
            case LEDLevel.Wheel:
                auraMode = AuraMode.Wheel;
                break;
            case LEDLevel.Ambilight:
                return ApplyColorFast(MainColor, SecondaryColor);
        }

        AuraSpeed auraSpeed = AuraSpeed.Fast;
        if (speed <= 33)
            auraSpeed = AuraSpeed.Slow;
        else if (speed > 33 && speed <= 66)
            auraSpeed = AuraSpeed.Medium;
        else
            auraSpeed = AuraSpeed.Fast;

        return ApplyColor(auraMode, MainColor, SecondaryColor, auraSpeed);
    }

    private bool ApplyColor(AuraMode mode, Color MainColor, Color SecondaryColor, AuraSpeed speed = AuraSpeed.Slow, AuraDirection direction = AuraDirection.Forward)
    {
        hidDevices.TryGetValue(AURA_HID_ID, out HidDevice hidDevice);
        if (hidDevice is null || !hidDevice.IsConnected)
            return false;

        hidDevice.Write(AuraMessage(mode, MainColor, SecondaryColor, speed, LEDZone.All));
        hidDevice.Write(MESSAGE_APPLY);
        hidDevice.Write(MESSAGE_SET);

        return true;
    }

    private bool ApplyColorFast(Color MainColor, Color SecondaryColor)
    {
        hidDevices.TryGetValue(AURA_HID_ID, out HidDevice hidDevice);
        if (hidDevice is null || !hidDevice.IsConnected)
            return false;

        // Left joystick
        hidDevice.Write(AuraMessage(AuraMode.SolidColor, MainColor, MainColor, AuraSpeed.Slow, LEDZone.JoystickLeftSideLeft));
        hidDevice.Write(AuraMessage(AuraMode.SolidColor, MainColor, MainColor, AuraSpeed.Slow, LEDZone.JoystickLeftSideRight));

        // Right joystick
        hidDevice.Write(AuraMessage(AuraMode.SolidColor, SecondaryColor, SecondaryColor, AuraSpeed.Slow, LEDZone.JoystickRightSideLeft));
        hidDevice.Write(AuraMessage(AuraMode.SolidColor, SecondaryColor, SecondaryColor, AuraSpeed.Slow, LEDZone.JoystickRightSideRight));

        return true;
    }

    private static byte[] AuraMessage(AuraMode mode, Color LEDColor1, Color LEDColor2, AuraSpeed speed, LEDZone zone, LEDDirection direction = LEDDirection.Up)
    {
        byte[] msg = new byte[17];
        msg[0] = AURA_HID_ID;
        msg[1] = 0xb3;
        msg[2] = (byte)zone; // Zone 
        msg[3] = (byte)mode; // Aura Mode
        msg[4] = LEDColor1.R; // R
        msg[5] = LEDColor1.G; // G
        msg[6] = LEDColor1.B; // B
        msg[7] = (byte)speed; // aura.speed as u8;
        msg[8] = (byte)direction; // aura.direction as u8;
        msg[9] = (mode == AuraMode.Breathing) ? (byte)1 : (byte)0;
        msg[10] = LEDColor2.R; // R
        msg[11] = LEDColor2.G; // G
        msg[12] = LEDColor2.B; // B
        return msg;
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\uE006";
            case ButtonFlags.OEM2:
                return "\uE005";
            case ButtonFlags.OEM3:
                return "\u2212";
            case ButtonFlags.OEM4:
                return "\u2213";
        }

        return defaultGlyph;
    }
}