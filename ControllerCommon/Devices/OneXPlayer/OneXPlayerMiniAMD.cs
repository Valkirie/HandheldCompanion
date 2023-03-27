using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniAMD : OneXPlayerMini
    {
        public OneXPlayerMiniAMD() : base()
        {
            // https://www.amd.com/fr/products/apu/amd-ryzen-7-5800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 10, 25 };
            this.GfxClock = new double[] { 100, 2000 };

            this.AccelerationAxis = new Vector3(-1.0f, -1.0f, 1.0f);

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
    }
}
