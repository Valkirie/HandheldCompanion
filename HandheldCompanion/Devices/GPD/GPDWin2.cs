namespace HandheldCompanion.Devices;

public class GPDWin2 : IDevice
{
    public GPDWin2()
    {
        // device specific settings
        ProductIllustration = "device_gpd_win2";

        // https://www.intel.com/content/www/us/en/products/sku/185282/intel-core-m38100y-processor-4m-cache-up-to-3-40-ghz/specifications.html
        nTDP = new double[] { 10, 10, 15 };
        cTDP = new double[] { 5, 15 };
        GfxClock = new double[] { 300, 900 };
        CpuClock = 3400;
    }
}