namespace HandheldCompanion.Devices;

public class OneXPlayerX1Pro : OneXPlayerX1AMD
{
    public OneXPlayerX1Pro()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;
    }
}
