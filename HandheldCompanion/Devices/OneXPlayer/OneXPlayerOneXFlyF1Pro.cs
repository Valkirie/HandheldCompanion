namespace HandheldCompanion.Devices;

public class OneXPlayerOneXFlyF1Pro : OneXPlayerOneXFly
{
    public OneXPlayerOneXFlyF1Pro()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;
    }
}