namespace HandheldCompanion.Devices;

public class AYANEOAIRPlusIntel : AYANEOAIRPlus
{
    public AYANEOAIRPlusIntel()
    {
        // https://www.intel.com/content/www/us/en/products/sku/226263/intel-core-i31215u-processor-10m-cache-up-to-4-40-ghz/specifications.html
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 5, 55 };
        this.GfxClock = new double[] { 100, 1100 };
        this.CpuClock = 4400;

        this.ECDetails.AddressFanDuty = 0x02;
    }
}