using System.Collections.Generic;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AYANEONEXT : Device
    {
        public AYANEONEXT() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_aya_next";
            this.ProductModel = "AYANEONext";

            this.DefaultTDP = 20;

            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            listeners.Add(new DeviceChord("Custom key BIG", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12 }));
            listeners.Add(new DeviceChord("Custom key Small", new List<KeyCode>() { KeyCode.LWin, KeyCode.D }));
        }
    }
}
