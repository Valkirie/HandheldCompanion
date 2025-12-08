using HandheldCompanion.Inputs;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class OneXPlayerMiniPro : OneXPlayerMini
{
    public OneXPlayerMiniPro()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2200 };
        CpuClock = 4700;

        AccelerometerAxis = new Vector3(1.0f, -1.0f, 1.0f);

        OEMChords.Clear();

        OEMChords.Add(new KeyboardChord("Orange",
            [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu],
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new KeyboardChord("Keyboard",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.O],
            [KeyCode.O, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new KeyboardChord("Function",
            [KeyCode.LWin, KeyCode.D],
            [KeyCode.LWin, KeyCode.D],
            false, ButtonFlags.OEM3
        ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2219";
            case ButtonFlags.OEM2:
                return "\u2210";
            case ButtonFlags.OEM3:
                return "\u2218";
        }

        return defaultGlyph;
    }
}