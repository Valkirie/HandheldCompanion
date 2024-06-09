using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;
using HandheldCompanion.Misc.Threading.Tasks;
using HandheldCompanion.Sensors;

namespace HandheldCompanion.Devices;

public class OneXPlayerX1 : IDevice
{
    private SerialPort? _serialPort; // COM3 SerialPort for Device control of OneXPlayer

    // Enable COM Port for LED Control
    public bool EnableSerialPort = true;
    public string SerialPortName = "COM3";
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
        Capabilities = DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
        Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDColor;
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 184
        };

        OEMChords.Add(new DeviceChord("Turbo",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.LMenu },
            new List<KeyCode> { KeyCode.LMenu, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
            ));
        
        LEDEnabled = SettingsManager.GetBoolean("LEDSettingsEnabled");
        LEDBrightness = SettingsManager.GetInt("LEDBrightness");
        LEDCurrentLevel = (LEDLevel)SettingsManager.GetInt("LEDSettingsLevel");
        LEDControllerColor = SettingsManager.GetColor("LEDMainColor");
        LEDBackColor = SettingsManager.GetColor("LEDSecondColor");
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
        var success = base.Open();
        if (!success)
            return false;


        if (EnableSerialPort)
        {
            // Add the serial port name to be excluded for other instances
            SerialUSBIMU.SerialPortNamesInUse.Add(SerialPortName);

            // Initialize and open the serial port if it has not been initialized yet
            if (_serialPort is null)
            {
                _serialPort = new SerialPort(SerialPortName, SerialPortBaudRate, SerialPortParity, SerialPortDataBits,
                    SerialPortStopBits);
                _serialPort.Open();

                LogManager.LogInformation("Enabled Serial Port Control: {0}", _serialPort.PortName);
            }
        }

        // allow OneX button to pass key inputs
        LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);

        ECRamDirectWrite(0x4EB, ECDetails, 0x40);

        return ECRamReadByte(0x4EB, ECDetails) == 0x40;
    }

    public override void Close()
    {
        if (_serialPort is not null && _serialPort.IsOpen)
        {
            _serialPort.Close();
        }
        
        LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);
        ECRamDirectWrite(0x4EB, ECDetails, 0x00);
        base.Close();
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
            
            case LEDLevel.Rainbow:
                // OneXConsole "Flowing Light" effect as a rainbow effect
                LEDOptionContoller = new byte[] { 0x03, 0x00, 0x00 };

                // RGB data empty, repeats 54 times, fill accordingly
                rgbDataController = Enumerable.Repeat((byte)0x00, 54).ToArray();

                ledColorController.R = 0x00;
                ledColorController.G = 0x00;
                
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
}
