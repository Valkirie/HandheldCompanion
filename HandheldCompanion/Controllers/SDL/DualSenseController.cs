using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers.SDL
{
    public class DualSenseController : DualShock4Controller
    {
        public DualSenseController()
        { }

        public DualSenseController(nint gamepad, uint deviceIndex, PnPDetails details) : base(gamepad, deviceIndex, details)
        { }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.Back:
                    return "\u2206";
                case ButtonFlags.Start:
                    return "\u2208";
                case ButtonFlags.LeftPadClick:
                case ButtonFlags.RightPadClick:
                case ButtonFlags.CenterPadClick:
                case ButtonFlags.LeftPadTouch:
                case ButtonFlags.RightPadTouch:
                    return "\u2207";
            }

            return base.GetGlyph(button);
        }
    }
}
