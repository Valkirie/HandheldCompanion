using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Management;
using System.Numerics;
using System.Text;
using WindowsInput.Events;
using Task = System.Threading.Tasks.Task;
using static HandheldCompanion.Utils.DeviceUtils;
using System.Threading.Tasks;

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

    private HidDevice hidDevice;
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

    public override bool IsOpen => hidDevice is not null && hidDevice.IsOpen && asusACPI is not null && asusACPI.IsOpen();

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
        DynamicLightingCapabilities |= LEDLevel.Wave;
        DynamicLightingCapabilities |= LEDLevel.Ambilight;

        powerProfileQuiet = new(Properties.Resources.PowerProfileSilentName, Properties.Resources.PowerProfileSilentDescription)
        {
            Default = true,
            OEMPowerMode = (int)AsusMode.Silent,
            Guid = PowerMode.BetterBattery
        };

        powerProfileBalanced = new(Properties.Resources.PowerProfilePerformanceName, Properties.Resources.PowerProfilePerformanceDescription)
        {
            Default = true,
            OEMPowerMode = (int)AsusMode.Performance,
            Guid = PowerMode.BetterPerformance
        };

        powerProfileCool = new(Properties.Resources.PowerProfileTurboName, Properties.Resources.PowerProfileTurboDescription)
        {
            Default = true,
            OEMPowerMode = (int)AsusMode.Turbo,
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

        // deprecated
        // asusACPI.SubscribeToEvents(WatcherEventArrived);

        return true;
    }

    public override void Close()
    {
        // close Asus ACPI
        if (asusACPI is not null)
            asusACPI.Close();

        // clear array
        if (hidDevice is not null)
            hidDevice.CloseDevice();        

        base.Close();
    }

    public void WatcherEventArrived(object sender, EventArrivedEventArgs e)
    {
        if (e.NewEvent is null) return;
        int EventID = int.Parse(e.NewEvent["EventID"].ToString());
        LogManager.LogDebug("WMI event {0}", EventID);
        HandleEvent((byte)EventID);
    }

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            try
            {
                device.OpenDevice();
                device.MonitorDeviceEvents = true;
            }
            catch
            {
                continue;
            }

            Task<HidReport> ReportDevice = Task.Run(async () => await device.ReadReportAsync());
            ReportDevice.ContinueWith(t => OnReport(ReportDevice.Result, device));

            hidDevice = device;
        }

        if (hidDevice is null)
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

        switch(result.ReportId)
        {
            case 90:
                {
                    // get key
                    byte key = result.Data[0];
                    HandleEvent(key);                  
                }
                break;
        }
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
            byte[] msgBackup = { INPUT_HID_ID, 0xba, 0xc5, 0xc4, (byte)brightness };

            IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid);
            foreach (HidDevice device in devices)
            {
                if (device is null || !device.IsConnected)
                    return false;

                device.OpenDevice();

                if (device.ReadFeatureData(out byte[] data, AURA_HID_ID))
                {
                    device.WriteFeatureData(msg);
                }
                else
                {
                    return false;
                }

                if (device.ReadFeatureData(out byte[] dataBackkup, INPUT_HID_ID))
                {
                    device.WriteFeatureData(msgBackup);
                }
                else
                {
                    return false;
                }

                device.CloseDevice();
            }

            return true;
        });

        return false;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level)
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
            case LEDLevel.Ambilight:
                return ApplyColorFast(MainColor, SecondaryColor);
        }

        return ApplyColor(auraMode, MainColor, SecondaryColor, AuraSpeed.Fast);
    }

    private bool ApplyColor(AuraMode mode, Color MainColor, Color SecondaryColor, AuraSpeed speed = AuraSpeed.Slow, AuraDirection direction = AuraDirection.Forward)
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid);
        foreach (HidDevice device in devices)
        {
            if (device is null || !device.IsConnected)
                return false;

            if (!device.ReadFeatureData(out byte[] data, AURA_HID_ID))
                return false;

            device.Write(AuraMessage(mode, MainColor, SecondaryColor, (int)speed));
            device.Write(MESSAGE_APPLY);
            device.Write(MESSAGE_SET);
        }

        return true;
    }

    private bool ApplyColorFast(Color MainColor, Color SecondaryColor)
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid);
        foreach (HidDevice device in devices)
        {
            if (device is null || !device.IsConnected)
                return false;

            if (!device.ReadFeatureData(out byte[] data, AURA_HID_ID))
                return false;

            // Left joystick
            device.Write(AuraMessage(AuraMode.SolidColor, MainColor, MainColor, (int)AuraSpeed.Slow, false, (int)LEDZone.JoystickLeftSideLeft));
            device.Write(AuraMessage(AuraMode.SolidColor, MainColor, MainColor, (int)AuraSpeed.Slow, false, (int)LEDZone.JoystickLeftSideRight));

            // Right joystick
            device.Write(AuraMessage(AuraMode.SolidColor, SecondaryColor, SecondaryColor, (int)AuraSpeed.Slow, false, (int)LEDZone.JoystickRightSideLeft));
            device.Write(AuraMessage(AuraMode.SolidColor, SecondaryColor, SecondaryColor, (int)AuraSpeed.Slow, false, (int)LEDZone.JoystickRightSideRight));
        }

        return true;
    }

    private static byte[] AuraMessage(AuraMode mode, Color LEDColor1, Color LEDColor2, int speed, bool mono = false, int zone = 0, int direction = 0)
    {
        byte[] msg = new byte[17];
        msg[0] = AURA_HID_ID;
        msg[1] = 0xb3;
        msg[2] = (byte)zone; // Zone 
        msg[3] = (byte)mode; // Aura Mode
        msg[4] = LEDColor1.R; // R
        msg[5] = mono ? (byte)0 : LEDColor1.G; // G
        msg[6] = mono ? (byte)0 : LEDColor1.B; // B
        msg[7] = (byte)speed; // aura.speed as u8;
        msg[8] = (byte)direction; // aura.direction as u8;
        msg[9] = (mode == AuraMode.Breathing) ? (byte)1 : (byte)0;
        msg[10] = LEDColor2.R; // R
        msg[11] = mono ? (byte)0 : LEDColor2.G; // G
        msg[12] = mono ? (byte)0 : LEDColor2.B; // B
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