using HandheldCompanion.Inputs;
using System.Collections.Generic;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class AYANEO2 : AYANEO.AYANEODeviceCEc
{
    public AYANEO2()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_2";
        this.ProductModel = "AYANEO2";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 33 };
        this.GfxClock = new double[] { 100, 2200 };
        this.CpuClock = 4700;
    }
}