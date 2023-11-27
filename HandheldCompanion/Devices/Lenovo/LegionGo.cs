using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class LegionGo : IDevice
{
    public enum LegionMode
    {
        Quiet = 0,
        Balance = 1,
        Performance = 2,
        Custom = 3,
    }

    public const byte INPUT_HID_ID = 0x04;

    public override bool IsOpen => hidDevices.ContainsKey(INPUT_HID_ID) && hidDevices[INPUT_HID_ID].IsOpen;

    public LegionGo()
    {
        // device specific settings
        ProductIllustration = "device_legion_go";

        // used to monitor OEM specific inputs
        _vid = 0x17EF;
        _pid = 0x6182;

        // https://www.amd.com/en/products/apu/amd-ryzen-z1
        // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2700 };

        GyrometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(-1.0f, 1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities |= DeviceCapabilities.None;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Ambilight;

        powerProfileQuiet = new(Properties.Resources.PowerProfileSilentName, Properties.Resources.PowerProfileSilentDescription)
        {
            Default = true,
            OSPowerMode = PowerMode.BetterBattery,
            OEMPowerMode = (int)LegionMode.Quiet,
            Guid = PowerMode.BetterBattery
        };

        powerProfileBalanced = new(Properties.Resources.PowerProfilePerformanceName, Properties.Resources.PowerProfilePerformanceDescription)
        {
            Default = true,
            OSPowerMode = PowerMode.BetterPerformance,
            OEMPowerMode = (int)LegionMode.Balance,
            Guid = PowerMode.BetterPerformance
        };

        powerProfileCool = new(Properties.Resources.PowerProfileTurboName, Properties.Resources.PowerProfileTurboDescription)
        {
            Default = true,
            OSPowerMode = PowerMode.BestPerformance,
            OEMPowerMode = (int)LegionMode.Performance,
            Guid = PowerMode.BestPerformance
        };

        PowerProfileManager.Applied += PowerProfileManager_Applied;

        OEMChords.Add(new DeviceChord("LegionR",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("LegionL",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM2
        ));

        // device specific layout
        DefaultLayout.AxisLayout[AxisLayoutFlags.RightPad] = new MouseActions {MouseType = MouseActionsType.Move, Filtering = true, Sensivity = 15 };

        DefaultLayout.ButtonLayout[ButtonFlags.RightPadClick] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.LeftButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.Low } };
        DefaultLayout.ButtonLayout[ButtonFlags.RightPadClickDown] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.RightButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.High } };
        DefaultLayout.ButtonLayout[ButtonFlags.B5] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.R1 } };
        DefaultLayout.ButtonLayout[ButtonFlags.B6] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.MiddleButton } };
        DefaultLayout.ButtonLayout[ButtonFlags.B7] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.ScrollUp } };
        DefaultLayout.ButtonLayout[ButtonFlags.B8] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.ScrollDown } };

        Init();
    }

    private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        if (profile.OEMPowerMode == -1)
            ECRAMWrite(0x0A, (byte)LegionMode.Custom);
        else
            ECRAMWrite(0x0A, (byte)profile.OEMPowerMode);

        // Fan control: Default, Full (0, 1)
        // ECRAMWrite(0x8A, 0);
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        SetQuickLightingEffect(0, 1);
        SetQuickLightingEffect(3, 1);
        SetQuickLightingEffect(4, 1);
        SetQuickLightingEffectEnable(0, false);
        SetQuickLightingEffectEnable(3, false);
        SetQuickLightingEffectEnable(4, false);

        lightProfileL = GetCurrentLightProfile(3);
        lightProfileR = GetCurrentLightProfile(4);

        // Legion XInput controller and other Legion devices shares the same USBHUB
        while (ControllerManager.PowerCyclers.Count > 0)
            Thread.Sleep(500);

        return true;
    }

    public override void Close()
    {
        // restore default touchpad behavior
        SetTouchPadStatus(1);

        // close devices
        foreach (KeyValuePair<byte, HidDevice> hidDevice in hidDevices)
        {
            byte key = hidDevice.Key;
            HidDevice device = hidDevice.Value;

            device.CloseDevice();
        }

        base.Close();
    }

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid, 0);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (device.Capabilities.InputReportByteLength == 64)
                hidDevices[INPUT_HID_ID] = device;  // HID-compliant vendor-defined device
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

    private LightionProfile lightProfileL = new();
    private LightionProfile lightProfileR = new();
    public override bool SetLedBrightness(int brightness)
    {
        lightProfileL.brightness = brightness;
        lightProfileR.brightness = brightness;

        SetLightingEffectProfileID(3, lightProfileL);
        SetLightingEffectProfileID(4, lightProfileR);

        return true;
    }

    public override bool SetLedStatus(bool status)
    {
        SetLightingEnable(0, status);

        return true;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
    {
        lightProfileL.r = MainColor.R;
        lightProfileL.g = MainColor.G;
        lightProfileL.b = MainColor.B;
        lightProfileL.speed = speed;
        SetLightingEffectProfileID(3, lightProfileL);

        lightProfileR.r = SecondaryColor.R;
        lightProfileR.g = SecondaryColor.G;
        lightProfileR.b = SecondaryColor.B;
        lightProfileR.speed = speed;
        SetLightingEffectProfileID(4, lightProfileR);

        return true;
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2205";
            case ButtonFlags.OEM2:
                return "\uE004";
            case ButtonFlags.OEM3:
                return "\u2212";
            case ButtonFlags.OEM4:
                return "\u2213";
            case ButtonFlags.OEM5:
                return "\u2214";
            case ButtonFlags.OEM6:
                return "\u2215";
            case ButtonFlags.OEM7:
                return "\u2216";
            case ButtonFlags.OEM8:
                return "\u2217";
        }

        return defaultGlyph;
    }
}