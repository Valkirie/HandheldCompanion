using HandheldCompanion.Inputs;
using System.Windows.Media;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class AYANEOKUN : AYANEO.AYANEODeviceCEc
{
    public AYANEOKUN()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_kun";
        this.ProductModel = "AYANEO KUN";

        // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 54 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        // device specific capacities
        this.Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDColor;

        // old EC
        this.OEMChords.Add(new KeyboardChord("T",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F18],
            [KeyCode.F18, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM5
        ));
        this.OEMChords.Add(new KeyboardChord("Guide",
            [KeyCode.LButton, KeyCode.XButton2],
            [KeyCode.LButton, KeyCode.XButton2],
            false, ButtonFlags.OEM6
        ));

        // new EC
        this.OEMChords.Add(new KeyboardChord("Custom Key Big", [KeyCode.F23], [KeyCode.F23], false, ButtonFlags.OEM1));
        this.OEMChords.Add(new KeyboardChord("Custom Key Small", [KeyCode.F24], [KeyCode.F24], false, ButtonFlags.OEM2));
        this.OEMChords.Add(new KeyboardChord("Custom Key Top Left", [KeyCode.F21], [KeyCode.F21], false, ButtonFlags.OEM3));
        this.OEMChords.Add(new KeyboardChord("Custom Key Top Right", [KeyCode.F22], [KeyCode.F22], false, ButtonFlags.OEM4));
        this.OEMChords.Add(new KeyboardChord("T", [KeyCode.F18], [KeyCode.F18], false, ButtonFlags.OEM5));
    }

    protected override byte[] MapColorValues(int zone, Color color)
    {
        switch (zone)
        {
            case 1:
                return [color.G, color.R, color.B];
            case 2:
                return [color.G, color.B, color.R];
            case 3:
                return [color.B, color.R, color.G];
            case 4:
                return [color.B, color.G, color.R];
            default:
                return [color.R, color.G, color.B];
        }
    }
}