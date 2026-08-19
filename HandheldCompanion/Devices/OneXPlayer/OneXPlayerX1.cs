using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc.Threading.Tasks;
using HandheldCompanion.Models;
using HandheldCompanion.Sensors;
using HandheldCompanion.Shared;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class OneXPlayerX1 : OneXAOKZOE
{
    protected const int VendorHidId = 0;

    // OneXPlayer classic vendor chip (0x1A86 / 0xFE00). The button/remap traffic
    // lives on the vendor-defined collection MI_02 (usage page 0xFF00, usage 0x0001).
    protected const int PID_VENDOR = 0xFE00;

    private SerialPort? _serialPort; // COM3 SerialPort for Device control of OneXPlayer

    private const byte FrameMarker = 0x3F;
    private const byte ButtonCommandId = 0xB2;
    private const byte StatusCommandId = 0xB8;
    protected const byte VibrationCommandId = 0xB3;
    private readonly bool[] _buttonStates = new bool[0x25];

    // Enable COM Port for LED Control
    public bool EnableSerialPort = true;
    public string SerialPortDeviceName = "CH340";
    public int SerialPortBaudRate = 115200;
    public Parity SerialPortParity = Parity.Even;
    public int SerialPortDataBits = 8;
    public StopBits SerialPortStopBits = StopBits.Two;

    public int TaskDelay = 200;

    private readonly SerialQueue _queue = new SerialQueue();

    // Local Values for LED Values
    private bool LEDEnabled;
    private int LEDBrightness;
    private LEDLevel LEDCurrentLevel;
    private Color LEDControllerColor;
    private Color LEDBackColor;
    private LEDPreset? LEDPreset;

    // Battery Protection
    public ushort ECBatteryLimitAddress = 0x4A3;
    public ushort ECBypassChargingAddress = 0x4A4;

    public OneXPlayerX1()
    {
        vendorId = 0x1A86;
        productIds = [PID_VENDOR];
        hidFilters = new()
        {
            { PID_VENDOR, new HidFilter(unchecked((short)0xFF00), unchecked(0x0001)) },
        };

        // device specific settings
        ProductIllustration = "device_onexplayer_x1";
        ProductModel = "ONEXPLAYERX1";

        GyroMatrix = new()
        {
            Axis = new Vector3(1.0f, -1.0f, 1.0f),
            AxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            }
        };

        AcceleroMatrix = new()
        {
            Axis = new Vector3(1.0f, -1.0f, -1.0f),
            AxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            }
        };

        // device specific capacities
        Capabilities = DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
        Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDColor;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.LEDPreset;

        if (CheckIsBatteryProtectionSupported())
        {
            Capabilities |= DeviceCapabilities.BatteryChargeLimit;
            Capabilities |= DeviceCapabilities.BatteryChargeLimitPercent;
            Capabilities |= DeviceCapabilities.BatteryBypassCharging;

            BatteryBypassPresets =
            [
                new("BatteryBypassPreset_Disabled"),
                new("BatteryBypassPreset_ResumeOnSleepShutdown"),
                new("BatteryBypassPreset_AlwaysOn"),
            ];
        }

        LEDPresets =
        [
            new ("LEDPreset_OneXPlayerX1_Preset01", "onexplayer/preset01.png", 0x0D),
            new ("LEDPreset_OneXPlayerX1_Preset02", "onexplayer/preset02.png", 0x03),
            new ("LEDPreset_OneXPlayerX1_Preset03", "onexplayer/preset03.png", 0x0B),
            new ("LEDPreset_OneXPlayerX1_Preset04", "onexplayer/preset04.png", 0x05),
            new ("LEDPreset_OneXPlayerX1_Preset05", "onexplayer/preset05.png", 0x07),
            new ("LEDPreset_OneXPlayerX1_Preset06", "onexplayer/preset06.png", 0x09),
            new ("LEDPreset_OneXPlayerX1_Preset07", "onexplayer/preset07.png", 0x0C),
            new ("LEDPreset_OneXPlayerX1_Preset08", "onexplayer/preset08.png", 0x14),
            new ("LEDPreset_OneXPlayerX1_Preset09", "onexplayer/preset09.png", 0x1E3),
            new ("LEDPreset_OneXPlayerX1_Preset10", "onexplayer/preset10.png", 0x01),
            new ("LEDPreset_OneXPlayerX1_Preset11", "onexplayer/preset11.png", 0x08),
        ];

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 184
        };

        OEMChords.Add(new KeyboardChord("Turbo",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LMenu, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM1
            ));

        OEMChords.Add(new KeyboardChord("Keyboard",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.O],
            [KeyCode.O, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM2
            ));

        // prepare hotkeys
        DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
        DeviceHotkeys[typeof(MainWindowCommands)].InputsChordType = InputsChordType.Long;
        DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
        DeviceHotkeys[typeof(OnScreenKeyboardCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = true;
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2211";
            case ButtonFlags.OEM2:
                return "\u2210";
        }

        return defaultGlyph;
    }

    public override bool Open()
    {
        bool success = base.Open();
        if (!success)
            return false;

        if (EnableSerialPort)
        {
            List<USBDeviceInfo> devices = GetSerialDevices();

            USBDeviceInfo? deviceInfo = devices.FirstOrDefault(device => device.Name.Contains(SerialPortDeviceName));
            if (deviceInfo is null)
            {
                LogManager.LogInformation("Failed to retrieve serial device with name: {0}", SerialPortDeviceName);
            }
            else
            {
                // Add the serial port name to be excluded for other instances
                string SerialPortName = Regex.Match(deviceInfo.Name, "COM\\d+").Value;
                SerialUSBIMU.SerialPortNamesInUse.Add(SerialPortName);

                // Initialize and open the serial port if it has not been initialized yet
                if (_serialPort is null)
                {
                    try
                    {
                        _serialPort = new SerialPort(SerialPortName, SerialPortBaudRate, SerialPortParity, SerialPortDataBits, SerialPortStopBits);
                        _serialPort.Open();
                        LogManager.LogInformation("Enabled Serial Port Control: {0}", _serialPort.PortName);
                    }
                    catch
                    {
                        LogManager.LogWarning("Serial port {0} is busy/denied. Disabling Serial Port Control.", SerialPortName);
                        _serialPort?.Dispose();
                        _serialPort = null;
                        EnableSerialPort = false;
                    }
                }
            }
        }

        SetTurboButtonTakeover(true);

        return success;
    }

    protected override void QuerySettings()
    {
        // raise events
        if (CheckIsBatteryProtectionSupported())
        {
            SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetString("BatteryChargeLimitPercent"), false, false);
            SettingsManager_SettingValueChanged("BatteryBypassChargingMode", ManagerFactory.settingsManager.GetString("BatteryBypassChargingMode"), false, false);

            SettingsManager_SettingValueChanged("LEDSettingsEnabled", ManagerFactory.settingsManager.GetString("LEDSettingsEnabled"), false, false);
            SettingsManager_SettingValueChanged("LEDBrightness", ManagerFactory.settingsManager.GetString("LEDBrightness"), false, false);
            SettingsManager_SettingValueChanged("LEDSettingsLevel", ManagerFactory.settingsManager.GetString("LEDSettingsLevel"), false, false);
            SettingsManager_SettingValueChanged("LEDMainColor", ManagerFactory.settingsManager.GetString("LEDMainColor"), false, false);
            SettingsManager_SettingValueChanged("LEDSecondColor", ManagerFactory.settingsManager.GetString("LEDSecondColor"), false, false);
            SettingsManager_SettingValueChanged("LEDPresetIndex", ManagerFactory.settingsManager.GetString("LEDPresetIndex"), false, false);
        }

        base.QuerySettings();
    }

    public override void Close()
    {
        Device_Removed();

        if (_serialPort is not null)
        {
            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
            }
            finally
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        SetTurboButtonTakeover(false);

        base.Close();
    }

    protected virtual void SetTurboButtonTakeover(bool enabled)
    {
        byte value = enabled ? (byte)0x40 : (byte)0x00;

        EcWriteByte(0xEB, value);
        
        // wait a bit for the EC to process the change
        Thread.Sleep(50);

        if (EcReadByte(0xEB) == value)
            LogManager.LogInformation("{0} {1} OEM button", enabled ? "Unlocked" : "Locked", ButtonFlags.OEM1);
        else
            LogManager.LogWarning("Failed to {0} OEM button", enabled ? "unlock" : "lock");
    }

    protected override void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
    {
        switch (name)
        {
            case "BatteryChargeLimitPercent":
                int percent = Convert.ToInt32(value);
                SetBatteryChargeLimit(percent);
                break;
            case "BatteryBypassChargingMode":
                int modeIndex = Convert.ToInt32(value);
                SetBatteryBypassChargingMode(modeIndex);
                break;
            case "LEDSettingsEnabled":
                LEDEnabled = Convert.ToBoolean(value);
                break;
            case "LEDBrightness":
                LEDBrightness = Convert.ToInt32(value);
                break;
            case "LEDSettingsLevel":
                LEDCurrentLevel = (LEDLevel)Convert.ToInt32(value);
                break;
            case "LEDMainColor":
                if (Convert.ToString(value) is string ledMainColorName && !string.IsNullOrEmpty(ledMainColorName))
                    LEDControllerColor = ManagerFactory.settingsManager.GetColor(ledMainColorName);
                break;
            case "LEDSecondColor":
                if (Convert.ToString(value) is string ledSecondColorName && !string.IsNullOrEmpty(ledSecondColorName))
                    LEDBackColor = ManagerFactory.settingsManager.GetColor(ledSecondColorName);
                break;
            case "LEDPresetIndex":
                int selectedIndex = Convert.ToInt32(value);
                LEDPreset = selectedIndex < LEDPresets.Count ? LEDPresets[selectedIndex] : null;
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary, initializing);
    }

    public override bool SetLedStatus(bool enable)
    {
        if (LEDEnabled != enable)
        {
            // Turn On/Off X1 Back LED
            byte[] prefix = { 0xFD, 0x3F };
            byte[] positionL = { 0x03 };
            byte[] positionR = { 0x04 };
            byte[] LEDOptionOn = { 0xFD, 0x00, 0x00, enable ? (byte)0x01 : (byte)0x00 };
            byte[] fill = Enumerable.Repeat(new[] { new byte(), new byte(), new byte() }, 18)
                .SelectMany(colorBytes => colorBytes)
                .ToArray();

            byte[] leftCommand = prefix.Concat(positionL).Concat(LEDOptionOn).Concat(fill)
                .Concat(new byte[] { 0x00, 0x3F, 0xFD }).ToArray();
            byte[] rightCommand = prefix.Concat(positionR).Concat(LEDOptionOn).Concat(fill)
                .Concat(new byte[] { 0x00, 0x3F, 0xFD }).ToArray();

            WriteToSerialPort(leftCommand);
            WriteToSerialPort(rightCommand);

            LEDEnabled = enable;
        }

        return true;
    }

    public override bool SetLedBrightness(int brightness)
    {
        if (LEDBrightness != brightness)
        {
            // X1 brightness range is: 1, 3, 4, convert from 0 - 100 % range
            brightness = brightness == 0 ? 0 : brightness < 33 ? 1 : brightness > 66 ? 4 : 3;

            // Define the HID message for setting brightness.
            byte[] msg =
            {
                0xFD, 0x3F, 0x00, 0xFD, 0x03,
                0x00, 0x01, 0x05, (byte)brightness, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, 0xFD
            };

            // Write the SerialPort message to set the LED brightness.
            WriteToSerialPort(msg);

            // Turn On/Off Back LED
            SetLedStatus(brightness > 0);

            LEDBrightness = brightness;
        }

        return true;
    }

    public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
    {
        if (!DynamicLightingCapabilities.HasFlag(level))
            return false;

        // Data message consists of a prefix, LED option, RGB data, and closing byte (0x00)
        byte[] prefix = { 0xFD, 0x3F };
        byte[] positionController = { 0x00 };
        byte[] positionBackL = { 0x03 };
        byte[] positionBackR = { 0x04 };
        byte[] LEDOptionContoller = { 0xFE, 0x00, 0x00 };
        byte[] LEDOptionBack = { 0xFE, 0x00, 0x00 };
        byte[] rgbDataController = { 0x00 };
        byte[] rgbDataBack = { 0x00 };

        // X1 RGB seems better than OneXFly
        Color ledColorController = mainColor;
        Color ledColorBack = secondaryColor;

        // Process Back LED here
        rgbDataBack = Enumerable.Repeat(new[] { ledColorBack.R, ledColorBack.G, ledColorBack.B }, 18)
            .SelectMany(colorBytes => colorBytes)
            .ToArray();

        // Perform functions and command build-up based on LED level
        switch (level)
        {
            case LEDLevel.SolidColor:
                // RGB data repeats 18 times, fill accordingly
                rgbDataController = Enumerable
                    .Repeat(new[] { ledColorController.R, ledColorController.G, ledColorController.B }, 18)
                    .SelectMany(colorBytes => colorBytes)
                    .ToArray();

                break;
        }

        // Combine prefix, LED Option, RGB data, and closing byte (0x00)
        byte[] msgController = prefix.Concat(positionController).Concat(LEDOptionContoller).Concat(rgbDataController).Concat(new byte[] { ledColorController.R, ledColorController.G, 0x3F, 0xFD }).ToArray();
        byte[] msgL = prefix.Concat(positionBackL).Concat(LEDOptionBack).Concat(rgbDataBack).Concat(new byte[] { ledColorBack.R, ledColorBack.G, 0x3F, 0xFD }).ToArray();
        byte[] msgR = prefix.Concat(positionBackR).Concat(LEDOptionBack).Concat(rgbDataBack).Concat(new byte[] { ledColorBack.R, ledColorBack.G, 0x3F, 0xFD }).ToArray();

        if (LEDControllerColor != mainColor || LEDCurrentLevel != level)
        {
            WriteToSerialPort(msgController);

            LEDControllerColor = mainColor;
            LEDCurrentLevel = level;
        }

        if (LEDBackColor != secondaryColor || LEDCurrentLevel != level)
        {
            WriteToSerialPort(msgL);
            WriteToSerialPort(msgR);

            LEDBackColor = secondaryColor;
            LEDCurrentLevel = level;
        }

        return true;
    }

    public override bool SetLEDPreset(LEDPreset? preset)
    {
        if (preset is not null)
        {
            byte[] prefix = { 0xFD, 0x3F };
            byte[] positionController = { 0x00 };
            byte[] LEDOptionContoller = { (byte)preset.Value, 0x00, 0x00 };
            byte[] rgbDataController;
            byte[] msgController;

            if (preset.Value == 0x1E3)
            {
                // OXP Class Special Format
                LEDOptionContoller = new byte[] { 0xFE, 0x00, 0x00 };
                rgbDataController = Enumerable.Repeat(new[] { (byte)0xB7, (byte)0x30, (byte)0x00 }, 18).SelectMany(colorBytes => colorBytes).ToArray();
                msgController = prefix.Concat(positionController).Concat(LEDOptionContoller).Concat(rgbDataController).Concat(new byte[] { 0xB7, 0x30, 0x3F, 0xFD }).ToArray();
            }
            else
            {
                // Other Preset Fill 0x00
                rgbDataController = Enumerable.Repeat((byte)0x00, 54).ToArray();
                msgController = prefix.Concat(positionController).Concat(LEDOptionContoller).Concat(rgbDataController).Concat(new byte[] { 0x00, 0x00, 0x3F, 0xFD }).ToArray();
            }

            if (preset != LEDPreset)
            {
                WriteToSerialPort(msgController);
            }

            LEDPreset = preset;

        }

        return true;
    }

    public void WriteToSerialPort(byte[] data)
    {
        if (_serialPort is not null && _serialPort.IsOpen)
        {
            _queue.Enqueue(() =>
            {
                //LogManager.LogInformation("Write To SerialPort: {0}", data);
                _serialPort.Write(data, 0, data.Length);
                Task.Delay(TaskDelay).Wait();
            });
        }
    }

    protected override void Device_Removed()
    {
        IsReading = false;

        // release pressed vendor buttons before clearing their state
        for (byte buttonId = 0; buttonId < _buttonStates.Length; buttonId++)
            if (_buttonStates[buttonId])
                HandleEvent(buttonId, false);

        Array.Clear(_buttonStates);

        if (hidDevices.Remove(VendorHidId, out HidDevice? device))
        {
            try { device.Dispose(); } catch { }
        }
    }

    protected override async void Device_Inserted(bool reScan = false)
    {
        if (reScan)
            await WaitUntilReady();

        if (!hidDevices.TryGetValue(VendorHidId, out HidDevice? device))
            return;

        device.OpenDevice();
        if (!device.IsOpen)
            return;

        IsReading = true;
        // HHD's controller opens the hid_v1/hid_v2_x2 OxpHidraw instance here,
        // then lets the device-specific protocol choose its initialization pages.
        InitializeVendorHidCommands();
        _ = ReadLoopAsync(device);
    }

    protected virtual void InitializeVendorHidCommands()
    {
        // Equivalent to hid_v1.INITIALIZE for standard X1 devices.
        WriteVendorHidCommand(0xB4, BuildRemapPage1(0x01));
        Thread.Sleep(50);
        WriteVendorHidCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
    }

    public override bool IsReady()
    {
        // Early return if device is already bound and connected
        if (hidDevices.TryGetValue(VendorHidId, out HidDevice? boundDevice) && boundDevice.IsConnected)
            return true;

        // A single VID/PID exposes many HID collections (keyboard, consumer,
        // mouse, vendor). Use hidFilters to pick the vendor-defined collection
        // that carries the button/remap traffic: matching on usage page + usage
        // avoids grabbing e.g. the writable-but-tiny keyboard collection.
        foreach (HidDevice device in GetHidDevices(vendorId, productIds, 0))
        {
            if (!device.IsConnected)
                continue;

            if (!hidFilters.TryGetValue(device.Attributes.ProductId, out HidFilter hidFilter))
                continue;

            if (device.Capabilities.UsagePage != hidFilter.UsagePage ||
                device.Capabilities.Usage != hidFilter.Usage)
                continue;

            hidDevices[VendorHidId] = device;
            return true;
        }

        return false;
    }

    private bool IsReading;

    private async Task ReadLoopAsync(HidDevice device)
    {
        try
        {
            while (IsReading)
            {
                HidReport report = await device.ReadReportAsync().ConfigureAwait(false);
                if (report?.Data is null || report.Data.Length < 14)
                    continue;

                byte[] data = report.Data;
                if (data[1] != FrameMarker || data[^2] != FrameMarker)
                    continue;

                if (data[0] == StatusCommandId)
                {
                    HandleStatusReport(data);
                    continue;
                }

                if (data[0] != ButtonCommandId)
                    continue;

                byte buttonId = data[6];
                if (buttonId >= _buttonStates.Length)
                    continue;

                bool pressed = data[12] == 0x01;
                if (_buttonStates[buttonId] == pressed)
                    continue;

                _buttonStates[buttonId] = pressed;
                HandleEvent(buttonId, pressed);
            }
        }
        catch { }
    }

    protected virtual void HandleStatusReport(byte[] report)
    { }

    protected virtual void HandleEvent(byte buttonId, bool pressed)
    {
        ButtonFlags button = MapVendorButton(buttonId);
        if (button == ButtonFlags.None)
            return;

        if (pressed)
            KeyPress(button);
        else
            KeyRelease(button);
    }

    protected bool WriteVendorHidCommand(byte commandId, byte[] payload)
    {
        if (!hidDevices.TryGetValue(VendorHidId, out HidDevice? device))
            return false;

        // CreateReport() exposes the protocol data area without the transport
        // report ID. WriteReport() adds that report ID and preserves the HID
        // report descriptor's device-specific output length.
        HidReport report = device.CreateReport();
        report.ReportId = 0x00;

        int frameLength = report.Data.Length;
        if (frameLength < 6 || payload.Length > frameLength - 5)
            return false;

        // HHD gen_cmd() produces this protocol frame without a report ID.
        report.Data[0] = commandId;
        report.Data[1] = FrameMarker;
        report.Data[2] = 0x01;
        Array.Copy(payload, 0, report.Data, 3, payload.Length);
        report.Data[^2] = FrameMarker;
        report.Data[^1] = commandId;

        return device.WriteReport(report);
    }

    protected HidDevice? GetVendorHidDeviceForLighting() =>
        hidDevices.GetValueOrDefault(VendorHidId);

    protected static byte[] BuildRemapPage1(byte preset) =>
    [
        0x02, 0x38, 0x20, 0x01, preset,
        0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
        0x02, 0x01, 0x02, 0x00, 0x00, 0x00,
        0x03, 0x01, 0x03, 0x00, 0x00, 0x00,
        0x04, 0x01, 0x04, 0x00, 0x00, 0x00,
        0x05, 0x01, 0x05, 0x00, 0x00, 0x00,
        0x06, 0x01, 0x06, 0x00, 0x00, 0x00,
        0x07, 0x01, 0x07, 0x00, 0x00, 0x00,
        0x08, 0x01, 0x08, 0x00, 0x00, 0x00,
        0x09, 0x01, 0x09, 0x00, 0x00, 0x00,
    ];

    protected static byte[] BuildRemapPage2(byte preset, byte m1KeyCode, byte m2KeyCode) =>
    [
        0x02, 0x38, 0x20, 0x02, preset,
        0x0A, 0x01, 0x0A, 0x00, 0x00, 0x00,
        0x0B, 0x01, 0x0B, 0x00, 0x00, 0x00,
        0x0C, 0x01, 0x0C, 0x00, 0x00, 0x00,
        0x0D, 0x01, 0x0D, 0x00, 0x00, 0x00,
        0x0E, 0x01, 0x0E, 0x00, 0x00, 0x00,
        0x0F, 0x01, 0x0F, 0x00, 0x00, 0x00,
        0x10, 0x01, 0x10, 0x00, 0x00, 0x00,
        0x22, 0x02, 0x01, m1KeyCode, 0x00, 0x00,
        0x23, 0x02, 0x01, m2KeyCode, 0x00, 0x00,
    ];

    protected virtual ButtonFlags MapVendorButton(byte buttonId)
    {
        return buttonId switch
        {
            0x21 => ButtonFlags.Special,
            0x22 => ButtonFlags.R4,
            0x23 => ButtonFlags.L4,
            0x24 => ButtonFlags.OEM2,
            _ => ButtonFlags.None,
        };
    }

    private bool CheckIsBatteryProtectionSupported()
    {
        try
        {
            // Create a ManagementObjectSearcher to query the Win32_BIOS class
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_BIOS");

            // Get the collection of ManagementObject instances
            ManagementObjectCollection collection = searcher.Get();

            // Iterate through the collection and access properties
            foreach (ManagementObject obj in collection)
            {
                int majorVersion = Convert.ToInt32(obj["EmbeddedControllerMajorVersion"]);
                int minorVersion = Convert.ToInt32(obj["EmbeddedControllerMinorVersion"]);
                return IsBatteryProtectionSupported(majorVersion, minorVersion);
            }
        }
        catch (Exception ex)
        {
            LogManager.LogError("Cannot found ECVersion: " + ex.Message);
        }

        return false;
    }

    public virtual bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return false;
    }

    public void SetBatteryChargeLimit(int chargeLimit)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        if (chargeLimit < 0 || chargeLimit > 100)
            return;

        ECRamDirectWriteByte(ECBatteryLimitAddress, ECDetails, (byte)chargeLimit);
    }

    public void SetBatteryBypassChargingMode(int modeIndex)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        if (modeIndex < 0 || modeIndex > 4)
            return;

        int modeValue = 0;

        switch (modeIndex)
        {
            case 0:
                modeValue = 0x00; // Disabled
                break;
            case 1:
                modeValue = 0x01; // Disabled on Sleep and Reboot
                break;
            case 2:
                modeValue = 0x03; // Always On
                break;
        }

        ECRamDirectWriteByte(ECBypassChargingAddress, ECDetails, (byte)modeValue);
    }
}

