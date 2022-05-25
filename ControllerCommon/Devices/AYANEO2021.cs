using static ControllerCommon.OneEuroFilter;

namespace ControllerCommon.Devices
{
    public class AYANEO2021 : Device
    {
        public AYANEO2021() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_aya_2021";

            oneEuroSettings = new OneEuroSettings(0.002d, 0.008d);
        }
    }
}
