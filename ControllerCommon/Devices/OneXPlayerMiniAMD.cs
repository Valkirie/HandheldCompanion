using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniAMD : Device
    {
        public OneXPlayerMiniAMD() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_onexplayer_mini";
            this.ProductModel = "ONEXPLAYERMini";

            // https://www.amd.com/fr/products/apu/amd-ryzen-7-5800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 10, 25 };

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

            listeners.Add(new DeviceChord("Keyboard key", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.O, KeyCode.LWin, KeyCode.O }));
            listeners.Add(new DeviceChord("Function key", new List<KeyCode>() { KeyCode.LWin, KeyCode.D }));
            listeners.Add(new DeviceChord("Function + Volume Up", new List<KeyCode>() { KeyCode.F1 }));

            // Workaround: Function + Fan is sending one PrintScreen *down* and two PrintScreen *up*
            listeners.Add(new DeviceChord("Function + Fan", new List<KeyCode>() { KeyCode.LWin, KeyCode.PrintScreen }));
            listeners.Add(new DeviceChord("Function + Fan", new List<KeyCode>() { KeyCode.LWin, KeyCode.PrintScreen, KeyCode.PrintScreen }));
        }
    }
}
