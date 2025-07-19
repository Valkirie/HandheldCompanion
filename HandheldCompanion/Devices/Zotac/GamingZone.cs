using HandheldCompanion.Inputs;
using WindowsInput.Events;

namespace HandheldCompanion.Devices.Zotac
{
    public class GamingZone : IDevice
    {
        public GamingZone()
        {
            // device specific settings
            this.ProductIllustration = "device_zotac_zone";
            
            // used to monitor OEM specific inputs
            vendorId = 0x1EE9;
            productIds = [0x1590];

            // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
            // https://www.amd.com/en/products/apu/amd-ryzen-7-8840u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 3, 28 };
            this.GfxClock = new double[] { 100, 2700 };
            this.CpuClock = 5100;

            this.OEMChords.Add(new KeyboardChord("ZOTAC key",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F17],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F17],
                false, ButtonFlags.OEM1
            ));

            this.OEMChords.Add(new KeyboardChord("Dots key",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F18],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F18],
                false, ButtonFlags.OEM2
            ));

            this.OEMChords.Add(new KeyboardChord("Home key",
                [KeyCode.LWin, KeyCode.D],
                [KeyCode.LWin, KeyCode.D],
                false, ButtonFlags.OEM3
            ));
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM1:
                    return "\u221D";
                case ButtonFlags.OEM2:
                    return "\u221E";
                case ButtonFlags.OEM3:
                    return "\u21F9";
            }

            return base.GetGlyph(button);
        }
    }
}
