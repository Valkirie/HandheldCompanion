using HandheldCompanion.Inputs;
using System.Collections.Generic;

using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEONEXT : IDevice
{
    public AYANEONEXT()
    {
        // device specific settings
        ProductIllustration = "device_aya_next";
        ProductModel = "AYANEONext";

        // https://www.amd.com/fr/products/apu/amd-ryzen-7-5800u
        // https://www.amd.com/fr/products/apu/amd-ryzen-7-5825u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 10, 25 };
        GfxClock = new double[] { 100, 2000 };
        CpuClock = 4500;

        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        OEMChords.Add(new DeviceChord("Custom key BIG",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12 },
            new List<KeyCode> { KeyCode.F12, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Custom key Small",
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
        }

        return defaultGlyph;
    }
}