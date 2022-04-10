namespace ControllerCommon.Devices
{
    public class AYANEONEXT : Device
    {
        public AYANEONEXT(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName, new DeviceController(0x045E, 0x028E))
        {
            this.WidthHeightRatio = 2.4d;
        }
    }
}
