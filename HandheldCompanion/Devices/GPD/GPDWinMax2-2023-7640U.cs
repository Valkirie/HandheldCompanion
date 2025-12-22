namespace HandheldCompanion.Devices;

public class GPDWinMax2_2023_7640U : GPDWinMax2
{
    public GPDWinMax2_2023_7640U()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/7000-series/amd-ryzen-5-7640u.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 3, 28 };
        GfxClock = new double[] { 100, 2600 };
        CpuClock = 4900;
    }
}