namespace HandheldCompanion.Devices;

public class GPDWinMini_HX370 : GPDWinMini_8840U
{
    public GPDWinMini_HX370()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 3, 28 };
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;
    }
}
