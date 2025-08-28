using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Devices.MSI;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HidLibrary;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.IGCL.IGCLBackend;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class ClawA1M : IDevice
{
    protected enum WMIEventCode
    {
        LaunchMcxMainUI = 41, // 0x00000029
        LaunchMcxOSD = 88, // 0x00000058
    }

    protected readonly Dictionary<WMIEventCode, ButtonFlags> keyMapping = new()
    {
        { 0, ButtonFlags.None },
        { WMIEventCode.LaunchMcxMainUI, ButtonFlags.OEM1 },
        { WMIEventCode.LaunchMcxOSD, ButtonFlags.OEM2 },
    };

    protected enum GamepadMode
    {
        Offline,
        XInput,
        DirectInput,
        MSI,
        Desktop,
        BIOS,
        TESTING,
    }

    protected enum MKeysFunction
    {
        Macro,
        Combination,
    }

    public enum CommandType
    {
        EnterProfileConfig = 1,
        ExitProfileConfig = 2,
        WriteProfile = 3,
        ReadProfile = 4,
        ReadProfileAck = 5,
        Ack = 6,
        SwitchProfile = 7,
        WriteProfileToEEPRom = 8,
        SyncRGB = 9,
        ReadRGBStatusAck = 10, // 0x0000000A
        ReadCurrentProfile = 11, // 0x0000000B
        ReadCurrentProfileAck = 12, // 0x0000000C
        ReadRGBStatus = 13, // 0x0000000D
        SyncToROM = 34, // 0x00000022
        RestoreFromROM = 35, // 0x00000023
        SwitchMode = 36, // 0x00000024
        ReadGamepadMode = 38, // 0x00000026
        GamepadModeAck = 39, // 0x00000027
        ResetDevice = 40, // 0x00000028
        SetFeatureState = 44, // 0x0000002C
        DisableDevice = 45, // 0x0000002D
        SetMotionStatus = 47, // 0x0000002F
        MotionDataAck = 48, // 0x00000030
        RGBControl = 224, // 0x000000E0
        CalibrationControl = 253, // 0x000000FD
        CalibrationAck = 254, // 0x000000FE
    }

    public enum BatteryMode
    {
        BestForMobility,
        Balanced,
        BestForBattery,
        Custom,
    }

    public enum ShiftType
    {
        None = -1,
        SportMode = 0,
        ComfortMode = 1,
        GreenMode = 2,
        ECO = 3,
        User = 4,
    }

    public enum ShiftModeCalcType
    {
        Active,
        Deactive,
        ChangeToCurrentShiftType,
    }

    #region imports
    [DllImport("UEFIVaribleDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetUEFIVariableEx(string name, string guid, byte[] box);

    [DllImport("UEFIVaribleDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool SetUEFIVariableEx(string name, string guid, byte[] box, int len);
    #endregion

    private ManagementEventWatcher? specialKeyWatcher;

    // todo: find the right value, this is placeholder
    private const byte INPUT_HID_ID = 0x01;
    protected GamepadMode gamepadMode = GamepadMode.MSI;

    protected string Scope { get; set; } = "root\\WMI";
    protected string Path { get; set; } = "MSI_ACPI.InstanceName='ACPI\\PNP0C14\\0_0'";

    protected const int PID_XINPUT = 0x1901;
    protected const int PID_DINPUT = 0x1902;
    protected const int PID_TESTING = 0x1903;

    protected string MsIDCVarData = "DD96BAAF-145E-4F56-B1CF-193256298E99";

    protected int WmiMajorVersion;
    protected int WmiMinorVersion;

    protected bool isNew_EC => WmiMajorVersion > 1;

    private bool ClawOpen = false;
    public override bool IsOpen => DeviceOpen && ClawOpen;

    private static readonly DeviceVersion[] deviceVersions =
    {
        // Claw 1
        new DeviceVersion() { Firmware = 0x163, RGB = [0x01, 0xFA], M1 = [0x00, 0x7A], M2 = [0x01, 0x1F] },
        new DeviceVersion() { Firmware = 0x166, RGB = [0x02, 0x4A], M1 = [0x00, 0xBA], M2 = [0x01, 0x63] },
        new DeviceVersion() { Firmware = 0x167, RGB = [0x02, 0x4A], M1 = [0x00, 0xBA], M2 = [0x01, 0x63] },

        // Claw 8
        new DeviceVersion() { Firmware = 0x211, RGB = [0x01, 0xFA], M1 = [0x00, 0x7A], M2 = [0x01, 0x1F] },
        new DeviceVersion() { Firmware = 0x217, RGB = [0x02, 0x4A], M1 = [0x00, 0xBA], M2 = [0x01, 0x63] },
        new DeviceVersion() { Firmware = 0x219, RGB = [0x02, 0x4A], M1 = [0x00, 0xBA], M2 = [0x01, 0x63] },

        // Claw A8
        new DeviceVersion() { Firmware = 0x308, RGB = [0x02, 0x4A], M1 = [0x00, 0xBA], M2 = [0x01, 0x63] },
    };

    protected int Firmware;
    public DeviceVersion? FirmwareDevice => deviceVersions.MinBy(version => Math.Abs(version.Firmware - Firmware));
    public override bool IsSupported => FirmwareDevice?.Firmware == Firmware;

    public ClawA1M()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw";

        // used to monitor OEM specific inputs
        vendorId = 0x0DB0;
        productIds = [PID_XINPUT, PID_DINPUT, PID_TESTING];
        hidFilters = new()
        {
            { PID_XINPUT, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) },
            { PID_DINPUT, new HidFilter(unchecked((short)0xFFF0), unchecked((short)0x0040)) },
        };

        // https://www.intel.com/content/www/us/en/products/sku/236847/intel-core-ultra-7-processor-155h-24m-cache-up-to-4-80-ghz/specifications.html
        nTDP = new double[] { 30, 30, 35 };
        cTDP = new double[] { 20, 45 };
        GfxClock = new double[] { 100, 2250 };
        CpuClock = 4800;

        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
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
        Capabilities |= DeviceCapabilities.FanOverride;
        Capabilities |= DeviceCapabilities.OEMCPU;
        Capabilities |= DeviceCapabilities.BatteryChargeLimit;
        Capabilities |= DeviceCapabilities.BatteryChargeLimitPercent;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Ambilight;

        // battery bypass settings
        BatteryBypassMin = 60;
        BatteryBypassMax = 100;
        BatteryBypassStep = 20;

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBetterBattery, Properties.Resources.PowerProfileMSIClawBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = BetterBatteryGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 20.0d, 20.0d, 20.0d },
            IntelEnduranceGamingEnabled = true,
            IntelEnduranceGamingPreset = (int)ctl_3d_endurance_gaming_mode_t.MAX // 30fps
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBetterPerformance, Properties.Resources.PowerProfileMSIClawBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            Guid = BetterPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 30.0d, 30.0d, 30.0d },
            IntelEnduranceGamingEnabled = true,
            IntelEnduranceGamingPreset = (int)ctl_3d_endurance_gaming_mode_t.PERFORMANCE // 60fps
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBestPerformance, Properties.Resources.PowerProfileMSIClawBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            Guid = BestPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 35.0d, 35.0d, 35.0d },
            IntelEnduranceGamingEnabled = false,
            IntelEnduranceGamingPreset = (int)ctl_3d_endurance_gaming_mode_t.PERFORMANCE // GPU Auto TDP is Off, FPS depends on the game, and it can be up to 120
        });

        OEMChords.Add(new KeyboardChord("CLAW",
            [], [],
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new KeyboardChord("QS",
            [], [],
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new KeyboardChord("M1",
            [], [],
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new KeyboardChord("M2",
            [], [],
            false, ButtonFlags.OEM4
        ));

        OEMChords.Add(new KeyboardChord("LButton",
            [KeyCode.LButton | KeyCode.OemClear],
            [KeyCode.LButton | KeyCode.OemClear],
            true, ButtonFlags.OEM5
        ));

        // prepare hotkeys
        DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
        DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = true;
    }

    public override bool Open()
    {
        bool success = base.Open();
        if (!success)
            return false;

        LogManager.LogInformation("Device Firmware: {0:X4}, {1}", Firmware, IsSupported ? "Supported" : "Unsupported");

        SetShiftMode(ShiftModeCalcType.Deactive);

        // OverBoost
        int uefiVariableEx = 0;
        byte[] box = GetMsiDCVarData(ref uefiVariableEx);
        if (uefiVariableEx != 0)
        {
            if (box[1] == (byte)0)
            {
                InitOverBoost(true);
                Thread.Sleep(600);
            }

            /*
            // Check if OverBoostSup is enabled
            bool OverBoostSup = GetOverBoostSup();
            if (OverBoostSup)
            {
                // Check if OverBoost is enabled
                bool OverBoost = GetOverBoost();
                if (OverBoost)
                {
                    // disable OverBoost ?
                }
            }
            */
        }

        // make sure M1/M2 are recognized as buttons
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            Thread.Sleep(300);
            device.Write(GetM12(true), 0, 64);
            Thread.Sleep(500);
            device.Write(GetM12(false), 0, 64);
            Thread.Sleep(500);
            SyncToROM();
            Thread.Sleep(500);
            SwitchMode(gamepadMode);
            Thread.Sleep(2000);
        }

        // set flag
        ClawOpen = true;

        // prepare WMI
        GetWMI();

        // unlock TDP
        set_long_limit(35);
        set_short_limit(35);

        // start WMI event monitor
        StartWatching();

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

    protected override void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        byte[] fanTable = new byte[8];
        if (profile.FanProfile.fanMode == FanMode.Software)
        {
            fanTable[0] = (byte)profile.FanProfile.fanSpeeds[4]; // ?
            fanTable[1] = (byte)profile.FanProfile.fanSpeeds[0]; // 0%
            fanTable[2] = (byte)profile.FanProfile.fanSpeeds[2]; // 20%
            fanTable[3] = (byte)profile.FanProfile.fanSpeeds[5]; // 50%
            fanTable[4] = (byte)profile.FanProfile.fanSpeeds[6]; // 60%
            fanTable[5] = (byte)profile.FanProfile.fanSpeeds[8]; // 80%
            fanTable[6] = (byte)profile.FanProfile.fanSpeeds[9]; // 90%
            fanTable[7] = (byte)profile.FanProfile.fanSpeeds[10]; // 100%
        }
        else
        {
            // restore default fan table
            fanTable = new byte[8] { 49, 0, 40, 49, 58, 67, 75, 75 };
        }

        // update fan table
        SetFanTable(fanTable);

        // update fan mode
        SetFanControl(profile.FanProfile.fanMode != FanMode.Hardware);

        // MSI Center, API_UserScenario
        bool IsDcMode = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        if (profile.Guid == BetterBatteryGuid)
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.ECO);
        }
        else if (profile.Guid == BetterPerformanceGuid)
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.GreenMode);
        }
        else if (profile.Guid == BestPerformanceGuid)
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.SportMode);
        }
        else
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.SportMode);
        }
    }

    private void ControllerManager_ControllerPlugged(Controllers.IController Controller, bool IsPowerCycling)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Inserted(true);
    }

    private void ControllerManager_ControllerUnplugged(Controllers.IController Controller, bool IsPowerCycling, bool WasTarget)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Removed();
    }

    private int LEDBrightness = 100;
    private Color LEDMainColor = Colors.Black;
    private Color LEDSecondColor = Colors.Black;

    protected override void QuerySettings()
    {
        // raise events
        SettingsManager_SettingValueChanged("MSIClawControllerIndex", ManagerFactory.settingsManager.GetInt("MSIClawControllerIndex"), false);
        SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetInt("BatteryChargeLimit"), false);
        SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetInt("BatteryChargeLimitPercent"), false);

        base.QuerySettings();
    }

    protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "BatteryChargeLimit":
                bool enabled = Convert.ToBoolean(value);
                SetBatteryMaster(enabled);
                break;
            case "BatteryChargeLimitPercent":
                int percent = Convert.ToInt32(value);
                SetBatteryChargeLimit(percent);
                break;
            case "MSIClawControllerIndex":
                {
                    gamepadMode = (GamepadMode)Convert.ToInt32(value);
                    SwitchMode(gamepadMode);
                }
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary);
    }

    public override void Close()
    {
        SetFanFullSpeed(false);

        // stop WMI event monitor
        StopWatching();

        // configure controller to Desktop
        SwitchMode(GamepadMode.Desktop);

        // close devices
        foreach (HidDevice hidDevice in hidDevices.Values)
            hidDevice.Dispose();
        hidDevices.Clear();

        // set flag
        ClawOpen = false;

        // manage events
        ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

        base.Close();
    }

    protected byte[] GetMsiDCVarData(ref int uefiVariableEx)
    {
        byte[] box = new byte[4096];
        uefiVariableEx = GetUEFIVariableEx("MsiDCVarData", MsIDCVarData, box);
        return box;
    }

    protected void InitOverBoost(bool enabled)
    {
        int uefiVariableEx = 0;
        byte[] box = GetMsiDCVarData(ref uefiVariableEx);
        Thread.Sleep(600);

        // set value
        box[1] = (byte)(enabled ? 1 : 0);
        SetUEFIVariableEx("MsiDCVarData", MsIDCVarData, box, uefiVariableEx);
        Thread.Sleep(600);
    }

    public async void SetOverBoost(bool enabled)
    {
        int uefiVariableEx = 0;
        byte[] box = GetMsiDCVarData(ref uefiVariableEx);
        Thread.Sleep(600);

        // set value
        box[6] = (byte)(enabled ? 1 : 0);
        SetUEFIVariableEx("MsiDCVarData", MsIDCVarData, box, uefiVariableEx);
        Thread.Sleep(600);

        Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = Properties.Resources.Dialog_ForceRestartTitle,
            Content = Properties.Resources.Dialog_ForceRestartDesc,
            DefaultButton = ContentDialogButton.Close,
            CloseButtonText = Properties.Resources.Dialog_No,
            PrimaryButtonText = Properties.Resources.Dialog_Yes
        }.ShowAsync();

        await dialogTask; // sync call

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                DeviceUtils.RestartComputer();
                break;
            case ContentDialogResult.Secondary:
                break;
        }
    }

    public bool HasOverBoost()
    {
        int uefiVariableEx = 0;
        byte[] box = GetMsiDCVarData(ref uefiVariableEx);
        if (uefiVariableEx != 0)
            return box[1] != 0;
        return false;
    }

    public bool GetOverBoost()
    {
        int uefiVariableEx = 0;
        byte[] box = GetMsiDCVarData(ref uefiVariableEx);
        if (uefiVariableEx != 0)
            return box[6] != 0;
        return false;
    }

    public bool GetOverBoostSup()
    {
        int uefiVariableEx = 0;
        byte[] box = GetMsiDCVarData(ref uefiVariableEx);
        if (uefiVariableEx != 0)
            return box[7] != 0;
        return false;
    }

    protected void GetWMI()
    {
        byte iDataBlockIndex = 1;

        byte[] dataWMI = WMI.Get(Scope, Path, "Get_WMI", iDataBlockIndex, 32, out bool readWMI);
        if (dataWMI.Length > 2 && dataWMI[1] >= (byte)2)
        {
            this.WmiMajorVersion = (int)dataWMI[1];
            this.WmiMinorVersion = (int)dataWMI[2];
        }
    }

    protected bool SetMotionStatus(bool enabled)
    {
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            byte[] msg = { 15, 0, 0, 60, (byte)CommandType.SetMotionStatus, (byte)(enabled ? 1 : 0) };
            if (device.Write(msg, 0, 64))
            {
                LogManager.LogInformation("Successfully SetMotionStatus to {0}", enabled);
                return true;
            }
            else
            {
                LogManager.LogWarning("Failed to SetMotionStatus to {0}", enabled);
                return false;
            }
        }

        return false;
    }

    protected bool SwitchMode(GamepadMode gamepadMode)
    {
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            byte[] msg = { 15, 0, 0, 60, (byte)CommandType.SwitchMode, (byte)gamepadMode, (byte)MKeysFunction.Macro };
            if (device.Write(msg, 0, 64))
            {
                LogManager.LogInformation("Successfully switched controller mode to {0}", gamepadMode);
                return true;
            }
            else
            {
                LogManager.LogWarning("Failed to switch controller mode to {0}", gamepadMode);
                return false;
            }
        }

        return false;
    }

    protected bool SyncToROM()
    {
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            byte[] msg = { 15, 0, 0, 60, (byte)CommandType.SyncToROM };
            if (device.Write(msg, 0, 64))
            {
                LogManager.LogInformation("Successfully synced to ROM");
                return true;
            }
            else
            {
                LogManager.LogWarning("Failed to sync to ROM");
                return false;
            }
        }

        return false;
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

            // update firmware
            Firmware = device.Attributes.Version;

            return true;
        }

        return false;
    }

    public override bool SetLedBrightness(int brightness)
    {
        // store value
        LEDBrightness = brightness;

        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            return device.Write(GetRGB(brightness, LEDMainColor, LEDSecondColor), 0, 64);

        return false;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
    {
        // store values
        LEDMainColor = MainColor;
        LEDSecondColor = SecondaryColor;

        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            switch (level)
            {
                case LEDLevel.SolidColor:
                    return device.Write(GetRGB(LEDBrightness, MainColor, MainColor), 0, 64);
                case LEDLevel.Ambilight:
                    return device.Write(GetRGB(LEDBrightness, MainColor, SecondaryColor), 0, 64);
            }
        }

        return false;
    }

    private byte[] GetRGB(double brightness, Color MainColor, Color SecondaryColor)
    {
        // grab the right array (or null if no device)
        byte[]? RGBdata = FirmwareDevice?.RGB;

        // pick actual values
        byte add1 = RGBdata != null ? RGBdata[0] : (byte)0x01;
        byte add2 = RGBdata != null ? RGBdata[1] : (byte)0xFA;

        List<byte> data = new List<byte>
        {
            // Preamble
            0x0F, 0x00, 0x00, 0x3C,

            // Write first profile
            0x21, 0x01,

            // Start at
            add1, add2,

            // Write 31 bytes
            0x20,

            // Index, Frame num, Effect, Speed, Brightness
            0x00, 0x01, 0x09, 0x03,
            (byte)Math.Max(0, Math.Min(100, (int)brightness))
        };

        // Append [red, green, blue] * 9
        // right is 0, 1, 2, 3
        // left is 4, 5, 6, 7

        for (int i = 0; i < 8; i++)
        {
            data.Add(i < 4 ? SecondaryColor.R : MainColor.R);
            data.Add(i < 4 ? SecondaryColor.G : MainColor.G);
            data.Add(i < 4 ? SecondaryColor.B : MainColor.B);
        }

        return data.ToArray();
    }

    private byte[] GetM12(bool useM1)
    {
        // grab the right array (or null if no device)
        byte[]? data = useM1
            ? FirmwareDevice?.M1
            : FirmwareDevice?.M2;

        // choose your two fallback bytes
        byte defaultAdd1 = useM1 ? (byte)0x00 : (byte)0x01;
        byte defaultAdd2 = useM1 ? (byte)0x7A : (byte)0x1F;

        // pick actual values
        byte add1 = data != null ? data[0] : defaultAdd1;
        byte add2 = data != null ? data[1] : defaultAdd2;

        return new byte[]
        {
        0x0F, 0x00, 0x00, 0x3C,
        0x21, 0x01,
        add1, add2,
        0x05, 0x01,
        0x00, 0x00,
        0x11, 0x00
        };
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

            SwitchMode(gamepadMode);
        }
    }

    protected void StartWatching()
    {
        try
        {
            var scope = new ManagementScope("\\\\.\\root\\WMI");
            specialKeyWatcher = new ManagementEventWatcher(scope, new WqlEventQuery("SELECT * FROM MSI_Event"));
            specialKeyWatcher.EventArrived += onWMIEvent;
            specialKeyWatcher.Start();
        }
        catch (Exception ex)
        {
            LogManager.LogError("Exception configuring MSI_Event monitor: {0}", ex.Message);
        }
    }

    protected void StopWatching()
    {
        if (specialKeyWatcher == null)
        {
            return;
        }

        try
        {
            specialKeyWatcher.EventArrived -= onWMIEvent;
            specialKeyWatcher.Stop();
            specialKeyWatcher.Dispose();
        }
        catch (Exception ex)
        {
            LogManager.LogError("Exception unconfiguring MSI_Event monitor: {0}", ex.Message);
        }

        specialKeyWatcher = null;
    }

    private void onWMIEvent(object sender, EventArrivedEventArgs e)
    {
        int WMIEvent = Convert.ToInt32(e.NewEvent.Properties["MSIEvt"].Value);
        WMIEventCode key = (WMIEventCode)(WMIEvent & byte.MaxValue);

        // LogManager.LogInformation("Received MSI WMI Event Code {0}", (int)key);

        if (!keyMapping.ContainsKey(key))
            return;

        // get button
        ButtonFlags button = keyMapping[key];
        switch (key)
        {
            default:
            case WMIEventCode.LaunchMcxMainUI:  // MSI Claw: Click
            case WMIEventCode.LaunchMcxOSD:     // Quick Settings: Click
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

    private void SetBatteryMaster(bool enable)
    {
        // Data block index specific to battery mode settings
        byte dataBlockIndex = 215;

        // Get the current battery data (1 byte) from the device
        byte[] data = WMI.Get(Scope, Path, "Get_Data", dataBlockIndex, 1, out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enable);

        // Build the complete 32-byte package
        byte[] fullPackage = new byte[32];
        fullPackage[0] = dataBlockIndex;
        fullPackage[1] = data[0];

        // Set the battery mode using the package.
        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    private bool GetBatteryChargeLimit(ref byte currentValue)
    {
        // Data block index specific to battery mode settings
        byte dataBlockIndex = 215;

        // Get the current battery data (1 byte) from the device
        byte[] data = WMI.Get(Scope, Path, "Get_Data", dataBlockIndex, 1, out bool readSuccess);
        if (readSuccess)
            currentValue = data[0];

        return readSuccess;
    }

    private void SetBatteryChargeLimit(int chargeLimit)
    {
        // Data block index specific to battery mode settings
        byte dataBlockIndex = 215;

        // Get the current battery data (1 byte) from the device
        byte currentValue = 0;
        GetBatteryChargeLimit(ref currentValue);

        // Update mask
        byte mask = (byte)((uint)currentValue & (uint)sbyte.MaxValue);

        // Build the complete 32-byte package
        byte[] fullPackage = new byte[32];
        fullPackage[0] = dataBlockIndex;
        fullPackage[1] = (byte)(currentValue - mask + chargeLimit);

        // Set the battery mode using the package.
        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    private void SetFanTable(byte[] fanTable)
    {
        /*
         * iDataBlockIndex = 1; // CPU
         * iDataBlockIndex = 2; // GPU
         */

        for (byte iDataBlockIndex = 1; iDataBlockIndex <= 2; iDataBlockIndex++)
        {
            // default: 49, 0, 40, 49, 58, 67, 75, 75
            byte[] data = WMI.Get(Scope, Path, "Get_Fan", iDataBlockIndex, 32, out bool readFan);

            // Build the complete 32-byte package:
            byte[] fullPackage = new byte[32];
            fullPackage[0] = iDataBlockIndex;
            Array.Copy(fanTable, 0, fullPackage, 1, fanTable.Length);

            WMI.Set(Scope, Path, "Set_Fan", fullPackage);
        }
    }

    public override void set_long_limit(int limit)
    {
        SetCPUPowerLimit(80, limit);
    }

    public override void set_short_limit(int limit)
    {
        SetCPUPowerLimit(81, limit);
    }

    protected void SetCPUPowerLimit(int iDataBlockIndex, int limit)
    {
        /*
         * iDataBlockIndex = 80; // Long (SPL | PL1)
         * iDataBlockIndex = 81; // Short (sPPT | PL2)
         * iDataBlockIndex = 82; // Very short (fPPT AMD only)
         */

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = (byte)iDataBlockIndex;
        fullPackage[1] = (byte)limit;

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        byte iDataBlockIndex = 1;

        byte[] data = WMI.Get(Scope, Path, "Get_AP", iDataBlockIndex, WMI.GetAPLength(iDataBlockIndex), out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enable);

        // update data block index
        iDataBlockIndex = 212;

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public void SetFanFullSpeed(bool enable)
    {
        byte iDataBlockIndex = 152;

        byte[] data = WMI.Get(Scope, Path, "Get_Data", iDataBlockIndex, 1, out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enable);

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public int GetShiftValue()
    {
        byte iDataBlockIndex = 0;

        // Optional: decode the value if needed.
        // bool isSupported = (shiftValue & 128) != 0;
        // bool isActive = (shiftValue & 64) != 0;
        // int modeValue = shiftValue & 0x3F; // lower 6 bits
        byte[] data = WMI.Get(Scope, Path, "Get_AP", iDataBlockIndex, WMI.GetAPLength(iDataBlockIndex), out bool readSuccess);
        if (readSuccess)
            return data[2];

        return -1;
    }

    public void SetShiftValue(int newShiftValue)
    {
        byte iDataBlockIndex = 210;

        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        fullPackage[1] = (byte)newShiftValue;

        // Write the package back to the EC.
        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public bool IsShiftSupported()
    {
        int currentValue = GetShiftValue();
        return (currentValue & 128) != 0;
    }

    public void SetShiftMode(ShiftModeCalcType calcType, ShiftType shiftType = ShiftType.None)
    {
        if (!IsShiftSupported())
            return;

        int ShiftModeValueInEC = GetShiftValue();
        ShiftModeValueInEC &= 195;

        switch (calcType)
        {
            case ShiftModeCalcType.Active:
                ShiftModeValueInEC |= 128;
                ShiftModeValueInEC |= 64;
                break;
            case ShiftModeCalcType.Deactive:
                ShiftModeValueInEC |= 128;
                ShiftModeValueInEC &= 191;
                break;
            case ShiftModeCalcType.ChangeToCurrentShiftType:
                ShiftModeValueInEC |= 192;
                ShiftModeValueInEC &= 252;
                switch (shiftType)
                {
                    case ShiftType.SportMode:
                        ShiftModeValueInEC += 4;
                        break;
                    case ShiftType.ComfortMode:
                        break;
                    case ShiftType.GreenMode:
                        ++ShiftModeValueInEC;
                        break;
                    case ShiftType.ECO:
                        ShiftModeValueInEC += 2;
                        break;
                    case ShiftType.User:
                        ShiftModeValueInEC += 3;
                        break;
                }
                break;
        }

        SetShiftValue(ShiftModeValueInEC);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\uE010";
            case ButtonFlags.OEM2:
                return "\uE011";
            case ButtonFlags.OEM3:
                return "\u2212";
            case ButtonFlags.OEM4:
                return "\u2213";
        }

        return defaultGlyph;
    }
}