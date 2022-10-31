using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AOKZOEA1 : Device
    {
        public AOKZOEA1() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.3f;
            this.ProductIllustration = "device_aokzoe_a1";
            this.ProductModel = "AOKZOEA1";

            // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u 
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 10, 28 };

            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, -1.0f, 1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            // Home
            listeners.Add(new DeviceChord("Home key",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D},
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D }
                ));

            // Keyboard
            listeners.Add(new DeviceChord("Keyboard key",
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.O },
                new List<KeyCode>() { KeyCode.O, KeyCode.LWin, KeyCode.RControlKey }
                ));

            // Turbo
            listeners.Add(new DeviceChord("Turbo key",
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu }
                ));
                
            // Home + Keyboard
            listeners.Add(new DeviceChord("Home + Keyboard",
                new List<KeyCode>() { KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete },
                new List<KeyCode>() { KeyCode.Delete, KeyCode.RControlKey, KeyCode.RAlt }
                ));
                
            // Home + Turbo
            listeners.Add(new DeviceChord("Home + Turbo",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.Snapshot },
                new List<KeyCode>() { KeyCode.Snapshot, KeyCode.LWin }
                ));
        }
    }
}
