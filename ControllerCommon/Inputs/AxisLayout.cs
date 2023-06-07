using System;
using System.Collections.Generic;
using System.Numerics;

namespace ControllerCommon.Inputs;

[Serializable]
public struct AxisLayout
{
    public static AxisLayout None = new(AxisLayoutFlags.None);
    public static AxisLayout LeftThumb = new(AxisLayoutFlags.LeftThumb, AxisFlags.LeftThumbX, AxisFlags.LeftThumbY);
    public static AxisLayout RightThumb = new(AxisLayoutFlags.RightThumb, AxisFlags.RightThumbX, AxisFlags.RightThumbY);
    public static AxisLayout L2 = new(AxisLayoutFlags.L2, AxisFlags.L2);
    public static AxisLayout R2 = new(AxisLayoutFlags.R2, AxisFlags.R2);
    public static AxisLayout LeftPad = new(AxisLayoutFlags.LeftPad, AxisFlags.LeftPadX, AxisFlags.LeftPadY);
    public static AxisLayout RightPad = new(AxisLayoutFlags.RightPad, AxisFlags.RightPadX, AxisFlags.RightPadY);

    public static SortedDictionary<AxisLayoutFlags, AxisLayout> Layouts = new()
    {
        { AxisLayoutFlags.None, None },
        { AxisLayoutFlags.LeftThumb, LeftThumb },
        { AxisLayoutFlags.RightThumb, RightThumb },
        { AxisLayoutFlags.L2, L2 },
        { AxisLayoutFlags.R2, R2 },
        { AxisLayoutFlags.LeftPad, LeftPad },
        { AxisLayoutFlags.RightPad, RightPad }
    };

    public AxisLayoutFlags flags = AxisLayoutFlags.None;
    private SortedDictionary<char, AxisFlags> axis = new();
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
        if (axis.TryGetValue(axisName, out var foundAxis))
            return foundAxis;

        return AxisFlags.None;
    }
}

[Serializable]
public enum AxisLayoutFlags : byte
{
    None = 0,
    LeftThumb = 1,
    RightThumb = 2,
    L2 = 3,
    R2 = 4,
    LeftPad = 5,
    RightPad = 6
}