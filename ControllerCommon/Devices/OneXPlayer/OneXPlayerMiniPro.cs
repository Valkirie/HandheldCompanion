using System.Numerics;

namespace ControllerCommon.Devices;

public class OneXPlayerMiniPro : OneXPlayerMini
{
    public OneXPlayerMiniPro()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2200 };

        AccelerationAxis = new Vector3(-1.0f, 1.0f, 1.0f);
    }
}