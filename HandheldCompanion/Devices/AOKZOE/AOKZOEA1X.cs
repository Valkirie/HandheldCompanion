namespace HandheldCompanion.Devices;

public class AOKZOEA1X : AOKZOEA1Pro
{
    public AOKZOEA1X()
    {
        // device specific settings
        ProductModel = "AOKZOEA1X";

        // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;
    }
}