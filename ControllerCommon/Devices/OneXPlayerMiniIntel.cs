using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniIntel : Device
    {
        public OneXPlayerMiniIntel() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_onexplayer_mini";
            this.ProductModel = "ONEXPLAYERMini";

            this.DefaultTDP = 28;

            this.AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'Y' },
                { 'Y', 'Z' },
                { 'Z', 'X' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, 1.0f, -1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            listeners.Add(new DeviceChord("Keyboard key", new List<KeyCode>() { KeyCode.LWin, KeyCode.RControlKey, KeyCode.O }));
            listeners.Add(new DeviceChord("Function key", new List<KeyCode>() { KeyCode.LWin, KeyCode.D }));
            listeners.Add(new DeviceChord("Function + Volume Up", new List<KeyCode>() { KeyCode.F1 }));

            // Workaround: Function + Fan is sending one PrintScreen *down* and two PrintScreen *up*
            listeners.Add(new DeviceChord("Function + Fan", new List<KeyCode>() { KeyCode.LWin, KeyCode.PrintScreen }));
            listeners.Add(new DeviceChord("Function + Fan", new List<KeyCode>() { KeyCode.LWin, KeyCode.PrintScreen, KeyCode.PrintScreen }));
        }
    }
}
