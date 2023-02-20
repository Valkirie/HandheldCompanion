using System;
using System.Collections.Generic;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public struct AxisLayout
    {
        public static AxisLayout LeftThumb = new AxisLayout(new List<AxisFlags>() { AxisFlags.LeftThumbX, AxisFlags.LeftThumbY });
        public static AxisLayout RightThumb = new AxisLayout(new List<AxisFlags>() { AxisFlags.RightThumbX, AxisFlags.RightThumbY });
        public static AxisLayout L2 = new AxisLayout(new List<AxisFlags>() { AxisFlags.L2 });
        public static AxisLayout R2 = new AxisLayout(new List<AxisFlags>() { AxisFlags.R2 });
        public static AxisLayout LeftPad = new AxisLayout(new List<AxisFlags>() { AxisFlags.LeftPadX, AxisFlags.LeftPadY });
        public static AxisLayout RightPad = new AxisLayout(new List<AxisFlags>() { AxisFlags.RightPadX, AxisFlags.RightPadY });

        public static Dictionary<AxisLayoutFlags, AxisLayout> Layouts = new()
        {
            {AxisLayoutFlags.LeftThumb, LeftThumb },
            {AxisLayoutFlags.RightThumb, RightThumb },
            {AxisLayoutFlags.L2, L2 },
            {AxisLayoutFlags.R2, R2 },
            {AxisLayoutFlags.LeftPad, LeftPad },
            {AxisLayoutFlags.RightPad, RightPad },
        };

        public List<AxisFlags> axis;
        public string glyph;

        public AxisLayout(List<AxisFlags> axis)
        {
            this.axis = axis;
        }

        public override string ToString()
        {
            return "TEST";
        }
    }

    [Serializable]
    public enum AxisLayoutFlags : byte
    {
        None = 0,
        LeftThumb,
        RightThumb,
        L2, R2,
        LeftPad,
        RightPad,
    }
}
