using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Windows.Forms;

namespace HandheldCompanion.Devices.AYANEO
{
    public class AYANEODevice : IDevice
    {
        protected enum LEDGroup
        {
            StickLeft = 1,
            StickRight = 2,
            StickBoth = 3,
            AYA = 4,
        }

        public AYANEODevice()
        {
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM1:
                    return "\uE003";
                case ButtonFlags.OEM2:
                    return "\u220B";
                case ButtonFlags.OEM3:
                    return "\u2209";
                case ButtonFlags.OEM4:
                    return "\u220A";
                case ButtonFlags.OEM5:
                    return "\u0054";
                case ButtonFlags.OEM6:
                    return "\uE001";
            }

            return defaultGlyph;
        }
    }
}
