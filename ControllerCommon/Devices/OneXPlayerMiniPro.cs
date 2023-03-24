using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniPro : IDevice
    {
        public OneXPlayerMiniPro() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_onexplayer_mini";
            this.ProductModel = "ONEXPLAYERMini";

            // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 4, 28 };
            this.GfxClock = new double[] { 100, 2200 };

            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, 1.0f, 1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            OEMChords.Add(new DeviceChord("Function + Fan",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.Snapshot },
                new List<KeyCode>() { KeyCode.LWin, KeyCode.Snapshot },
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("Keyboard",
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.O, KeyCode.RControlKey, KeyCode.LWin, KeyCode.K },
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.K, KeyCode.O, KeyCode.LWin, KeyCode.RControlKey },
                false, ButtonFlags.OEM2
                ));

            // dirty implementation from OneX...
            OEMChords.Add(new DeviceChord("Function",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                new List<KeyCode>() { KeyCode.D, KeyCode.LWin },
                false, ButtonFlags.OEM3
                ));
            OEMChords.Add(new DeviceChord("Function",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                false, ButtonFlags.OEM3
                ));

            OEMChords.Add(new DeviceChord("Function + Volume Up",
                new List<KeyCode>() { KeyCode.F7 },
                new List<KeyCode>() { KeyCode.F7 },
                false, ButtonFlags.OEM4
                ));

            OEMChords.Add(new DeviceChord("Fan",
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
                false, ButtonFlags.OEM5
                ));
        }
    }
}
