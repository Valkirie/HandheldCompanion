using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

    private const byte INPUT_HID_ID = 0x5a;
    private const byte AURA_HID_ID = 0x5d;

    static byte[] MESSAGE_APPLY = { AURA_HID_ID, 0xb4 };
    static byte[] MESSAGE_SET = { AURA_HID_ID, 0xb5, 0, 0, 0 };

    public override bool IsOpen => hidDevices.ContainsKey(INPUT_HID_ID) && hidDevices[INPUT_HID_ID].IsOpen && AsusACPI.IsOpen;

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
        vendorId = 0x0B05;
        productIds = [0x1ABE];

        // https://www.amd.com/en/products/apu/amd-ryzen-z1
        // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
        // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        GyrometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
        Capabilities |= DeviceCapabilities.BatteryChargeLimit;
        Capabilities |= DeviceCapabilities.BatteryChargeLimitPercent;
        Capabilities |= DeviceCapabilities.OEMCPU;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Breathing;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;
        DynamicLightingCapabilities |= LEDLevel.Wheel;
        DynamicLightingCapabilities |= LEDLevel.Ambilight;

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileROGAllyBetterBattery, Properties.Resources.PowerProfileROGAllyBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OEMPowerMode = (int)AsusMode.Silent,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = BetterBatteryGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 10.0d, 10.0d, 10.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileROGAllyBetterPerformance, Properties.Resources.PowerProfileROGAllyBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OEMPowerMode = (int)AsusMode.Performance,
            OSPowerMode = OSPowerMode.BetterPerformance,
            Guid = BetterPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileROGAllyBestPerformance, Properties.Resources.PowerProfileROGAllyBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OEMPowerMode = (int)AsusMode.Turbo,
            OSPowerMode = OSPowerMode.BestPerformance,
            Guid = BestPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 25.0d, 25.0d, 25.0d }
        });

        OEMChords.Add(new KeyboardChord("CC",
            [], [],
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new KeyboardChord("AC",
            [], [],
            false, ButtonFlags.OEM2
        ));

        // M1 and M2 do a repeating input when holding the button
        OEMChords.Add(new KeyboardChord("M1",
            [KeyCode.F18],
            [KeyCode.F18],
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new KeyboardChord("M2",
            [KeyCode.F17],
            [KeyCode.F17],
            false, ButtonFlags.OEM4
        ));

        // prepare hotkeys
        DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = true;
        DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
    }

    #region buffer
    private byte[] flushBufferWriteChanges = new byte[] { 0x5A, 0xD1, 0x0A, 0x01 };
    private byte[] modeGame = new byte[] { 0x5A, 0xD1, 0x01, 0x01, 0x01 };
    private byte[] modeMouse = new byte[] { 0x5A, 0xD1, 0x01, 0x01, 0x03 };
    private byte[] dPadUpDownDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x01, 0x2C, 0x01, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x19, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x03, 0x8C, 0x88, 0x76 };
    private byte[] dPadLeftRightDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x02, 0x2C, 0x01, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x02, 0x82, 0x23, 0x00, 0x00, 0x00, 0x01, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x02, 0x82, 0x0D };
    private byte[] joySticksDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x03, 0x2C, 0x01, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x08 };
    private byte[] shoulderButtonsDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x04, 0x2C, 0x01, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x06 };
    private byte[] faceButtonsABDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x05, 0x2C, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x02, 0x82, 0x31 };
    private byte[] faceButtonsXYDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x06, 0x2C, 0x01, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x02, 0x82, 0x4D, 0x00, 0x00, 0x00, 0x01, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x1E };
    private byte[] viewAndMenuDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x07, 0x2C, 0x01, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x12 };
    private byte[] M1M2Default = new byte[] { 0x5A, 0xD1, 0x02, 0x08, 0x2C, 0x02, 0x00, 0x8E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x8E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x8F };
    private byte[] M1F18M2F17 = new byte[] { 0x5A, 0xD1, 0x02, 0x08, 0x2C, 0x02, 0x00, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x30 };
    private byte[] triggersDefault = new byte[] { 0x5A, 0xD1, 0x02, 0x09, 0x2C, 0x01, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x0E };
    private byte[] commitReset1of4 = new byte[] { 0x5A, 0xD1, 0x0F, 0x20 };
    private byte[] commitReset2of4 = new byte[] { 0x5A, 0xD1, 0x06, 0x02, 0x64, 0x64 };
    private byte[] commitReset3of4 = new byte[] { 0x5A, 0xD1, 0x04, 0x04, 0x00, 0x64, 0x00, 0x64 };
    private byte[] commitReset4of4 = new byte[] { 0x5A, 0xD1, 0x05, 0x04, 0x00, 0x64, 0x00, 0x64 };
    #endregion

    public override bool Open()
    {
        bool success = base.Open();
        if (!success)
            return false;

        return true;
    }

    public override void OpenEvents()
    {
        base.OpenEvents();

        // manage events
        ControllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

        Device_Inserted();
    }

    private static byte[] defaultCPUFan = new byte[] { 0x3A, 0x3D, 0x40, 0x44, 0x48, 0x4D, 0x51, 0x62, 0x08, 0x11, 0x16, 0x1A, 0x22, 0x29, 0x30, 0x45 };
    private static byte[] defaultGPUFan = new byte[] { 0x3A, 0x3D, 0x40, 0x44, 0x48, 0x4D, 0x51, 0x62, 0x0C, 0x16, 0x1D, 0x1F, 0x26, 0x2D, 0x34, 0x4A };

    private static byte[] ToAsusCurve(double[] fanSpeeds)
    {
        if (fanSpeeds is null || fanSpeeds.Length != 11)
            return defaultCPUFan;

        int[] anchorTemps = { 20, 30, 40, 50, 60, 70, 80, 90 }; // °C
        int[] sourceIdx = { 2, 3, 4, 5, 6, 7, 8, 9 }; // map to 0..100 steps

        byte[] curve = new byte[16];

        // first 8: temps
        for (int i = 0; i < 8; i++)
            curve[i] = (byte)anchorTemps[i];

        // last 8: duties (clamped 0..100, monotonic non-decreasing)
        byte last = 0;
        for (int i = 0; i < 8; i++)
        {
            byte duty = (byte)Math.Max(0, Math.Min(100, Math.Round(fanSpeeds[sourceIdx[i]])));
            if (duty < last) duty = last;   // ensure monotonic
            curve[8 + i] = last = duty;
        }
        return curve;
    }

    protected override void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        if (profile.FanProfile.fanMode == FanMode.Software)
        {
            byte[] asus = ToAsusCurve(profile.FanProfile.fanSpeeds);
            AsusACPI.SetFanCurve(AsusFan.CPU, asus);
            AsusACPI.SetFanCurve(AsusFan.GPU, asus);
            AsusACPI.SetFanCurve(AsusFan.Mid, asus);
        }
        else
        {
            // restore default fan table
            SetFanControl(false);
        }
    }

    private void ControllerManager_ControllerPlugged(Controllers.IController Controller, bool IsPowerCycling)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Inserted(true);
    }

    private void ControllerManager_ControllerUnplugged(Controllers.IController Controller, bool IsPowerCycling, bool WasTarget)
    {
        // hack, force rescan
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Removed();
    }

    private bool IsReading = false;

    private void Device_Removed()
    {
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            device.MonitorDeviceEvents = false;
            device.Removed -= Device_Removed;
            try { device.Dispose(); } catch { }
        }

        // stop further reads
        IsReading = false;
    }

    private async void Device_Inserted(bool reScan = false)
    {
        // if you still want to automatically re-attach:
        if (reScan)
            await WaitUntilReady();

        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            device.MonitorDeviceEvents = true;
            device.Removed += Device_Removed;
            device.OpenDevice();

            // fire‐and‐forget the read loop
            IsReading = true;
            _ = ReadLoopAsync(device);

            // force M1/M2 to send F17 and F18
            ConfigureController(true);
        }
    }

    private async Task ReadLoopAsync(HidDevice device)
    {
        try
        {
            while (IsReading)
            {
                HidReport report = await device.ReadReportAsync().ConfigureAwait(false);
                HandleReport(report, device);
            }
        }
        catch { }
    }

    private void HandleReport(HidReport report, HidDevice device)
    {
        lock (this.updateLock)
        {
            byte key = report.Data[0];
            HandleEvent(key);
        }
    }

    protected override void QuerySettings()
    {
        // raise events
        SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetString("BatteryChargeLimit"), false);
        SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetString("BatteryChargeLimitPercent"), false);

        base.QuerySettings();
    }

    public override void Close()
    {
        // close Asus ACPI
        AsusACPI.Close();

        // restore default M1/M2 behavior
        ConfigureController(false);

        // close devices
        lock (this.updateLock)
        {
            foreach (HidDevice hidDevice in hidDevices.Values)
                hidDevice.Dispose();
            hidDevices.Clear();
        }

        // manage events
        ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

        base.Close();
    }

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 64);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (device.ReadFeatureData(out byte[] data, INPUT_HID_ID))
                hidDevices[INPUT_HID_ID] = device;
            else if (device.ReadFeatureData(out data, AURA_HID_ID))
                hidDevices[AURA_HID_ID] = device;
        }

        try
        {
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice hidDevice))
            {
                try
                {
                    PnPDevice pnpDevice = PnPDevice.GetDeviceByInterfaceId(hidDevice.DevicePath);
                    string device_parent = pnpDevice.GetProperty<string>(DevicePropertyKey.Device_Parent);

                    PnPDevice pnpParent = PnPDevice.GetDeviceByInstanceId(device_parent);
                    Guid parent_guid = pnpParent.GetProperty<Guid>(DevicePropertyKey.Device_ClassGuid);
                    string parent_instanceId = pnpParent.GetProperty<string>(DevicePropertyKey.Device_InstanceId);

                    return DeviceHelper.IsDeviceAvailable(parent_guid, parent_instanceId);
                }
                catch { }
            }
        }
        catch { }

        return false;
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        if (!IsOpen)
            return;

        switch (enable)
        {
            case false:
                // restore default
                AsusACPI.SetFanCurve(AsusFan.CPU, defaultCPUFan);
                AsusACPI.SetFanCurve(AsusFan.GPU, defaultGPUFan);
                AsusACPI.SetFanCurve(AsusFan.Mid, defaultCPUFan);
                break;
        }
    }

    /*
    public override void SetFanDuty(double percent)
    {
        if (!IsOpen)
            return;

        AsusACPI.SetFanSpeed(AsusFan.CPU, Convert.ToByte(percent));
        AsusACPI.SetFanSpeed(AsusFan.GPU, Convert.ToByte(percent));
        AsusACPI.SetFanSpeed(AsusFan.Mid, Convert.ToByte(percent));
    }
    */

    public override float ReadFanDuty()
    {
        if (!IsOpen)
            return 100.0f;

        if (AsusACPI.IsOpen)
            return (AsusACPI.DeviceGet(AsusACPI.CPU_Fan) + AsusACPI.DeviceGet(AsusACPI.GPU_Fan)) / 2.0f * 100.0f;

        return 100.0f;
    }

    private void HandleEvent(byte key)
    {
        if (!keyMapping.ContainsKey(key))
            return;

        // get button
        ButtonFlags button = keyMapping[key];
        switch (key)
        {
            case 167:   // Armory crate: Hold
                KeyPress(button);
                break;

            case 168:   // Armory crate: Hold, released
                KeyRelease(button);
                break;

            case 56:    // Armory crate: Click
            case 166:   // Command center: Click
                {
                    Task.Run(async () =>
                    {
                        KeyPress(button);
                        await Task.Delay(KeyPressDelay).ConfigureAwait(false); // Avoid blocking the synchronization context
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

        if (hidDevices.TryGetValue(AURA_HID_ID, out HidDevice hidDevice))
        {
            if (!hidDevice.IsConnected)
                return false;

            byte[] msg = { AURA_HID_ID, 0xba, 0xc5, 0xc4, (byte)brightness };
            return hidDevice.WriteFeatureData(msg);
        }

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
        if (hidDevices.TryGetValue(AURA_HID_ID, out HidDevice hidDevice))
        {
            if (!hidDevice.IsConnected)
                return false;

            hidDevice.Write(AuraMessage(mode, MainColor, SecondaryColor, speed, LEDZone.All));
            hidDevice.Write(MESSAGE_APPLY);
            hidDevice.Write(MESSAGE_SET);

            return true;
        }

        return false;
    }

    private bool ApplyColorFast(Color MainColor, Color SecondaryColor)
    {
        if (hidDevices.TryGetValue(AURA_HID_ID, out HidDevice hidDevice))
        {
            if (!hidDevice.IsConnected)
                return false;

            // Left joystick
            hidDevice.Write(AuraMessage(AuraMode.SolidColor, MainColor, MainColor, AuraSpeed.Slow, LEDZone.JoystickLeftSideLeft));
            hidDevice.Write(AuraMessage(AuraMode.SolidColor, MainColor, MainColor, AuraSpeed.Slow, LEDZone.JoystickLeftSideRight));

            // Right joystick
            hidDevice.Write(AuraMessage(AuraMode.SolidColor, SecondaryColor, SecondaryColor, AuraSpeed.Slow, LEDZone.JoystickRightSideLeft));
            hidDevice.Write(AuraMessage(AuraMode.SolidColor, SecondaryColor, SecondaryColor, AuraSpeed.Slow, LEDZone.JoystickRightSideRight));

            return true;
        }

        return false;
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

        return base.GetGlyph(button);
    }

    private void ConfigureController(bool Remap)
    {
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            if (!device.IsConnected)
                return;

            device.WriteFeatureData(modeGame, 64);
            device.WriteFeatureData(dPadUpDownDefault, 64);
            device.WriteFeatureData(dPadLeftRightDefault, 64);
            device.WriteFeatureData(joySticksDefault, 64);
            device.WriteFeatureData(shoulderButtonsDefault, 64);
            device.WriteFeatureData(faceButtonsABDefault, 64);
            device.WriteFeatureData(faceButtonsXYDefault, 64);
            device.WriteFeatureData(viewAndMenuDefault, 64);

            device.WriteFeatureData((Remap ? M1F18M2F17 : M1M2Default), 64);

            device.WriteFeatureData(triggersDefault, 64);
            device.WriteFeatureData(commitReset1of4, 64);
            device.WriteFeatureData(commitReset2of4, 64);
            device.WriteFeatureData(commitReset3of4, 64);
            device.WriteFeatureData(commitReset4of4, 64);
        }
    }

    public bool XBoxController(bool disabled)
    {
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            if (!device.IsConnected)
                return false;

            return device.WriteFeatureData(new byte[] { 0x5A, 0xD1, 0x0B, 0x01, disabled ? (byte)0x02 : (byte)0x01 }, 64);
        }

        return false;
    }

    public void SetBatteryChargeLimit(int chargeLimit)
    {
        if (!IsOpen)
            return;

        if (chargeLimit < 0 || chargeLimit > 100)
            return;

        AsusACPI.DeviceSet(AsusACPI.BatteryLimit, chargeLimit);
    }

    public override void set_long_limit(int limit)
    {
        AsusACPI.DeviceSet(AsusACPI.PPT_APUA3, limit);
    }

    public override void set_short_limit(int limit)
    {
        AsusACPI.DeviceSet(AsusACPI.PPT_APUA0, limit);
        AsusACPI.DeviceSet(AsusACPI.PPT_APUC1, limit);
    }

    protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "BatteryChargeLimit":
                bool enabled = Convert.ToBoolean(value);
                switch (enabled)
                {
                    case false:
                        SetBatteryChargeLimit(100);
                        break;
                    case true:
                        int percent = Convert.ToInt32(ManagerFactory.settingsManager.GetInt("BatteryChargeLimitPercent"));
                        SetBatteryChargeLimit(percent);
                        break;
                }
                break;

            case "BatteryChargeLimitPercent":
                {
                    int percent = Convert.ToInt32(value);
                    SetBatteryChargeLimit(percent);
                }
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary);
    }
}