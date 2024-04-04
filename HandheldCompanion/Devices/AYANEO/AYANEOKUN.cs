using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class AYANEOKUN : AYANEO.AYANEODeviceCEc
{
    public AYANEOKUN()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_kun";
        this.ProductModel = "AYANEO KUN";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 54 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        this.GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        this.GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        this.AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        this.AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        this.Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDColor;

        this.OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F17 },
            new List<KeyCode> { KeyCode.F17, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM2
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey },

            false, ButtonFlags.OEM4
        ));

        this.OEMChords.Add(new DeviceChord("T",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F18 },
            new List<KeyCode> { KeyCode.F18, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM5
        ));

        this.OEMChords.Add(new DeviceChord("Guide",
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

    protected override byte[] MapColorValues(int zone, Color color)
    {
        switch(zone)
        {
            case 1:
                return [color.G, color.R, color.B];
            case 2:
                return [color.G, color.B, color.R];
            case 3:
                return [color.B, color.R, color.G];
            case 4:
                return [color.B, color.G, color.R];
            default:
                return [color.R, color.G, color.B];
        }
    }
}