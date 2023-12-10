namespace HandheldCompanion.Devices;

public class NUCDeck : IDevice
{
    public NUCDeck()
    {
        // device specific settings
        ProductIllustration = "device_cncdan_nucdeck";
        ProductModel = "NUCDeck";

        // https://www.intel.com/content/www/us/en/products/sku/97539/intel-core-i57260u-processor-4m-cache-up-to-3-40-ghz/specifications.html
        nTDP = new double[] { 15, 15, 15 };
        cTDP = new double[] { 9, 15 };
        GfxClock = new double[] { 300, 950 };
        CpuClock = 3400;
    }

}