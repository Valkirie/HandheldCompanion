namespace ControllerCommon.Devices
{
    public class OXPAMDMini : Device
    {
        public OXPAMDMini(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName, new DeviceController(0x045E, 0x028E))
        {
            this.WidthHeightRatio = 2.4d;
        }
    }
}
