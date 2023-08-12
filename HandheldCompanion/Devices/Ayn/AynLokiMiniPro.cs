using System.Numerics;

namespace HandheldCompanion.Devices;

public class LokiMiniPro : AynLoki
{
    public LokiMiniPro()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-3-7320u
        nTDP = new double[] { 12, 12, 12 };
        cTDP = new double[] { 5, 15 };
        GfxClock = new double[] { 100, 1900 };
    }
}