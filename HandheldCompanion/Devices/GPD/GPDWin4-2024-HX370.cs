namespace HandheldCompanion.Devices;

public class GPDWin4_2024_HX370 : GPDWin4_2024
{
    public GPDWin4_2024_HX370()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;
    }
}
