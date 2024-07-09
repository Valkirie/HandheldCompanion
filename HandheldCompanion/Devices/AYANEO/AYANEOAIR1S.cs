using HandheldCompanion.Inputs;
using System.Collections.Generic;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEOAIR1S : AYANEOAIR
{
    public AYANEOAIR1S()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 4, 28 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        this.OEMChords.Clear();

        this.OEMChords.Add(new KeyboardChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM4
        ));

        this.OEMChords.Add(new KeyboardChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));

        this.OEMChords.Add(new KeyboardChord("Custom Key Big",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F17 },
            new List<KeyCode> { KeyCode.F17, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
        ));

        this.OEMChords.Add(new KeyboardChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM2
        ));
    }
}