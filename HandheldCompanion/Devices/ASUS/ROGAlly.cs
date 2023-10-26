using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Numerics;
using System.Text;
using WindowsInput.Events;
using Task = System.Threading.Tasks.Task;

namespace HandheldCompanion.Devices;

public class ROGAlly : IDevice
{
    private readonly Dictionary<byte, ButtonFlags> keyMapping = new()
    {
        { 0, ButtonFlags.None },
        { 56, ButtonFlags.OEM2 },
        { 162, ButtonFlags.None },
        { 166, ButtonFlags.OEM1 },
        { 165, ButtonFlags.OEM3 },
        { 167, ButtonFlags.OEM4 },
        { 168, ButtonFlags.OEM4 },
        { 236, ButtonFlags.None }
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

    private enum AuraMode
    {
        Static = 0,
        Breathe = 1,
        Cycle = 2,
        Rainbow = 3,
        Strobe = 4,
    }

    private enum AuraSpeed
    {
        Slow = 0xeb,
        Medium = 0xf5,
        Fast = 0xe1,
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
        Capabilities = DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.LEDControl;

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

        if (hidDevice is not null)
            hidDevice.OpenDevice();

        // try open asus ACPI
        asusACPI = new AsusACPI();
        if (asusACPI is null)
            return false;

        asusACPI.SubscribeToEvents(WatcherEventArrived);

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
        hidDevice = GetHidDevices(_vid, _pid).FirstOrDefault();
        if (hidDevice is null)
            return false;

        var pnpDevice = PnPDevice.GetDeviceByInterfaceId(hidDevice.DevicePath);
        var device_parent = pnpDevice.GetProperty<string>(DevicePropertyKey.Device_Parent);

        var pnpParent = PnPDevice.GetDeviceByInstanceId(device_parent);
        var parent_guid = pnpParent.GetProperty<Guid>(DevicePropertyKey.Device_ClassGuid);
        var parent_instanceId = pnpParent.GetProperty<string>(DevicePropertyKey.Device_InstanceId);

        return DeviceHelper.IsDeviceAvailable(parent_guid, parent_instanceId);
    }

    public override void SetFanControl(bool enable)
    {
        if (!asusACPI.IsOpen())
            return;

        switch (enable)
        {
            case false:
                asusACPI.DeviceSet(AsusACPI.PerformanceMode, (int)AsusMode.Turbo);
                return;
        }
    }

    public override void SetFanDuty(double percent)
    {
        if (!asusACPI.IsOpen())
            return;

        asusACPI.SetFanSpeed(AsusFan.CPU, Convert.ToByte(percent));
        asusACPI.SetFanSpeed(AsusFan.GPU, Convert.ToByte(percent));
    }

    public override float ReadFanDuty()
    {
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
        var button = keyMapping[key];

        switch (key)
        {
            case 167:   // Armory crate: Hold
                KeyPress(button);
                break;

            case 168:   // Armory crate: Hold, released
                KeyRelease(button);
                break;

            default:
            case 56:    // Armory crate: Click
            case 165:   // Back paddles: Click
            case 166:   // Command center: Click
                {
                    Task.Run(async () =>
                    {
                        KeyPress(button);
                        await Task.Delay(KeyPressDelay);
                        KeyRelease(button);
                    });
                }
                break;
        }
    }

    public override bool SetLedStatus(bool status)
    {
        switch(status)
        {
            case true:
                bool success1 = SetLedBrightness(SettingsManager.GetInt("LEDBrightness"));
                bool success2 = SetLedColor(SettingsManager.GetString("LEDColor"));
                return success1 && success2;
            case false:
                return SetLedBrightness(0);
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

    public override bool SetLedColor(string hexColor)
    {
        // Remove the '#' character and convert the remaining string to a 32-bit integer
        int argbValue = int.Parse(hexColor.Substring(1), System.Globalization.NumberStyles.HexNumber);
        
        // Create a Color object from the ARGB value
        Color color = Color.FromArgb(argbValue);

        // Apply the color for the left and right LED, with default auro mode settings
        ApplyColor(AuraMode.Static, color, color, AuraSpeed.Slow);

        return true;
    }

    private bool ApplyColor(AuraMode mode, Color color, Color color2, AuraSpeed speed)
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid);
        foreach (HidDevice device in devices)
        {
            if (device is null || !device.IsConnected)
                return false;

            if (!device.ReadFeatureData(out byte[] data, AURA_HID_ID))
                return false;

            device.Write(AuraMessage(mode, color, color2, (int)speed));
            device.Write(MESSAGE_APPLY);
            device.Write(MESSAGE_SET);
        }

        return true;
    }

    private static byte[] AuraMessage(AuraMode mode, Color color, Color color2, int speed, bool mono = false)
    {
        byte[] msg = new byte[17];
        msg[0] = AURA_HID_ID;
        msg[1] = 0xb3;
        msg[2] = 0x00; // Zone 
        msg[3] = (byte)mode; // Aura Mode
        msg[4] = color.R; // R
        msg[5] = mono ? (byte)0 : color.G; // G
        msg[6] = mono ? (byte)0 : color.B; // B
        msg[7] = (byte)speed; // aura.speed as u8;
        msg[8] = 0; // aura.direction as u8;
        msg[9] = (mode == AuraMode.Breathe) ? (byte)1 : (byte)0;
        msg[10] = color2.R; // R
        msg[11] = mono ? (byte)0 : color2.G; // G
        msg[12] = mono ? (byte)0 : color2.B; // B
        return msg;
    }
}
