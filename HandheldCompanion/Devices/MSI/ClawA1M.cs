using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Management;
using System.Numerics;
using System.Threading.Tasks;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class ClawA1M : IDevice
{
    private enum WMIEventCode
    {
        LaunchMcxMainUI = 41, // 0x00000029
        LaunchMcxOSD = 88, // 0x00000058
    }

    private readonly Dictionary<WMIEventCode, ButtonFlags> keyMapping = new()
    {
        { 0, ButtonFlags.None },
        { WMIEventCode.LaunchMcxMainUI, ButtonFlags.OEM1 },
        { WMIEventCode.LaunchMcxOSD, ButtonFlags.OEM2 },
    };

    private enum GamepadMode
    {
        Offline,
        XInput,
        DInput,
        MSI,
        Desktop,
        BIOS,
        TESTING,
    }

    private enum MKeysFunction
    {
        Macro,
        Combination,
    }

    private enum CommandType
    {
        EnterProfileConfig = 1,
        ExitProfileConfig = 2,
        WriteProfile = 3,
        ReadProfile = 4,
        ReadProfileAck = 5,
        Ack = 6,
        SwitchProfile = 7,
        WriteProfileToEEPRom = 8,
        ReadFirmwareVersion = 9,
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
        RGBControl = 224, // 0x000000E0
        CalibrationControl = 253, // 0x000000FD
        CalibrationAck = 254, // 0x000000FE
    }

    private ManagementEventWatcher? specialKeyWatcher;
    private Dictionary<byte, HidDevice> hidDevices = new();

    public ClawA1M()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw";

        // used to monitor OEM specific inputs
        _vid = 0x0DB0;
        _pid = 0x1901;

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
        Capabilities |= DeviceCapabilities.None;

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBetterBattery, Properties.Resources.PowerProfileMSIClawBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = new("961cc777-2547-4f9d-8174-7d86181b8a7a"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 20.0d, 20.0d, 20.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBetterPerformance, Properties.Resources.PowerProfileMSIClawBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            Guid = new("3af9B8d9-7c97-431d-ad78-34a8bfea439f"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 30.0d, 30.0d, 30.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMSIClawBestPerformance, Properties.Resources.PowerProfileMSIClawBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            Guid = new("ded574b5-45a0-4f42-8737-46345c09c238"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 35.0d, 35.0d, 35.0d }
        });

        OEMChords.Add(new DeviceChord("CLAW",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("QS",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("M1",             // unimplemented
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("M2",             // unimplemented
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM4
        ));
    }

    private void StartWatching()
    {
        try
        {
            var scope = new ManagementScope("\\\\.\\root\\WMI");
            specialKeyWatcher = new ManagementEventWatcher(scope, (EventQuery)(new WqlEventQuery("SELECT * FROM MSI_Event")));
            specialKeyWatcher.EventArrived += onWMIEvent;
            specialKeyWatcher.Start();
        }
        catch (Exception ex)
        {
            LogManager.LogError("Exception configuring MSI_Event monitor: {0}", ex.Message);
        }
    }

    private void StopWatching()
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
        WMIEventCode key = (WMIEventCode)(WMIEvent & (int)byte.MaxValue);

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

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid, 0);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            // improve detection maybe using if device.ReadFeatureData() ?
            if (device.Capabilities.InputReportByteLength != 64)
                continue;

            byte[] msg = { 15, 0, 0, 60, (byte)CommandType.SwitchMode, (byte)GamepadMode.XInput, (byte)MKeysFunction.Macro };
            device.Write(msg);
        }

        return true;
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // start WMI event monitor
        StartWatching();

        return true;
    }

    public override void Close()
    {
        // stop WMI event monitor
        StopWatching();

        base.Close();
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