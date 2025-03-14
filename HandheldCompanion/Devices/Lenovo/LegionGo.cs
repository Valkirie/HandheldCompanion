using HandheldCompanion.Actions;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
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

    private async Task<bool> GetFanFullSpeedAsync()
    {
        try
        {
            return await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "GetFeatureValue",
                new() { { "IDs", (int)CapabilityID.FanFullSpeed } },
                pdc => Convert.ToInt32(pdc["Value"].Value) == 1);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in GetFanFullSpeedAsync: {0}", ex.Message);
            return false; // or some default value
        }
    }

    public async Task SetFanFullSpeedAsync(bool enabled)
    {
        try
        {
            await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "SetFeatureValue",
                new()
                {
                { "IDs", (int)CapabilityID.FanFullSpeed },
                { "value", enabled ? 1 : 0 },
                });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetFanFullSpeedAsync: {0} with status: {1}", ex.Message, enabled);
        }
    }

    private async Task SetFanTable(FanTable fanTable)
    {
        try
        {
            await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_METHOD",
                "Fan_Set_Table",
                new() { { "FanTable", fanTable.GetBytes() } });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetFanTable: {0} with fanTable: {1}", ex.Message, string.Join(',', fanTable.GetBytes()));
        }
    }

    public static async Task<int> GetSmartFanModeAsync()
    {
        try
        {
            return await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "GetSmartFanMode",
                [],
                pdc => Convert.ToInt32(pdc["Data"].Value));
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in GetSmartFanModeAsync: {0}", ex.Message);
            return -1; // or some default value
        }
    }

    private async Task SetSmartFanMode(int fanMode)
    {
        try
        {
            await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "SetSmartFanMode",
                new() { { "Data", fanMode } });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetSmartFanMode: {0} with fanMode: {1}", ex.Message, fanMode);
        }
    }

    public async Task SetCPUPowerLimit(CapabilityID capabilityID, int limit)
    {
        try
        {
            await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "SetFeatureValue",
                new()
                {
                { "IDs", (int)capabilityID },
                { "value", limit },
                });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetCPUPowerLimit: {0} with capability: {1} and limit: {2}", ex.Message, capabilityID, limit);
        }
    }

    // InstantBootAc (0x03010001) controls the 80% power charge limit.
    // https://github.com/aarron-lee/LegionGoRemapper/blob/ab823f2042fc857cca856687a385a033d68c58bf/py_modules/legion_space.py#L138
    public async Task SetBatteryChargeLimit(bool enabled)
    {
        try
        {
            await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "SetFeatureValue",
                new()
                {
                { "IDs", (int)CapabilityID.InstantBootAc },
                { "value", enabled ? 1 : 0 },
                });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error in SetBatteryChargeLimit: {0} with status: {1}", ex.Message, enabled);
        }
    }

    public const byte INPUT_HID_ID = 0x04;

    public override bool IsOpen => hidDevices.ContainsKey(INPUT_HID_ID) && hidDevices[INPUT_HID_ID].IsOpen;

    public const int LeftJoyconIndex = 3;
    public const int RightJoyconIndex = 4;

    private LightionProfile lightProfileL = new();
    private LightionProfile lightProfileR = new();

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
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
        Capabilities |= DeviceCapabilities.BatteryChargeLimit;

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

        // manage events
        ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

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

        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        return true;
    }

    private void QueryPowerProfile()
    {
        PowerProfileManager_Applied(ManagerFactory.powerProfileManager.GetCurrent(), UpdateSource.Background);
    }

    private void PowerProfileManager_Initialized()
    {
        QueryPowerProfile();
    }

    private void QuerySettings()
    {
        SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetBoolean("BatteryChargeLimit"), false);
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    public override void Close()
    {
        // Reset the fan speed to default before device shutdown/restart
        SetFanFullSpeedAsync(false);

        // restore default touchpad behavior
        SetTouchPadStatus(1);

        // close devices
        foreach (HidDevice hidDevice in hidDevices.Values)
            hidDevice.Dispose();
        hidDevices.Clear();

        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

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

        // Legion XInput controller and other Legion devices shares the same USBHUB
        while (ControllerManager.PowerCyclers.Count > 0)
            Thread.Sleep(100);

        return DeviceHelper.IsDeviceAvailable(parent_guid, parent_instanceId);
    }

    private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        if (profile.FanProfile.fanMode != FanMode.Hardware)
        {
            // default fanTable
            // FanTable fanTable = new(new ushort[] { 44, 48, 55, 60, 71, 79, 87, 87, 100, 100 });

            FanTable fanTable = new([
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
            ]);

            // update fan table
            SetFanTable(fanTable).Wait();
        }

        Task<int> fanModeTask = Task.Run(GetSmartFanModeAsync);
        int fanMode = fanModeTask.Result;

        if (Enum.IsDefined(typeof(LegionMode), profile.OEMPowerMode) && fanMode != profile.OEMPowerMode)
            SetSmartFanMode(profile.OEMPowerMode).Wait();
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

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "BatteryChargeLimit":
                SetBatteryChargeLimit(Convert.ToBoolean(value));
                break;
        }
    }
}