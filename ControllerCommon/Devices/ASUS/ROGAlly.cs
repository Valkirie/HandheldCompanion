using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using System;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class ROGAlly : IDevice
    {
        public ROGAlly() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_rog_ally";

            // https://www.amd.com/en/products/apu/amd-ryzen-z1
            // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
            // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 5, 53 };
            this.GfxClock = new double[] { 100, 2700 };

            this.AngularVelocityAxis = new Vector3(-1.0f, 1.0f, 1.0f);
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
