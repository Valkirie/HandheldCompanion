namespace ControllerCommon.Devices
{
    public class GPDWin2 : IDevice
    {
        public GPDWin2() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_gpd_win2";

            // https://www.intel.com/content/www/us/en/products/sku/185282/intel-core-m38100y-processor-4m-cache-up-to-3-40-ghz/specifications.html
            this.nTDP = new double[] { 10, 10, 15 };
            this.cTDP = new double[] { 5, 15 };
            this.GfxClock = new double[] { 300, 900 };
        }
    }
}
