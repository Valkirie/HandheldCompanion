using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers.SDL
{
    public class DualShock4Controller : SDLController
    {
        public DualShock4Controller()
        { }

        public DualShock4Controller(nint gamepad, uint deviceIndex, PnPDetails details) : base(gamepad, deviceIndex, details)
        { }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.B1:
                    return "\u21E3"; // Cross
                case ButtonFlags.B2:
                    return "\u21E2"; // Circle
                case ButtonFlags.B3:
                    return "\u21E0"; // Square
                case ButtonFlags.B4:
                    return "\u21E1"; // Triangle
                case ButtonFlags.L1:
                    return "\u21B0";
                case ButtonFlags.R1:
                    return "\u21B1";
                case ButtonFlags.Back:
                    return "\u21E6";
                case ButtonFlags.Start:
                    return "\u21E8";
                case ButtonFlags.L2Soft:
                    return "\u21B2";
                case ButtonFlags.L2Full:
                    return "\u21B2";
                case ButtonFlags.R2Soft:
                    return "\u21B3";
                case ButtonFlags.R2Full:
                    return "\u21B3";
                case ButtonFlags.Special:
                    return "\uE000";
                case ButtonFlags.LeftPadClick:
                case ButtonFlags.RightPadClick:
                case ButtonFlags.LeftPadTouch:
                case ButtonFlags.RightPadTouch:
                    return "\u21E7";
            }

            return base.GetGlyph(button);
        }

        public override string GetGlyph(AxisFlags axis)
        {
            switch (axis)
            {
                case AxisFlags.L2:
                    return "\u21B2";
                case AxisFlags.R2:
                    return "\u21B3";
            }

            return base.GetGlyph(axis);
        }

        public override string GetGlyph(AxisLayoutFlags axis)
        {
            switch (axis)
            {
                case AxisLayoutFlags.L2:
                    return "\u21B2";
                case AxisLayoutFlags.R2:
                    return "\u21B3";
                case AxisLayoutFlags.LeftPad:
                case AxisLayoutFlags.RightPad:
                    return "\u21E7";
            }

            return base.GetGlyph(axis);
        }
    }
}
