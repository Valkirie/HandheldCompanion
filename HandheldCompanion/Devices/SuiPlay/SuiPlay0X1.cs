using HandheldCompanion.Inputs;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class SuiPlay0X1 : AYANEO.AYANEODeviceCEc
{
    public SuiPlay0X1()
    {
        // device specific settings
        // SuiPlay 0X1 seams to be based on an AYANEO 2S with updated EC
        this.ProductIllustration = "device_suiplay_0x1";
        this.ProductModel = "SUIPLAY 0X1";

        // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 54 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        this.OEMChords.Clear();
        this.OEMChords.Add(new KeyboardChord("Custom Key Big", [KeyCode.F23], [KeyCode.F23], false, ButtonFlags.OEM1));
        this.OEMChords.Add(new KeyboardChord("Custom Key Small", [KeyCode.F24], [KeyCode.F24], false, ButtonFlags.OEM2));
        this.OEMChords.Add(new KeyboardChord("Custom Key Top Left", [KeyCode.F21], [KeyCode.F21], false, ButtonFlags.OEM3));
        this.OEMChords.Add(new KeyboardChord("Custom Key Top Right", [KeyCode.F22], [KeyCode.F22], false, ButtonFlags.OEM4));
        this.OEMChords.Add(new KeyboardChord("T", [KeyCode.F18], [KeyCode.F18], false, ButtonFlags.OEM5));
    }
}