using HandheldCompanion.Inputs;
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

        // IMU matrices loaded from AYANEOAIR.json

        this.OEMChords.Clear();
        this.OEMChords.Add(new KeyboardChord("Custom Key Big",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12],
            [KeyCode.F12, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM1
        ));
        this.OEMChords.Add(new KeyboardChord("Custom Key Small",
            [KeyCode.LWin, KeyCode.D],
            [KeyCode.LWin, KeyCode.D],
            false, ButtonFlags.OEM2
        ));
        this.OEMChords.Add(new KeyboardChord("Custom Key Top Left",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F11],
            [KeyCode.F11, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM3
        ));
        this.OEMChords.Add(new KeyboardChord("Custom Key Top Right",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F10],
            [KeyCode.F10, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM4
        ));
    }
}