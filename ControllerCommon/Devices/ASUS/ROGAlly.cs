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
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 5, 53 };
            this.GfxClock = new double[] { 100, 2200 };

            // device specific capacities

            /*
            OEMChords.Add(new DeviceChord("Special Left",
                new List<KeyCode>() { KeyCode.F11, KeyCode.L },
                new List<KeyCode>() { KeyCode.F11, KeyCode.L },
                false, ButtonFlags.OEM1
                ));
            */
        }
    }
}
