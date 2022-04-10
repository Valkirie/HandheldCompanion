namespace ControllerCommon.Devices
{
    public class AYANEONEXT : Device
    {
        public AYANEONEXT(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName)
        {
            this.WidthHeightRatio = 2.4d;
        }
    }
}
