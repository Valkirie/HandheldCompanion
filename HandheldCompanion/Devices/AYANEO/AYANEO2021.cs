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
        ProductIllustration = "device_aya_2021";
        ProductModel = "AYANEO2021";

        // https://www.amd.com/en/support/apu/amd-ryzen-processors/amd-ryzen-5-mobile-processors-radeon-graphics/amd-ryzen-5-4500u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 3, 25 };
        GfxClock = new double[] { 100, 1500 };
        CpuClock = 4000;

        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        OEMChords.Add(new DeviceChord("WIN key",
            new List<KeyCode> { KeyCode.LWin },
            new List<KeyCode> { KeyCode.LWin },
            false, ButtonFlags.OEM1
        ));

        // Conflicts with OS
        //listeners.Add("TM key", new ChordClick(KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete));

        OEMChords.Add(new DeviceChord("ESC key",
            new List<KeyCode> { KeyCode.Escape },
            new List<KeyCode> { KeyCode.Escape },
            false, ButtonFlags.OEM2
        ));

        // Conflicts with Ayaspace when installed
        OEMChords.Add(new DeviceChord("KB key",
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

        return defaultGlyph;
    }
}