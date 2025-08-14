using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers.SDL
{
    public class Xbox360Controller : SDLController
    {
        public Xbox360Controller()
        { }

        public Xbox360Controller(nint gamepad, uint deviceIndex, PnPDetails details) : base(gamepad, deviceIndex, details)
        { }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.B1:
                    return "\u21D3"; // Button A
                case ButtonFlags.B2:
                    return "\u21D2"; // Button B
                case ButtonFlags.B3:
                    return "\u21D0"; // Button X
                case ButtonFlags.B4:
                    return "\u21D1"; // Button Y
                case ButtonFlags.L1:
                    return "\u2198";
                case ButtonFlags.R1:
                    return "\u2199";
                case ButtonFlags.Back:
                    return "\u21FA";
                case ButtonFlags.Start:
                    return "\u21FB";
                case ButtonFlags.L2Soft:
                    return "\u21DC";
                case ButtonFlags.L2Full:
                    return "\u2196";
                case ButtonFlags.R2Soft:
                    return "\u21DD";
                case ButtonFlags.R2Full:
                    return "\u2197";
                case ButtonFlags.Special:
                    return "\uE001";
            }

            return base.GetGlyph(button);
        }

        public override string GetGlyph(AxisFlags axis)
        {
            switch (axis)
            {
                case AxisFlags.L2:
                    return "\u2196";
                case AxisFlags.R2:
                    return "\u2197";
            }

            return base.GetGlyph(axis);
        }

        public override string GetGlyph(AxisLayoutFlags axis)
        {
            switch (axis)
            {
                case AxisLayoutFlags.L2:
                    return "\u2196";
                case AxisLayoutFlags.R2:
                    return "\u2197";
            }

            return base.GetGlyph(axis);
        }
    }
}
