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

            // https://ark.intel.com/content/www/us/en/ark/products/226254/intel-core-i71260p-processor-18m-cache-up-to-4-70-ghz.html
            this.nTDP = new double[] { 28, 64 };
            this.cTDP = new double[] { 20, 64 };

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
