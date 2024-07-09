using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEO2021 : IDevice
{
    public AYANEO2021()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_2021";
        this.ProductModel = "AYANEO2021";

        // https://www.amd.com/en/support/apu/amd-ryzen-processors/amd-ryzen-5-mobile-processors-radeon-graphics/amd-ryzen-5-4500u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 25 };
        this.GfxClock = new double[] { 100, 1500 };
        this.CpuClock = 4000;

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

        this.OEMChords.Add(new KeyboardChord("WIN key",
            new List<KeyCode> { KeyCode.LWin },
            new List<KeyCode> { KeyCode.LWin },
            false, ButtonFlags.OEM1
        ));

        // Conflicts with OS
        //listeners.Add("TM key", new ChordClick(KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete));

        this.OEMChords.Add(new KeyboardChord("ESC key",
            new List<KeyCode> { KeyCode.Escape },
            new List<KeyCode> { KeyCode.Escape },
            false, ButtonFlags.OEM2
        ));

        // Conflicts with Ayaspace when installed
        this.OEMChords.Add(new KeyboardChord("KB key",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.O },
            new List<KeyCode> { KeyCode.O, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\uE008";
            case ButtonFlags.OEM2:
                return "\u242F";
            case ButtonFlags.OEM3:
                return "\u243D";
        }

        return base.GetGlyph(button);
    }
}