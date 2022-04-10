namespace ControllerCommon.Devices
{
    public class OXPAMDMini : Device
    {
        public OXPAMDMini(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName)
        {
            this.WidthHeightRatio = 2.4d;
        }
    }
}
