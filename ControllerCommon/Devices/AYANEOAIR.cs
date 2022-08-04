using System.Collections.Generic;
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

            this.DefaultTDP = 8;

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

            listeners.Add(new DeviceChord("Top Left Long", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.RControlKey, KeyCode.RAlt, KeyCode.Delete }));
            listeners.Add(new DeviceChord("Top Left Short", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin }));
            listeners.Add(new DeviceChord("Top Right Long", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.LWin, KeyCode.Tab }));
            listeners.Add(new DeviceChord("Top Right Short", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.Escape }));
            listeners.Add(new DeviceChord("Custom Key Small", new List<KeyCode>() { KeyCode.LWin, KeyCode.LWin, KeyCode.D }));
            listeners.Add(new DeviceChord("Custom Key Big", new List<KeyCode>() { KeyCode.LControlKey, KeyCode.LWin }));
        }
    }
}
