using System.Numerics;

namespace HandheldCompanion.Devices;

public class LokiMax6800U : AynLoki
{
    public LokiMax6800U()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 28 };
        GfxClock = new double[] { 100, 2200 };
    }
}