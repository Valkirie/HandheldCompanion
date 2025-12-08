using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Inputs;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class AOKZOEA1 : OneXAOKZOE
{
    protected HidDevice? hidDevice;
    protected const int PID_LED = 0xB001;

    public AOKZOEA1()
    {
        // device specific settings
        ProductIllustration = "device_aokzoe_a1";
        ProductModel = "AOKZOEA1";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u 
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2200 };
        CpuClock = 4700;

        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
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
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;

        // LED HID Device
        vendorId = 0x1A2C;
        productIds = [PID_LED];
        hidFilters = new()
        {
            { PID_LED, new HidFilter(unchecked((short)0xFF01), unchecked(0x0001)) },
        };

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E, // 78
            AddressDataPort = 0x4F,     // 79
            FanValueMin = 0,
            FanValueMax = 255
        };

        // Home
        OEMChords.Add(new KeyboardChord("Home",
            [KeyCode.LWin, KeyCode.D],
            [KeyCode.LWin, KeyCode.D],
            false, ButtonFlags.OEM1
        ));

        // Home (long press 1.5s)
        OEMChords.Add(new KeyboardChord("Home, Long-press",
            [KeyCode.LWin, KeyCode.G],
            [KeyCode.LWin, KeyCode.G],
            false, ButtonFlags.OEM6
        ));

        // Keyboard
        OEMChords.Add(new KeyboardChord("Keyboard",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.O],
            [KeyCode.O, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM2
        ));

        // Turbo
        OEMChords.Add(new KeyboardChord("Turbo",
            [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu],
            false, ButtonFlags.OEM3
        ));

        // Home + Keyboard
        OEMChords.Add(new KeyboardChord("Home + Keyboard",
            [KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete],
            [KeyCode.Delete, KeyCode.RControlKey, KeyCode.RAlt],
            false, ButtonFlags.OEM4
        ));

        // Home + Turbo
        OEMChords.Add(new KeyboardChord("Home + Turbo",
            [KeyCode.LWin, KeyCode.Snapshot],
            [KeyCode.Snapshot, KeyCode.LWin],
            false, ButtonFlags.OEM5
        ));

        // prepare hotkeys
        DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
        DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM3] = true;
    }

    public override void SetFanControl(bool enable, int mode)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        // Determine the fan control mode based enable
        byte controlValue = enable ? (byte)FanControlMode.Manual : (byte)FanControlMode.Automatic;

        // Update the fan control mode
        EcWriteByte(ACPI_FanMode_Address, controlValue);
    }

    public override void SetFanDuty(double percent)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        // Convert 0-100 percentage to range
        byte fanSpeedSetpoint = (byte)(percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin);

        // Ensure the value is within the valid range
        fanSpeedSetpoint = Math.Min((byte)ECDetails.FanValueMax, Math.Max((byte)ECDetails.FanValueMin, fanSpeedSetpoint));

        // Set the requested fan speed
        EcWriteByte(ACPI_FanPWMDutyCycle_Address, fanSpeedSetpoint);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u220C";
            case ButtonFlags.OEM2:
                return "\u2210";
            case ButtonFlags.OEM3:
                return "\u2211";
        }

        return base.GetGlyph(button);
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

            hidDevice = device;
            return true;
        }

        return false;
    }

    public override bool SetLedBrightness(int brightness)
    {
        // AOKZOE A1 uses the HID v2 vendor protocol (0x1A2C:0xB001, FF01:0001)
        return SendV2Brightness(hidDevice, brightness);
    }

    public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
    {
        if (!DynamicLightingCapabilities.HasFlag(level))
            return false;

        switch (level)
        {
            case LEDLevel.SolidColor:
                {
                    // Snap to the nearest colour supported by the A1 LEDs
                    Color ledColor = FindClosestColor(mainColor);
                    return SendV2SolidColor(hidDevice, ledColor);
                }

            case LEDLevel.Rainbow:
                {
                    // Map "Rainbow" to the device's flowing mode
                    return SendV2Rainbow(hidDevice);
                }

            default:
                return false;
        }
    }

    static Color FindClosestColor(Color inputColor)
    {
        // Predefined colors that work on the device
        Color[] predefinedColors = new Color[]
        {
            Color.FromRgb(255, 0, 0),         // Red (255,0,0) 
            Color.FromRgb(255, 82, 0),        // Orange (255, 165, 0)
            Color.FromRgb(255, 255, 0),       // Yellow (255, 255, 0)
            Color.FromRgb(130, 255, 0),       // Lime Green (50, 205, 50)
            Color.FromRgb(0, 255, 0),         // Green (0, 128, 0)
            Color.FromRgb(0, 255, 110),       // Turquoise (Cyan) (0, 255, 255)
            Color.FromRgb(0, 255, 255),       // Teal (0, 128, 128)
            Color.FromRgb(130, 255, 255),     // Blue (? ? ?)
            Color.FromRgb(0, 0, 255),         // Dark Blue (? ? ?)
            Color.FromRgb(122, 0, 255),       // Purple (Violet) (128, 0, 128)
            Color.FromRgb(255, 0, 255),       // Pink (255, 182, 193)
            Color.FromRgb(255, 0, 129),       // Magenta (255, 0, 255)
        };

        // Initialize with the first color
        Color closestColor = predefinedColors[0];
        double minDistance = CalculateDistance(inputColor, closestColor);

        // Iterate through predefined colors to find the closest one
        foreach (var predefinedColor in predefinedColors)
        {
            double distance = CalculateDistance(inputColor, predefinedColor);

            // Update closest color if a closer one is found
            if (distance < minDistance)
            {
                minDistance = distance;
                closestColor = predefinedColor;
            }
        }

        // Return the closest predefined color
        return closestColor;
    }

    static double CalculateDistance(Color color1, Color color2)
    {
        // Helper method to calculate the Euclidean distance between two colors

        int deltaR = color2.R - color1.R;
        int deltaG = color2.G - color1.G;
        int deltaB = color2.B - color1.B;

        // Euclidean distance formula
        return Math.Sqrt(deltaR * deltaR + deltaG * deltaG + deltaB * deltaB);
    }
}