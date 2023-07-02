using System.Numerics;

namespace ControllerCommon.Devices;

public class LokiZero : AynLoki
{
    public LokiZero()
    {
        // https://www.amd.com/en/products/apu/amd-athlon-silver-3050u
        nTDP = new double[] { 10, 10, 10 };
        cTDP = new double[] { 5, 15 };
        GfxClock = new double[] { 100, 1100 };
    }
}