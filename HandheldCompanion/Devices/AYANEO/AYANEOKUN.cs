using HandheldCompanion.Inputs;
using System.Collections.Generic;
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

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 54 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        // device specific capacities
        this.Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDColor;

        this.OEMChords.Add(new DeviceChord("T",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F18 },
            new List<KeyCode> { KeyCode.F18, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM5
        ));
        this.OEMChords.Add(new DeviceChord("Guide",
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            false, ButtonFlags.OEM6
        ));

    }

    protected override byte[] MapColorValues(int zone, Color color)
    {
        switch(zone)
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