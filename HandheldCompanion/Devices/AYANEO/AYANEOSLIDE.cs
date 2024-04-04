using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class AYANEOSlide : AYANEO.AYANEODeviceCEii
{
    public AYANEOSlide()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_slide";
        this.ProductModel = "AYANEOSlide";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 54 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        this.GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
        this.GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        this.AccelerometerAxis = new Vector3(-1.0f, 1.0f, -1.0f);
        this.AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        this.OEMChords.Clear();

        this.OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM3
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM4
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F17 },
            new List<KeyCode> { KeyCode.F17, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM1
        ));

        this.OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.D, KeyCode.LWin },
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
                return "\u2209";
            case ButtonFlags.OEM4:
                return "\u220A";
        }

        return defaultGlyph;
    }
}