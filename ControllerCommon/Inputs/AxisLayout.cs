using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public struct AxisLayout
    {
        public static AxisLayout None = new AxisLayout(AxisLayoutFlags.None);
        public static AxisLayout LeftThumb = new AxisLayout(AxisLayoutFlags.LeftThumb, AxisFlags.LeftThumbX, AxisFlags.LeftThumbY);
        public static AxisLayout RightThumb = new AxisLayout(AxisLayoutFlags.RightThumb, AxisFlags.RightThumbX, AxisFlags.RightThumbY);
        public static AxisLayout L2 = new AxisLayout(AxisLayoutFlags.L2, AxisFlags.L2);
        public static AxisLayout R2 = new AxisLayout(AxisLayoutFlags.R2, AxisFlags.R2);
        public static AxisLayout LeftPad = new AxisLayout(AxisLayoutFlags.LeftPad, AxisFlags.LeftPadX, AxisFlags.LeftPadY);
        public static AxisLayout RightPad = new AxisLayout(AxisLayoutFlags.RightPad, AxisFlags.RightPadX, AxisFlags.RightPadY);

        public static Dictionary<AxisLayoutFlags, AxisLayout> Layouts = new()
        {
            { AxisLayoutFlags.None, None },
            { AxisLayoutFlags.LeftThumb, LeftThumb },
            { AxisLayoutFlags.RightThumb, RightThumb },
            { AxisLayoutFlags.L2, L2 },
            { AxisLayoutFlags.R2, R2 },
            { AxisLayoutFlags.LeftPad, LeftPad },
            { AxisLayoutFlags.RightPad, RightPad },
        };

        public AxisLayoutFlags flags = AxisLayoutFlags.None;
        private Dictionary<char, AxisFlags> axis = new();
        public Vector2 vector = new();

        public AxisLayout(AxisLayoutFlags flags)
        {
            this.flags = flags;
        }

        public AxisLayout(AxisLayoutFlags flags, AxisFlags axisY) : this(flags)
        {
            axis.Add('Y', axisY);
        }

        public AxisLayout(AxisLayoutFlags flags, AxisFlags axisX, AxisFlags axisY) : this(flags, axisY)
        {
            axis.Add('X', axisX);
        }

        public AxisFlags GetAxisFlags(char axisName)
        {
            if (axis.ContainsKey(axisName))
                return axis[axisName];

            return AxisFlags.None;
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
