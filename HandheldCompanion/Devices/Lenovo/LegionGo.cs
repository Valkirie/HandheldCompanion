using HandheldCompanion.Actions;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class LegionGo : IDevice
{
    public enum LegionMode
    {
        Quiet = 0x01,
        Balanced = 0x02,
        Performance = 0x03,
        Custom = 0xFF,
    }

    public enum CapabilityID
    {
        IGPUMode = 0x00010000,
        FlipToStart = 0x00030000,
        NvidiaGPUDynamicDisplaySwitching = 0x00040000,
        AMDSmartShiftMode = 0x00050001,
        AMDSkinTemperatureTracking = 0x00050002,
        SupportedPowerModes = 0x00070000,
        LegionZoneSupportVersion = 0x00090000,
        IGPUModeChangeStatus = 0x000F0000,
        CPUShortTermPowerLimit = 0x0101FF00,
        CPULongTermPowerLimit = 0x0102FF00,
        CPUPeakPowerLimit = 0x0103FF00,
        CPUTemperatureLimit = 0x0104FF00,
        APUsPPTPowerLimit = 0x0105FF00,
        CPUCrossLoadingPowerLimit = 0x0106FF00,
        CPUPL1Tau = 0x0107FF00,
        GPUPowerBoost = 0x0201FF00,
        GPUConfigurableTGP = 0x0202FF00,
        GPUTemperatureLimit = 0x0203FF00,
        GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = 0x0204FF00,
        GPUStatus = 0x02070000,
        GPUDidVid = 0x02090000,
        InstantBootAc = 0x03010001,
        InstantBootUsbPowerDelivery = 0x03010002,
        FanFullSpeed = 0x04020000,
        CpuCurrentFanSpeed = 0x04030001,
        GpuCurrentFanSpeed = 0x04030002,
        CpuCurrentTemperature = 0x05040000,
        GpuCurrentTemperature = 0x05050000
    }

    private Task<bool> GetFanFullSpeedAsync() =>
        WMI.CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "GetFeatureValue",
            new() { { "IDs", (int)CapabilityID.FanFullSpeed } },
            pdc => Convert.ToInt32(pdc["Value"].Value) == 1);

    public Task SetFanFullSpeedAsync(bool enabled) =>
        WMI.CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "SetFeatureValue",
            new()
            {
                { "IDs", (int)CapabilityID.FanFullSpeed },
                { "value", enabled ? 1 : 0 },
            });

    private Task SetFanTable(FanTable fanTable) => WMI.CallAsync("root\\WMI",
        $"SELECT * FROM LENOVO_FAN_METHOD",
        "Fan_Set_Table",
        new() { { "FanTable", fanTable.GetBytes() } });

    public static Task<int> GetSmartFanModeAsync() => WMI.CallAsync("root\\WMI",
        $"SELECT * FROM LENOVO_GAMEZONE_DATA",
        "GetSmartFanMode",
        [],
        pdc => Convert.ToInt32(pdc["Data"].Value));

    private Task SetSmartFanMode(int fanMode) => WMI.CallAsync("root\\WMI",
        $"SELECT * FROM LENOVO_GAMEZONE_DATA",
        "SetSmartFanMode",
        new() { { "Data", fanMode } });

    public Task SetCPUPowerLimit(CapabilityID capabilityID, int limit) =>
        WMI.CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "SetFeatureValue",
            new()
            {
                { "IDs", (int)capabilityID },
                { "value", limit },
            });

    public const byte INPUT_HID_ID = 0x04;

    public override bool IsOpen => hidDevices.ContainsKey(INPUT_HID_ID) && hidDevices[INPUT_HID_ID].IsOpen;

    public const int LeftJoyconIndex = 3;
    public const int RightJoyconIndex = 4;

    public LegionGo()
    {
        // device specific settings
        ProductIllustration = "device_legion_go";

        // used to monitor OEM specific inputs
        _vid = 0x17EF;
        _pid = 0x6182;

        // fix for threshold overflow
        GamepadMotion.SetCalibrationThreshold(124.0f, 2.0f);

        // https://www.amd.com/en/products/apu/amd-ryzen-z1
        // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        GyrometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities |= DeviceCapabilities.None;
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Breathing;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;
        DynamicLightingCapabilities |= LEDLevel.Wheel;

        // Legion Go - Quiet
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileLegionGoBetterBattery, Properties.Resources.PowerProfileLegionGoBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            OEMPowerMode = (int)LegionMode.Quiet,
            Guid = new("961cc777-2547-4f9d-8174-7d86181b8a7a"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 8.0d, 8.0d, 8.0d }
        });

        // Legion Go - Balanced
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileLegionGoBetterPerformance, Properties.Resources.PowerProfileLegionGoBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            OEMPowerMode = (int)LegionMode.Balanced,
            Guid = new("3af9B8d9-7c97-431d-ad78-34a8bfea439f"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
        });

        // Legion Go - Performance
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileLegionGoBestPerformance, Properties.Resources.PowerProfileLegionGoBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            OEMPowerMode = (int)LegionMode.Performance,
            Guid = new("ded574b5-45a0-4f42-8737-46345c09c238"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 20.0d, 20.0d, 20.0d }
        });

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
        DefaultLayout.AxisLayout[AxisLayoutFlags.RightPad] = new MouseActions { MouseType = MouseActionsType.Move, Filtering = true, Sensivity = 15 };

        DefaultLayout.ButtonLayout[ButtonFlags.RightPadClick] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.LeftButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.Low } };
        DefaultLayout.ButtonLayout[ButtonFlags.RightPadClickDown] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.RightButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.High } };
        DefaultLayout.ButtonLayout[ButtonFlags.B5] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.R1 } };
        DefaultLayout.ButtonLayout[ButtonFlags.B6] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.MiddleButton } };
        DefaultLayout.ButtonLayout[ButtonFlags.B7] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.ScrollUp } };
        DefaultLayout.ButtonLayout[ButtonFlags.B8] = new List<IActions>() { new MouseActions { MouseType = MouseActionsType.ScrollDown } };

        /*
        Task<bool> task = Task.Run(async () => await GetFanFullSpeedAsync());
        bool FanFullSpeed = task.Result; 
        */
    }

    private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        FanTable fanTable = new(new ushort[] { 44, 48, 55, 60, 71, 79, 87, 87, 100, 100 });
        if (profile.FanProfile.fanMode != FanMode.Hardware)
        {
            fanTable = new(new ushort[] {
                (ushort)profile.FanProfile.fanSpeeds[1],
                (ushort)profile.FanProfile.fanSpeeds[2],
                (ushort)profile.FanProfile.fanSpeeds[3],
                (ushort)profile.FanProfile.fanSpeeds[4],
                (ushort)profile.FanProfile.fanSpeeds[5],
                (ushort)profile.FanProfile.fanSpeeds[6],
                (ushort)profile.FanProfile.fanSpeeds[7],
                (ushort)profile.FanProfile.fanSpeeds[8],
                (ushort)profile.FanProfile.fanSpeeds[9],
                (ushort)profile.FanProfile.fanSpeeds[10],
            });
        }

        // update fan table
        SetFanTable(fanTable);

        Task<int> fanModeTask = Task.Run(async () => await GetSmartFanModeAsync());
        int fanMode = fanModeTask.Result;

        if (fanMode != profile.OEMPowerMode)
            SetSmartFanMode(profile.OEMPowerMode);
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // initialize SapientiaUsb
        Init();

        // make sure both left and right gyros are enabled
        SetLeftGyroStatus(1);
        SetRightGyroStatus(1);

        // make sure both left and right gyros are reporting values
        SetGyroModeStatus(2, 1, 1);
        SetGyroModeStatus(2, 2, 2);

        // make sure both left and right gyros are reporting raw values
        SetGyroSensorDataOnorOff(LeftJoyconIndex, 0x02);
        SetGyroSensorDataOnorOff(RightJoyconIndex, 0x02);

        // disable QuickLightingEffect(s)
        SetQuickLightingEffect(0, 1);
        SetQuickLightingEffect(3, 1);
        SetQuickLightingEffect(4, 1);
        SetQuickLightingEffectEnable(0, false);
        SetQuickLightingEffectEnable(3, false);
        SetQuickLightingEffectEnable(4, false);

        // get current light profile(s)
        lightProfileL = GetCurrentLightProfile(3);
        lightProfileR = GetCurrentLightProfile(4);

        // Legion XInput controller and other Legion devices shares the same USBHUB
        while (ControllerManager.PowerCyclers.Count > 0)
            Thread.Sleep(100);

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

        // Reset the fan speed to default before device shutdown/restart
        SetFanFullSpeedAsync(false);

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

        SetLightingEffectProfileID(LeftJoyconIndex, lightProfileL);
        SetLightingEffectProfileID(RightJoyconIndex, lightProfileR);

        return true;
    }

    public override bool SetLedStatus(bool status)
    {
        SetLightingEnable(0, status);

        return true;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
    {
        // Speed is inverted for Legion Go
        lightProfileL.speed = 100 - speed;
        lightProfileR.speed = 100 - speed;

        // 1 - solid color
        // 2 - breathing
        // 3 - rainbow
        // 4 - spiral rainbow
        switch (level)
        {
            case LEDLevel.Breathing:
                {
                    lightProfileL.effect = 2;
                    lightProfileR.effect = 2;
                    SetLightProfileColors(MainColor, MainColor);
                }
                break;
            case LEDLevel.Rainbow:
                {
                    lightProfileL.effect = 3;
                    lightProfileR.effect = 3;
                }
                break;
            case LEDLevel.Wheel:
                {
                    lightProfileL.effect = 4;
                    lightProfileR.effect = 4;
                }
                break;
            default:
                {
                    lightProfileL.effect = 1;
                    lightProfileR.effect = 1;
                    SetLightProfileColors(MainColor, MainColor);
                }
                break;
        }

        SetLightingEffectProfileID(LeftJoyconIndex, lightProfileL);
        SetLightingEffectProfileID(RightJoyconIndex, lightProfileR);

        return true;
    }

    private void SetLightProfileColors(Color MainColor, Color SecondaryColor)
    {
        lightProfileL.r = MainColor.R;
        lightProfileL.g = MainColor.G;
        lightProfileL.b = MainColor.B;

        lightProfileR.r = SecondaryColor.R;
        lightProfileR.g = SecondaryColor.G;
        lightProfileR.b = SecondaryColor.B;
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