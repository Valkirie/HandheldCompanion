using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEONEXT : IDevice
{
    public AYANEONEXT()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_next";
        this.ProductModel = "AYANEONext";

        // https://www.amd.com/fr/products/apu/amd-ryzen-7-5800u
        // https://www.amd.com/fr/products/apu/amd-ryzen-7-5825u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 10, 25 };
        this.GfxClock = new double[] { 100, 2000 };
        this.CpuClock = 4500;

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

        this.OEMChords.Add(new KeyboardChord("Custom key BIG",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12],
            [KeyCode.F12, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM1
        ));

        this.OEMChords.Add(new KeyboardChord("Custom key Small",
            [KeyCode.LWin, KeyCode.D],
            [KeyCode.LWin, KeyCode.D],
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

        return base.GetGlyph(button);
    }
}