namespace ControllerCommon.Devices
{
    public class AYANEO2021 : Device
    {
        public AYANEO2021(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName)
        {
            this.ProductSupported = true;

            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_aya_2021";
        }
    }
}
