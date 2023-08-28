namespace HandheldCompanion.Devices;

public class AYANEOAIRPlusIntel : AYANEOAIRPlus
{
    public AYANEOAIRPlusIntel()
    {
        // https://www.intel.com/content/www/us/en/products/sku/226263/intel-core-i31215u-processor-10m-cache-up-to-4-40-ghz/specifications.html
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 55 };
        GfxClock = new double[] { 100, 1100 };
    }
}