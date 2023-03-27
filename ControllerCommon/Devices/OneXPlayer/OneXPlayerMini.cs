using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMini : IDevice
    {
        public OneXPlayerMini() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_onexplayer_mini";
            this.ProductModel = "ONEXPLAYERMini";

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

            // unused
            OEMChords.Add(new DeviceChord("Fan",
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                false, ButtonFlags.OEM5
                ));

            OEMChords.Add(new DeviceChord("Keyboard",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.RControlKey, KeyCode.O },
                new List<KeyCode>() { KeyCode.O, KeyCode.RControlKey, KeyCode.LWin },
                false, ButtonFlags.OEM2
                ));

            OEMChords.Add(new DeviceChord("Function",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                new List<KeyCode>() { KeyCode.D, KeyCode.LWin },
                false, ButtonFlags.OEM3
                ));

            OEMChords.Add(new DeviceChord("Function + Volume Up",
                new List<KeyCode>() { KeyCode.F1 },
                new List<KeyCode>() { KeyCode.F1, KeyCode.F1 }
                ));

            // dirty implementation from OneX...
            OEMChords.Add(new DeviceChord("Function + Fan",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.Snapshot },
                new List<KeyCode>() { KeyCode.Snapshot, KeyCode.LWin },
                false, ButtonFlags.OEM1
                ));
            OEMChords.Add(new DeviceChord("Function + Fan",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.Snapshot },
                new List<KeyCode>() { KeyCode.Snapshot, KeyCode.Snapshot, KeyCode.LWin },
                false, ButtonFlags.OEM1
                ));
        }

        public override bool Open()
        {
            bool success = base.Open();
            if (!success)
                return false;

            // allow OneX button to pass key inputs
            return ECRamDirectWrite(0xF1, 0x40);
        }

        public override void Close()
        {
            ECRamDirectWrite(0xF1, 0x00);
            base.Close();
        }
    }
}
