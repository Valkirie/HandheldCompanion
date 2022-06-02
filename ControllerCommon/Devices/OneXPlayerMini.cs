using System.Numerics;
using static ControllerCommon.OneEuroFilter;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMini : Device
    {
        public OneXPlayerMini() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_onexplayer_mini";

            this.AngularVelocityAxis = new Vector3(1.0f, 1.0f, 1.0f);
            this.AccelerationAxis = new Vector3(-1.0f, -1.0f, 1.0f);

            oneEuroSettings = new OneEuroSettings(0.002d, 0.008d);
        }
    }
}
