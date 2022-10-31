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

            // Select
            listeners.Add(new DeviceChord("Select key",
                new List<KeyCode>() { KeyCode.Tab },
                new List<KeyCode>() { KeyCode.Tab }
                ));

            // Start
            listeners.Add(new DeviceChord("Start key",
                new List<KeyCode>() { KeyCode.Escape },
                new List<KeyCode>() { KeyCode.Escape }
                ));

            // Desktop
            listeners.Add(new DeviceChord("Desktop key",
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
        }
    }
}
