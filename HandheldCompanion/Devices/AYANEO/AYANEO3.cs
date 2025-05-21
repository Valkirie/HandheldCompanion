using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices.AYANEO
{
    public class AYANEO3 : AYANEODeviceCEii
    {
        public AYANEO3()
        {
            ProductIllustration = "device_aya_3";
            ProductModel = "AYANEO 3";

            nTDP = new double[] { 15, 20, 28 };
            cTDP = new double[] { 3, 54 };
            GfxClock = new double[] { 100, 2900 };
            CpuClock = 5100;

            AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);

            this.OEMChords.Clear();

            this.OEMChords.Add(new KeyboardChord("Custom Key Big",
                new List<KeyCode> { KeyCode.F23 },
                new List<KeyCode> { KeyCode.F23 },
                false,
                ButtonFlags.OEM1));

            this.OEMChords.Add(new KeyboardChord("Custom Key Small",
                new List<KeyCode> { KeyCode.F24 },
                new List<KeyCode> { KeyCode.F24 },
                false,
                ButtonFlags.OEM2));

            this.OEMChords.Add(new KeyboardChord("Custom Key Top Right",
                new List<KeyCode> { KeyCode.F22 },
                new List<KeyCode> { KeyCode.F22 },
                false,
                ButtonFlags.OEM3));

            this.OEMChords.Add(new KeyboardChord("Custom Key Top Left",
                new List<KeyCode> { KeyCode.F21 },
                new List<KeyCode> { KeyCode.F21 },
                false,
                ButtonFlags.OEM4));

            this.OEMChords.Add(new KeyboardChord("RC1",
                new List<KeyCode> { KeyCode.LControlKey, KeyCode.LShiftKey, KeyCode.F12 },
                new List<KeyCode> { KeyCode.LControlKey, KeyCode.LShiftKey, KeyCode.F12 },
                false,
                ButtonFlags.OEM5
            ));

            this.OEMChords.Add(new KeyboardChord("LC1",
                new List<KeyCode> { KeyCode.LControlKey, KeyCode.LShiftKey, KeyCode.F11 },
                new List<KeyCode> { KeyCode.LControlKey, KeyCode.LShiftKey, KeyCode.F11 },
                false,
                ButtonFlags.OEM6
            ));
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM5: // RC1
                    return "RC1";
                case ButtonFlags.OEM6: // LC1
                    return "LC1";
                default:
                    return base.GetGlyph(button);
            }
        }
    }
}