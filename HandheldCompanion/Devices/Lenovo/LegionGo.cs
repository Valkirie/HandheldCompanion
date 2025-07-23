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

    public enum RgbMode : byte
    {
        Solid = 1,
        Pulse = 2,
        Dynamic = 3,
        Spiral = 4,
    }

    #region WMI
    protected bool GetFanFullSpeed()
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

    protected void SetFanTable(FanTable fanTable)
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

    protected int GetSmartFanMode()
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

    protected void SetSmartFanMode(int fanMode)
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

    protected byte ClampByte(int v) => (byte)Math.Max(0, Math.Min(255, v));
    protected byte[] ConvertHex(string hex) => Convert.FromHexString(hex);

    // todo: find the right value, this is placeholder
    protected const byte INPUT_HID_ID = 0x01;
    protected bool IsPassthrough = false;

    public LegionGo()
    {
        // battery bypass settings
        BatteryBypassMin = 80;
        BatteryBypassMax = 80;

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

        // device specific capacities
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.FanOverride;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
        Capabilities |= DeviceCapabilities.BatteryChargeLimit;
        Capabilities |= DeviceCapabilities.OEMCPU;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Breathing;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;
        DynamicLightingCapabilities |= LEDLevel.Wheel;

        OEMChords.Add(new KeyboardChord("LegionR", [], [], false, ButtonFlags.OEM1));
        OEMChords.Add(new KeyboardChord("LegionL", [], [], false, ButtonFlags.OEM2));
    }

    public override bool IsReady()
    {
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

    public override void Close()
    {
        // manage events
        ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;
        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;

        // close devices
        foreach (HidDevice hidDevice in hidDevices.Values)
            hidDevice.Dispose();
        hidDevices.Clear();

        // Reset the fan speed to default before device shutdown/restart
        SetFanFullSpeed(false);

        // restore default touchpad behavior
        SetPassthrough(false);

        // unload SapientiaUsb
        FreeSapientiaUsb();

        base.Close();
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
        SettingsManager_SettingValueChanged("LegionControllerPassthrough", ManagerFactory.settingsManager.GetBoolean("LegionControllerPassthrough"), false);

        base.QuerySettings();
    }

    protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "BatteryChargeLimit":
                SetBatteryChargeLimit(Convert.ToBoolean(value));
                break;
            case "LegionControllerPassthrough":
                SetPassthrough(Convert.ToBoolean(value));
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary);
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

    private void ControllerManager_ControllerPlugged(IController Controller, bool IsPowerCycling)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Inserted(true);
    }

    protected virtual void Device_Inserted(bool reScan = false)
    {
        // initialize SapientiaUsb
        Init();
    }

    private void ControllerManager_ControllerUnplugged(IController Controller, bool IsPowerCycling, bool WasTarget)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Removed();
    }

    protected virtual void Device_Removed()
    {
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            device.MonitorDeviceEvents = false;
            device.Removed -= Device_Removed;
            try { device.Dispose(); } catch { }
        }

        // unload SapientiaUsb
        FreeSapientiaUsb();
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

    protected byte[] ControllerLegionSwap(bool enabled)
    {
        return new byte[]
        {
            0x05, 0x06, 0x69, 0x04, 0x01,
            (byte)(enabled ? 0x02 : 0x01),
            0x01
        };
    }

    public virtual void SetPassthrough(bool enabled)
    {
        IsPassthrough = enabled;
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