using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows.Media;
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

    #region WMI
    private bool GetFanFullSpeed()
    {
        try
        {
            return WMI.Call<bool>("root\\WMI",
                "SELECT * FROM LENOVO_OTHER_METHOD",
                "GetFeatureValue",
                new() { { "IDs", (int)CapabilityID.FanFullSpeed } },
                pdc => Convert.ToInt32(pdc["Value"].Value) == 1);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in GetFanFullSpeed: {0}", ex.Message);
            return false;
        }
    }

    public void SetFanFullSpeed(bool enabled)
    {
        try
        {
            WMI.Call("root\\WMI",
                "SELECT * FROM LENOVO_OTHER_METHOD",
                "SetFeatureValue",
                new()
                {
                { "IDs", (int)CapabilityID.FanFullSpeed },
                { "value", enabled ? 1 : 0 }
                });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetFanFullSpeed: {0}, Enabled: {1}", ex.Message, enabled);
        }
    }

    private void SetFanTable(FanTable fanTable)
    {
        try
        {
            WMI.Call("root\\WMI",
                "SELECT * FROM LENOVO_FAN_METHOD",
                "Fan_Set_Table",
                new() { { "FanTable", fanTable.GetBytes() } });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetFanTable: {0}, FanTable: {1}", ex.Message, string.Join(',', fanTable.GetBytes()));
        }
    }

    private int GetSmartFanMode()
    {
        try
        {
            return WMI.Call<int>("root\\WMI",
                "SELECT * FROM LENOVO_GAMEZONE_DATA",
                "GetSmartFanMode",
                [],
                pdc => Convert.ToInt32(pdc["Data"].Value));
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in GetSmartFanMode: {0}", ex.Message);
            return -1;
        }
    }

    private void SetSmartFanMode(int fanMode)
    {
        try
        {
            WMI.Call("root\\WMI",
                "SELECT * FROM LENOVO_GAMEZONE_DATA",
                "SetSmartFanMode",
                new() { { "Data", fanMode } });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetSmartFanMode: {0}, FanMode: {1}", ex.Message, fanMode);
        }
    }

    public void SetCPUPowerLimit(CapabilityID capabilityID, int limit)
    {
        try
        {
            WMI.Call("root\\WMI",
                "SELECT * FROM LENOVO_OTHER_METHOD",
                "SetFeatureValue",
                new()
                {
                { "IDs", (int)capabilityID },
                { "value", limit }
                });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetCPUPowerLimit: {0}, Capability: {1}, Limit: {2}", ex.Message, capabilityID, limit);
        }
    }

    public void SetBatteryChargeLimit(bool enabled)
    {
        try
        {
            WMI.Call("root\\WMI",
                "SELECT * FROM LENOVO_OTHER_METHOD",
                "SetFeatureValue",
                new()
                {
                { "IDs", (int)CapabilityID.InstantBootAc },
                { "value", enabled ? 1 : 0 }
                });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetBatteryChargeLimit: {0}, Enabled: {1}", ex.Message, enabled);
        }
    }
    #endregion

    public const int LeftJoyconIndex = 3;
    public const int RightJoyconIndex = 4;

    private LightionProfile lightProfileL = new();
    private LightionProfile lightProfileR = new();

    // todo: find the right value, this is placeholder
    private const byte INPUT_HID_ID = 0x01;

    public LegionGo()
    {
        // device specific settings
        ProductIllustration = "device_legion_go";

        // used to monitor OEM specific inputs
        vendorId = 0x17EF;
        productIds = [
            0x6182, // xinput
            0x6183, // dinput
            0x6184, // dual_dinput
            0x6185, // fps
            0x61EB, // xinput (2025 FW)
            0x61EC, // dinput (2025 FW)
            0x61ED, // dual_dinput (2025 FW)
            0x61EE, // fps (2025 FW)
        ];
        hidFilters = new()
        {
            { 0x6182, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // xinput (old FW)
            { 0x6183, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // dinput (old FW)
            { 0x6184, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // dual_dinput (old FW)
            { 0x6185, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // fps (old FW)

            { 0x61EB, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // xinput (2025 FW)
            { 0x61EC, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // dinput (2025 FW)
            { 0x61ED, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // dual_dinput (2025 FW)
            { 0x61EE, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // fps (2025 FW)
        };

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
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.FanOverride;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
        Capabilities |= DeviceCapabilities.BatteryChargeLimit;
        Capabilities |= DeviceCapabilities.OEMCPU;

        // battery bypass settings
        BatteryBypassMin = 80;
        BatteryBypassMax = 80;

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
            Guid = BetterBatteryGuid,
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
            Guid = BetterPerformanceGuid,
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
            Guid = BestPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 20.0d, 20.0d, 20.0d }
        });

        OEMChords.Add(new KeyboardChord("LegionR",
            [], [],
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new KeyboardChord("LegionL",
            [], [],
            false, ButtonFlags.OEM2
        ));

        // device specific layout
        DefaultLayout.AxisLayout[AxisLayoutFlags.RightPad] = [new MouseActions { MouseType = MouseActionsType.Move, Filtering = true, Sensivity = 15 }];

        DefaultLayout.ButtonLayout[ButtonFlags.RightPadClick] = [new MouseActions { MouseType = MouseActionsType.LeftButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.Low }];
        DefaultLayout.ButtonLayout[ButtonFlags.RightPadClickDown] = [new MouseActions { MouseType = MouseActionsType.RightButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.High }];
        DefaultLayout.ButtonLayout[ButtonFlags.B5] = [new ButtonActions { Button = ButtonFlags.R1 }];
        DefaultLayout.ButtonLayout[ButtonFlags.B6] = [new MouseActions { MouseType = MouseActionsType.MiddleButton }];
        DefaultLayout.ButtonLayout[ButtonFlags.B7] = [new MouseActions { MouseType = MouseActionsType.ScrollUp }];
        DefaultLayout.ButtonLayout[ButtonFlags.B8] = [new MouseActions { MouseType = MouseActionsType.ScrollDown }];
    }

    public override void OpenEvents()
    {
        base.OpenEvents();

        // manage events
        ControllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

        // raise events
        switch (ManagerFactory.powerProfileManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPowerProfile();
                break;
        }

        Device_Inserted();
    }

    private void Device_Removed()
    {
        // close device
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            device.MonitorDeviceEvents = false;
            device.Removed -= Device_Removed;

            try { device.Dispose(); } catch { }
        }

        // unload SapientiaUsb
        FreeSapientiaUsb();
    }

    private async void Device_Inserted(bool reScan = false)
    {
        // if you still want to automatically re-attach:
        if (reScan)
            await WaitUntilReady();

        // listen for events
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            device.MonitorDeviceEvents = true;
            device.Removed += Device_Removed;
            device.OpenDevice();

            foreach (var cmd in ControllerFactoryReset())
                device.Write(cmd);

            // enable gyros
            foreach (byte[] cmd in EnableControllerGyro("left"))
                device.Write(cmd);
            foreach (byte[] cmd in EnableControllerGyro("right"))
                device.Write(cmd);

            // disable built-in swap
            device.Write(ControllerLegionSwap(false));
        }

        // initialize SapientiaUsb
        Init();

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
    }

    public override bool IsReady()
    {
        // Wait until no LegionController is currently power cycling
        IEnumerable<LegionController> legionControllers = ControllerManager.GetPhysicalControllers<LegionController>();
        while (legionControllers.Any(controller => ControllerManager.PowerCyclers.ContainsKey(controller.GetContainerInstanceId())))
            Thread.Sleep(1000);

        IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 0);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (!hidFilters.TryGetValue(device.Attributes.ProductId, out HidFilter hidFilter))
                continue;

            if (device.Capabilities.UsagePage != hidFilter.UsagePage || device.Capabilities.Usage != hidFilter.Usage)
                continue;

            hidDevices[INPUT_HID_ID] = device;

            return true;
        }

        return false;
    }

    private int ControllerIndex(string side) => side.ToLower() switch
    {
        "left" => LeftJoyconIndex,
        "right" => RightJoyconIndex,
        _ => throw new ArgumentException($"Unknown controller '{side}'")
    };

    private IEnumerable<byte[]> EnableControllerGyro(string side)
    {
        int idx = ControllerIndex(side);
        yield return new byte[] { 0x05, 0x06, 0x6A, 0x02, (byte)idx, 0x01, 0x01 }; // enable
        yield return new byte[] { 0x05, 0x06, 0x6A, 0x07, (byte)idx, 0x02, 0x01 }; // high-quality
    }

    private IEnumerable<byte[]> DisableControllerGyro(string side)
    {
        int idx = ControllerIndex(side);
        yield return new byte[] { 0x05, 0x06, 0x6A, 0x07, (byte)idx, 0x01, 0x01 }; // disable high-quality
    }

    private IEnumerable<byte[]> ControllerFactoryReset()
    {
        // hex strings from Python, parsed into byte[]
        yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x01, 0x01 };
        yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x02, 0x01 };
        yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x03, 0x01 };
        yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x04, 0x01 };
    }

    private byte[] ControllerLegionSwap(bool enabled)
    {
        return new byte[]
        {
        0x05, 0x06, 0x69, 0x04, 0x01,
        (byte)(enabled ? 0x02 : 0x01),
        0x01
        };
    }

    private void ControllerManager_ControllerUnplugged(IController Controller, bool IsPowerCycling, bool WasTarget)
    {
        if (Controller is LegionController legionController)
            Device_Removed();
    }

    private void ControllerManager_ControllerPlugged(IController Controller, bool IsPowerCycling)
    {
        if (Controller is LegionController legionController)
            Device_Inserted(true);
    }

    private void QueryPowerProfile()
    {
        // manage events
        ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;

        PowerProfileManager_Applied(ManagerFactory.powerProfileManager.GetCurrent(), UpdateSource.Background);
    }

    private void PowerProfileManager_Initialized()
    {
        QueryPowerProfile();
    }

    protected override void QuerySettings()
    {
        // raise events
        SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetBoolean("BatteryChargeLimit"), false);

        base.QuerySettings();
    }

    protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "BatteryChargeLimit":
                SetBatteryChargeLimit(Convert.ToBoolean(value));
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary);
    }

    public override void Close()
    {
        // Reset the fan speed to default before device shutdown/restart
        SetFanFullSpeed(false);

        // restore default touchpad behavior
        SetTouchPadStatus(1);

        // close devices
        foreach (HidDevice hidDevice in hidDevices.Values)
            hidDevice.Dispose();
        hidDevices.Clear();

        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
        ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

        base.Close();
    }

    private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        if (profile.FanProfile.fanMode != FanMode.Hardware)
        {
            // default fanTable is ushort[] { 44, 48, 55, 60, 71, 79, 87, 87, 100, 100 }

            // prepare array of fan speeds
            ushort[] fanSpeeds = profile.FanProfile.fanSpeeds.Skip(1).Take(10).Select(speed => (ushort)speed).ToArray();
            FanTable fanTable = new(fanSpeeds);

            // update fan table
            SetFanTable(fanTable);
        }

        // get current fan mode and set it to the desired one if different
        int currentFanMode = GetSmartFanMode();
        if (Enum.IsDefined(typeof(LegionMode), profile.OEMPowerMode) && currentFanMode != profile.OEMPowerMode)
            SetSmartFanMode(profile.OEMPowerMode);
    }

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

    public override void set_long_limit(int limit)
    {
        SetCPUPowerLimit(CapabilityID.CPULongTermPowerLimit, limit);
        SetCPUPowerLimit(CapabilityID.CPUCrossLoadingPowerLimit, limit);
    }

    public override void set_short_limit(int limit)
    {
        SetCPUPowerLimit(CapabilityID.CPUShortTermPowerLimit, limit);
        SetCPUPowerLimit(CapabilityID.CPUPeakPowerLimit, limit);
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

            case ButtonFlags.L4:
                return "\u2215";
            case ButtonFlags.L5:
                return "\u2216";
            case ButtonFlags.R4:
                return "\u2214";
            case ButtonFlags.R5:
                return "\u2217";
            case ButtonFlags.B5:
                return "\u2213";
            case ButtonFlags.B6:
                return "\u27F7";
            case ButtonFlags.B7:
                return "\u27F0";
            case ButtonFlags.B8:
                return "\u27F1";
        }

        return defaultGlyph;
    }
}