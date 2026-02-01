using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc.Threading.Tasks;
using HandheldCompanion.Models;
using HandheldCompanion.Sensors;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class OneXPlayerX1 : OneXAOKZOE
{
    private SerialPort? _serialPort; // COM3 SerialPort for Device control of OneXPlayer

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
        // device specific settings
        ProductIllustration = "device_onexplayer_x1";
        ProductModel = "ONEXPLAYERX1";

        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' },
        };

        AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' },
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

        // prepare hotkeys
        DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
        DeviceHotkeys[typeof(MainWindowCommands)].InputsChordType = InputsChordType.Long;
        DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2211";
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

            USBDeviceInfo deviceInfo = devices.FirstOrDefault(a => a.Name.Contains(SerialPortDeviceName));
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
                    _serialPort = new SerialPort(SerialPortName, SerialPortBaudRate, SerialPortParity, SerialPortDataBits, SerialPortStopBits);
                    _serialPort.Open();

                    LogManager.LogInformation("Enabled Serial Port Control: {0}", _serialPort.PortName);
                }
            }
        }

        // allow OneX button to pass key inputs
        EcWriteByte(0xEB, 0x40);
        if (EcReadByte(0xEB) == 0x40)
            LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);

        return success;
    }

    protected override void QuerySettings()
    {
        // raise events
        if (CheckIsBatteryProtectionSupported())
        {
            SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetString("BatteryChargeLimitPercent"), false);
            SettingsManager_SettingValueChanged("BatteryBypassChargingMode", ManagerFactory.settingsManager.GetString("BatteryBypassChargingMode"), false);

            SettingsManager_SettingValueChanged("LEDSettingsEnabled", ManagerFactory.settingsManager.GetString("LEDSettingsEnabled"), false);
            SettingsManager_SettingValueChanged("LEDBrightness", ManagerFactory.settingsManager.GetString("LEDBrightness"), false);
            SettingsManager_SettingValueChanged("LEDSettingsLevel", ManagerFactory.settingsManager.GetString("LEDSettingsLevel"), false);
            SettingsManager_SettingValueChanged("LEDMainColor", ManagerFactory.settingsManager.GetString("LEDMainColor"), false);
            SettingsManager_SettingValueChanged("LEDSecondColor", ManagerFactory.settingsManager.GetString("LEDSecondColor"), false);
            SettingsManager_SettingValueChanged("LEDPresetIndex", ManagerFactory.settingsManager.GetString("LEDPresetIndex"), false);
        }

        base.QuerySettings();
    }

    public override void Close()
    {
        if (_serialPort is not null && _serialPort.IsOpen)
        {
            _serialPort.Close();
        }

        EcWriteByte(0xEB, 0x00);
        if (EcReadByte(0xEB) == 0x00)
            LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);

        base.Close();
    }

    protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
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
                LEDControllerColor = ManagerFactory.settingsManager.GetColor(Convert.ToString(value));
                break;
            case "LEDSecondColor":
                LEDBackColor = ManagerFactory.settingsManager.GetColor(Convert.ToString(value));
                break;
            case "LEDPresetIndex":
                int selectedIndex = Convert.ToInt32(value);
                LEDPreset = selectedIndex < LEDPresets.Count ? LEDPresets[selectedIndex] : null;
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary);
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