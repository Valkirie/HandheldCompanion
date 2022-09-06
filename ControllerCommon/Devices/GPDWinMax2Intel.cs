using System.Numerics;

namespace ControllerCommon.Devices
{
    public class GPDWinMax2Intel : Device
    {
        public GPDWinMax2Intel() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_gpd_winmax2";

            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 15, 28 };

            this.AngularVelocityAxis = new Vector3(1.0f, -1.0f, 1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };
        }
    }
}
