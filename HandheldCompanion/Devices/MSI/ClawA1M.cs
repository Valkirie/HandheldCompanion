using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media;

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

    private ManagementEventWatcher? specialKeyWatcher;

    // todo: find the right value, this is placeholder
    private const byte INPUT_HID_ID = 0x01;
    protected GamepadMode gamepadMode = GamepadMode.Offline;

    protected string Scope { get; set; } = "root\\WMI";
    protected string Path { get; set; } = "MSI_ACPI.InstanceName='ACPI\\PNP0C14\\0_0'";

    protected const int PID_XINPUT = 0x1901;
    protected const int PID_DINPUT = 0x1902;
    protected const int PID_TESTING = 0x1903;

    protected int WmiMajorVersion;
    protected int WmiMinorVersion;

    protected bool isNew_EC => WmiMajorVersion > 1;

    public ClawA1M()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw";

        // used to monitor OEM specific inputs
        vendorId = 0x0DB0;
        productIds = [PID_XINPUT, PID_DINPUT, PID_TESTING];

        // https://www.intel.com/content/www/us/en/products/sku/236847/intel-core-ultra-7-processor-155h-24m-cache-up-to-4-80-ghz/specifications.html
        nTDP = new double[] { 28, 28, 65 };
        cTDP = new double[] { 20, 65 };
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
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBetterBattery, Properties.Resources.PowerProfileMSIClawBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = BetterBatteryGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 20.0d, 20.0d, 20.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBetterPerformance, Properties.Resources.PowerProfileMSIClawBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            Guid = BetterPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 30.0d, 30.0d, 30.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBestPerformance, Properties.Resources.PowerProfileMSIClawBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            Guid = BestPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 35.0d, 35.0d, 35.0d }
        });

        OEMChords.Add(new KeyboardChord("CLAW",
            [], [],
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new KeyboardChord("QS",
            [], [],
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new KeyboardChord("M1",             // unimplemented
            [], [],
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new KeyboardChord("M2",             // unimplemented
            [], [],
            false, ButtonFlags.OEM4
        ));
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // start WMI event monitor
        GetWMI();
        StartWatching();
        Device_Inserted();

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ControllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

        // raise events
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

    private void ControllerManager_ControllerPlugged(Controllers.IController Controller, bool IsPowerCycling)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Removed();
    }

    private void ControllerManager_ControllerUnplugged(Controllers.IController Controller, bool IsPowerCycling, bool WasTarget)
    {
        // hack, force rescan
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
        {
            switch (Controller.GetProductID())
            {
                case PID_XINPUT:
                    ManagerFactory.deviceManager.RefreshXInput();
                    break;
                case PID_DINPUT:
                    ManagerFactory.deviceManager.RefreshDInput();
                    break;
            }
        }
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        SettingsManager_SettingValueChanged("MSIClawControllerIndex", ManagerFactory.settingsManager.GetInt("MSIClawControllerIndex"), false);
        SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetInt("BatteryChargeLimit"), false);
        SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetInt("BatteryChargeLimitPercent"), false);
    }

    protected virtual void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "MSIClawControllerIndex":
                {
                    gamepadMode = (GamepadMode)Convert.ToInt32(value);
                    SwitchMode(gamepadMode);
                }
                break;
        }
    }

    public override void Close()
    {
        // stop WMI event monitor
        StopWatching();

        // configure controller to Desktop
        SwitchMode(GamepadMode.Desktop);

        // close devices
        foreach (HidDevice hidDevice in hidDevices.Values)
            hidDevice.Dispose();
        hidDevices.Clear();

        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

        base.Close();
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
            if (device.Write(msg))
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
            if (device.Write(msg))
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

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 0);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            // improve detection maybe using if device.ReadFeatureData() ?
            if (device.Capabilities.InputReportByteLength != 64 || device.Capabilities.OutputReportByteLength != 64)
                continue;

            hidDevices[INPUT_HID_ID] = device;

            return true;
        }

        return false;
    }

    public override bool SetLedBrightness(int brightness)
    {
        Color LEDMainColor = ManagerFactory.settingsManager.GetColor("LEDMainColor");

        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            return device.Write(SetRgbCmd(brightness, LEDMainColor.R, LEDMainColor.G, LEDMainColor.B));

        return false;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, DeviceUtils.LEDLevel level, int speed = 100)
    {
        int LEDBrightness = ManagerFactory.settingsManager.GetInt("LEDBrightness");

        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            return device.Write(SetRgbCmd(LEDBrightness, MainColor.R, MainColor.G, MainColor.B));

        return false;
    }

    private byte[] SetRgbCmd(double brightness, byte red, byte green, byte blue)
    {
        List<byte> data = new List<byte>
        {
            // Preamble
            0x0F, 0x00, 0x00, 0x3C,

            // Write first profile
            0x21, 0x01,

            // Start at
            0x01, 0xFA,

            // Write 31 bytes
            0x20,

            // Index, Frame num, Effect, Speed, Brightness
            0x00, 0x01, 0x09, 0x03,
            (byte)Math.Max(0, Math.Min(100, (int)brightness))
        };

        // Append [red, green, blue] * 9
        for (int i = 0; i < 9; i++)
        {
            data.Add(red);
            data.Add(green);
            data.Add(blue);
        }

        return data.ToArray();
    }

    private void Device_Removed()
    {
        // close device
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            device.Removed -= Device_Removed;
            device.Dispose();
        }

        while (!IsReady())
            Task.Delay(250).Wait();

        Device_Inserted();
    }

    private void Device_Inserted()
    {
        // listen for events
        if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
        {
            device.Removed += Device_Removed;
            device.MonitorDeviceEvents = true;

            /*
            GamepadMode currentMode = GamepadMode.Offline;
            switch (device.Attributes.ProductId)
            {
                case PID_XINPUT:
                    currentMode = GamepadMode.XInput;
                    break;
                case PID_DINPUT:
                    currentMode = GamepadMode.DirectInput;
                    break;
            }
            */

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