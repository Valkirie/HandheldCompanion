using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public struct AxisLayout
    {
        public static AxisLayout LeftThumb = new AxisLayout(AxisLayoutFlags.LeftThumb, AxisFlags.LeftThumbX, AxisFlags.LeftThumbY);
        public static AxisLayout RightThumb = new AxisLayout(AxisLayoutFlags.RightThumb, AxisFlags.RightThumbX, AxisFlags.RightThumbY);
        public static AxisLayout L2 = new AxisLayout(AxisLayoutFlags.L2, AxisFlags.L2);
        public static AxisLayout R2 = new AxisLayout(AxisLayoutFlags.R2, AxisFlags.R2);
        public static AxisLayout LeftPad = new AxisLayout(AxisLayoutFlags.LeftPad, AxisFlags.LeftPadX, AxisFlags.LeftPadY);
        public static AxisLayout RightPad = new AxisLayout(AxisLayoutFlags.RightPad, AxisFlags.RightPadX, AxisFlags.RightPadY);

        public static Dictionary<AxisLayoutFlags, AxisLayout> Layouts = new()
        {
            { AxisLayoutFlags.LeftThumb, LeftThumb },
            { AxisLayoutFlags.RightThumb, RightThumb },
            { AxisLayoutFlags.L2, L2 },
            { AxisLayoutFlags.R2, R2 },
            { AxisLayoutFlags.LeftPad, LeftPad },
            { AxisLayoutFlags.RightPad, RightPad },
        };

        public AxisLayoutFlags flags = AxisLayoutFlags.None;
        public List<AxisFlags> axis = new();
        public Vector2 vector = new();
        public string glyph;

        public AxisLayout(AxisLayoutFlags flags, AxisFlags axisX)
        {
            this.flags = flags;
            axis.Add(axisX);
        }

        public AxisLayout(AxisLayoutFlags flags, AxisFlags axisX, AxisFlags axisY) : this(flags, axisX)
        {
            axis.Add(axisY);
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
