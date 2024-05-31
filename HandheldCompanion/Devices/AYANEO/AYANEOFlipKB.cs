using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Windows.Media;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class AYANEOFlipKB : AYANEO.AYANEODeviceCEc
{
    public AYANEOFlipKB()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_flip_kb";
        this.ProductModel = "AYANEO Flip KB";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        // https://www.amd.com/en/products/apu/amd-ryzen-7-8840u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 28 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;


        // TODO: Add OEMChords for "three dots" key here
    }
}