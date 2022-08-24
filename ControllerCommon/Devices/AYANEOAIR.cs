using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AYANEOAIR : Device
    {
        public AYANEOAIR() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_aya_air";

            // https://www.amd.com/fr/products/apu/amd-ryzen-5-5560u
            this.nTDP = new double[] { 8, 8, 15 };
            this.cTDP = new double[] { 10, 25 };

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
