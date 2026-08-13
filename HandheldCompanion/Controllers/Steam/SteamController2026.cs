using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Text;

namespace HandheldCompanion.Controllers.Steam
{
    public class SteamController2026 : SDLController
    {
        public SteamController2026(nint gamepad, uint deviceIndex, PnPDetails details) : base(gamepad, deviceIndex, details)
        {
            // Misc5 = B9 = Pressure-sensitive button left
            // Misc6 = B10 = Pressure-sensitive button right
        }

        public override string ToString()
        {
            return "Steam Controller";
        }

        public override IEnumerable<ButtonFlags> GetMixedHotkeyExcludedButtons()
        {
            return [ButtonFlags.B9, ButtonFlags.B10];
        }

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
                    return "\u21B0";
                case ButtonFlags.R1:
                    return "\u21B1";
                case ButtonFlags.Back:
                    return "\u21FA";
                case ButtonFlags.Start:
                    return "\u21FB";
                case ButtonFlags.L2Soft:
                case ButtonFlags.L2Full:
                    return "\u21B2";
                case ButtonFlags.R2Soft:
                case ButtonFlags.R2Full:
                    return "\u21B3";
                case ButtonFlags.L4:
                    return "\u2276";
                case ButtonFlags.L5:
                    return "\u2278";
                case ButtonFlags.R4:
                    return "\u2277";
                case ButtonFlags.R5:
                    return "\u2279";
                case ButtonFlags.Special:
                    return "\u21E4";
                case ButtonFlags.Special2:
                    return "\u21E5";
                case ButtonFlags.LeftStickTouch:
                    return "\u21DA";
                case ButtonFlags.RightStickTouch:
                    return "\u21DB";
                case ButtonFlags.LeftPadTouch:
                    return "\u2268";
                case ButtonFlags.RightPadTouch:
                    return "\u2269";
                case ButtonFlags.LeftPadClick:
                    return "\u2266";
                case ButtonFlags.RightPadClick:
                    return "\u2267";
                case ButtonFlags.LeftPadClickUp:
                    return "\u2270";
                case ButtonFlags.LeftPadClickDown:
                    return "\u2274";
                case ButtonFlags.LeftPadClickLeft:
                    return "\u226E";
                case ButtonFlags.LeftPadClickRight:
                    return "\u2272";
                case ButtonFlags.RightPadClickUp:
                    return "\u2271";
                case ButtonFlags.RightPadClickDown:
                    return "\u2275";
                case ButtonFlags.RightPadClickLeft:
                    return "\u226F";
                case ButtonFlags.RightPadClickRight:
                    return "\u2273";
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
                case AxisLayoutFlags.LeftPad:
                    return "\u2264";
                case AxisLayoutFlags.RightPad:
                    return "\u2265";
                case AxisLayoutFlags.Gyroscope:
                    return "\u2B94";
            }

            return base.GetGlyph(axis);
        }
    }
}
