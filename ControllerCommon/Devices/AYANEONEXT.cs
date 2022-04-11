namespace ControllerCommon.Devices
{
    public class AYANEONEXT : Device
    {
        public AYANEONEXT(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName)
        {
            this.ProductSupported = true;

            this.WidthHeightRatio = 2.4d;
            this.ProductIllustration = "device_aya_next";
        }
    }
}
