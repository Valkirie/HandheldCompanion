using System.Numerics;

namespace HandheldCompanion.Devices;

public class OneXPlayerMiniAMD : OneXPlayerMini
{
    public OneXPlayerMiniAMD()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-5800u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 10, 25 };
        GfxClock = new double[] { 100, 2000 };
        CpuClock = 4400;

        AccelerometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
    }
}