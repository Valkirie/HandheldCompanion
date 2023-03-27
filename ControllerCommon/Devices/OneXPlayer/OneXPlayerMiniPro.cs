using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniPro : OneXPlayerMini
    {
        public OneXPlayerMiniPro() : base()
        {
            // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 4, 28 };
            this.GfxClock = new double[] { 100, 2200 };

            this.AccelerationAxis = new Vector3(-1.0f, 1.0f, 1.0f);
        }
    }
}
