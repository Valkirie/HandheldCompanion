using System.Numerics;

namespace HandheldCompanion.Devices;

public class GPDWinMax2_2024_HX370 : GPDWinMax2
{
    public GPDWinMax2_2024_HX370()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 3, 28 };
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;

        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        AccelerometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);
    }
}