public class OneXPlayerX1AMD : OneXPlayerX1
{
    public OneXPlayerX1AMD()
    {
        // https://www.amd.com/fr/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 15, 30 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;
    }

    public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return majorVersion >= 1 && minorVersion >= 3;
    }
}

public class OneXPlayerX1Intel : OneXPlayerX1
{
    public OneXPlayerX1Intel()
    {
        // https://www.intel.com/content/www/us/en/products/sku/236847/intel-core-ultra-7-processor-155h-24m-cache-up-to-4-80-ghz/specifications.html
        // follow the values presented in OneXConsole
        nTDP = new double[] { 15, 15, 35 };
        cTDP = new double[] { 6, 35 };
        GfxClock = new double[] { 100, 2250 };
        CpuClock = 4800;

        // Power Saving
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBattery, Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = BetterBatteryGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
        });

        // Performance
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBetterPerformance, Properties.Resources.PowerProfileOneXPlayerX1IntelBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            CPUBoostLevel = CPUBoostLevel.Enabled,
            Guid = BetterPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 30.0d, 30.0d, 30.0d }
        });

        // Max Performance
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBestPerformance, Properties.Resources.PowerProfileOneXPlayerX1IntelBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            CPUBoostLevel = CPUBoostLevel.Enabled,
            Guid = BestPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 35.0d, 35.0d, 64.0d },
            EPPOverrideEnabled = true,
            EPPOverrideValue = 32,
        });
    }

    public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return majorVersion >= 0 && minorVersion >= 67;
    }
}
