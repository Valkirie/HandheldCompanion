using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;
namespace HandheldCompanion.Devices;

public class AYANEOKUN : AYANEO.AYANEODevice
{
    private enum LEDGroup
    {
        JoystickLeft = 1,
        JoystickRight = 2,
        JoystickBoth = 3,
        AYAButton = 4,
    }

    private static byte[] AYA_ZONES = new byte[] { 4 };
    private static byte[] STICK_ZONES = new byte[] { 1, 2, 3, 4 };

    private Color color = Color.FromRgb(255, 255, 255);

    public AYANEOKUN()
    {
        // device specific settings
        ProductIllustration = "device_aya_kun";
        ProductModel = "AYANEO KUN";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 3, 54 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        GyrometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        DynamicLightingCapabilities |= LEDLevel.SolidColor;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 100
        };

        OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F17 },
            new List<KeyCode> { KeyCode.F17, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey },

            false, ButtonFlags.OEM4
        ));

        OEMChords.Add(new DeviceChord("T",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F18 },
            new List<KeyCode> { KeyCode.F18, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM5
        ));

        OEMChords.Add(new DeviceChord("Guide",
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            false, ButtonFlags.OEM6
        ));

    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\uE003";
            case ButtonFlags.OEM2:
                return "\u220B";
            case ButtonFlags.OEM3:
                return "\u2209";
            case ButtonFlags.OEM4:
                return "\u220A";
            case ButtonFlags.OEM5:
                return "\u0054";
            case ButtonFlags.OEM6:
                return "\uE001";
        }

        return defaultGlyph;
    }

    public override bool SetLedStatus(bool status)
    {
        if (status)
        {
            SetLEDGroupEnable(LEDGroup.AYAButton);
            SetLEDGroupColor(LEDGroup.AYAButton, AYA_ZONES, color);

            SetLEDGroupEnable(LEDGroup.JoystickBoth);
            SetLEDGroupColor(LEDGroup.JoystickBoth, STICK_ZONES, color);
        }
        else
        {
            SetLEDGroupDisable(LEDGroup.AYAButton);
            SetLEDGroupDisable(LEDGroup.JoystickBoth);
        }
        return true;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed)
    {
        if (!DynamicLightingCapabilities.HasFlag(level))
            return false;

        color = MainColor;

        switch (level)
        {
            case LEDLevel.SolidColor:
                SetLEDGroupColor(LEDGroup.AYAButton, AYA_ZONES, color);
                SetLEDGroupColor(LEDGroup.JoystickBoth, STICK_ZONES, color);
                break;
        }

        return true;
    }

    private void SetLEDGroupEnable(LEDGroup group)
    {
        SendLEDCommand((byte)group, 2, 0x80); // This seems to determine the time between color transitions, 0x80 being a very good balance
    }

    private void SetLEDGroupDisable(LEDGroup group)
    {
        SendLEDCommand((byte)group, 2, 0xc0);
    }

    private void SetLEDGroupColor(LEDGroup group, byte[] zones, Color color)
    {
        foreach (byte zone in zones)
        {
            byte[] colorValues = GetColorValues(group, zone, color);
            for (byte colorComponentIndex = 0; colorComponentIndex < colorValues.Length; colorComponentIndex++)
            {
                byte zoneColorComponent = (byte)(zone * 3 + colorComponentIndex); // Indicates which Zone and which color component
                byte colorComponentValueBrightness = (byte)(colorValues[colorComponentIndex] * 192 / byte.MaxValue); // Convert 0-255 to 0-100
                SendLEDCommand((byte)group, zoneColorComponent, colorComponentValueBrightness);
            }
        }
    }

    private void SendLEDCommand(byte group, byte command, byte argument)
    {
        using (new ScopedLock(updateLock))
        {
            ECRAMWrite(0x6d, group);
            ECRAMWrite(0xb1, command);
            ECRAMWrite(0xb2, argument);
            ECRAMWrite(0xbf, 0x10);
            Thread.Sleep(5); // Sleep here to give the controller enough time. AYASpace does this as well.
            ECRAMWrite(0xbf, 0xfe);
        }
    }

    // Get remapped RGB color values for the specific zone
    // 1: R -> G, G -> R, B -> B
    // 2: R -> G, G -> B, B -> R
    // 3: R -> B, G -> R, B -> G
    // 4: R -> B, G -> G, B -> R
    // 4 (AYA): R -> B, G -> R, B -> G
    private byte[] GetColorValues(LEDGroup group, byte zone, Color color)
    {
        if (zone == 1) return new byte[] { color.G, color.R, color.B };
        if (zone == 2) return new byte[] { color.G, color.B, color.R };
        if (zone == 3 || group == LEDGroup.AYAButton) return new byte[] { color.B, color.R, color.G };
        if (zone == 4) return new byte[] { color.B, color.G, color.R };

        // Just return the default
        return new byte[] { color.R, color.G, color.B };
    }
}