using HandheldCompanion.Inputs;
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
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16],
            [KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM4
        ));

        this.OEMChords.Add(new KeyboardChord("Custom Key Top Left",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15],
            [KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM3
        ));

        this.OEMChords.Add(new KeyboardChord("Custom Key Big",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F17],
            [KeyCode.F17, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM1
        ));

        this.OEMChords.Add(new KeyboardChord("Custom Key Small",
            [KeyCode.LWin, KeyCode.D],
            [KeyCode.LWin, KeyCode.D],
            false, ButtonFlags.OEM2
        ));
    }
}