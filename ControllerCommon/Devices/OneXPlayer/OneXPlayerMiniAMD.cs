using System.Numerics;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniAMD : OneXPlayerMini
    {
        public OneXPlayerMiniAMD() : base()
        {
            // https://www.amd.com/fr/products/apu/amd-ryzen-7-5800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 10, 25 };
            this.GfxClock = new double[] { 100, 2000 };

            this.AccelerationAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        }
    }
}
