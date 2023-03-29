using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWinMax2AMD : GPDWinMax2
    {
        public GPDWinMax2AMD() : base()
        {
            // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 28 };
            this.cTDP = new double[] { 15, 28 };
            this.GfxClock = new double[] { 100, 2200 };

            this.AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'Y' },
                { 'Y', 'Z' },
                { 'Z', 'X' },
            };

            this.AccelerationAxis = new Vector3(1.0f, -1.0f, 1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };
        }
    }
}
