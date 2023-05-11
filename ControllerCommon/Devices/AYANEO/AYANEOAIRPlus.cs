using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AYANEOAIRPlus : IDevice
    {
        public AYANEOAIRPlus() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_aya_air";
            this.ProductModel = "AYANEOAir";

            // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 3, 33 };
            this.GfxClock = new double[] { 100, 2200 };

            this.AngularVelocityAxis = new Vector3(1.0f, -1.0f, -1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, -1.0f, -1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            OEMChords.Add(new DeviceChord("Custom Key Top Right",
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.F16 },
                new List<KeyCode>() { KeyCode.F16, KeyCode.LControl, KeyCode.LWin },
                false, ButtonFlags.OEM3
                ));

            OEMChords.Add(new DeviceChord("Custom Key Top Left",
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.F15 },
                new List<KeyCode>() { KeyCode.F15, KeyCode.LControl, KeyCode.LWin },
                false, ButtonFlags.OEM4
                ));

            OEMChords.Add(new DeviceChord("Custom Key Big",
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.F17 },
                new List<KeyCode>() { KeyCode.F17, KeyCode.LControl, KeyCode.LWin },
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("Custom Key Small",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                new List<KeyCode>() { KeyCode.D, KeyCode.LWin },
                false, ButtonFlags.OEM2
                ));
        }
    }
}
