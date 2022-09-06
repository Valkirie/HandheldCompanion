using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AYANEOAIRPro : Device
    {
        public AYANEOAIRPro() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_aya_air";

            // https://www.amd.com/en/products/apu/amd-ryzen-7-5825u
            this.nTDP = new double[] { 12, 12, 15 };
            this.cTDP = new double[] { 8, 18 };

            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, 1.0f, -1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            listeners.Add(new DeviceChord("Custom Key Top Right", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F10 }));
            listeners.Add(new DeviceChord("Custom Key Top Left", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F11 }));
            listeners.Add(new DeviceChord("Custom Key Big", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12 }));
            listeners.Add(new DeviceChord("Custom Key Small", new List<KeyCode>() { KeyCode.LWin, KeyCode.D }));
        }
    }
}
