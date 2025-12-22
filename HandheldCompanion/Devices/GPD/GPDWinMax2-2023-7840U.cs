namespace HandheldCompanion.Devices;

public class GPDWinMax2_2023_7840U : GPDWinMax2
{
    public GPDWinMax2_2023_7840U()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 3, 28 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5200;
    }
}