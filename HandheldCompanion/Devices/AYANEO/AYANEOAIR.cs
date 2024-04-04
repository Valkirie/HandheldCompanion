using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class AYANEOAIR : AYANEO.AYANEODeviceCEc
{
    public AYANEOAIR()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_air";
        this.ProductModel = "AYANEOAir";

        // https://www.amd.com/en/products/apu/amd-ryzen-5-5560u
        this.nTDP = new double[] { 12, 12, 15 };
        this.cTDP = new double[] { 3, 15 };
        this.GfxClock = new double[] { 100, 1600 };
        this.CpuClock = 4000;

        this.GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        this.GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        this.AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        this.AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        this.OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F10 },
            new List<KeyCode> { KeyCode.F10, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F11 },
            new List<KeyCode> { KeyCode.F11, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM4
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12 },
            new List<KeyCode> { KeyCode.F12, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM2
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
                return "\u220A";
            case ButtonFlags.OEM4:
                return "\u2209";
        }

        return defaultGlyph;
    }
}