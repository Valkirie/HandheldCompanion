﻿using System;
using System.Numerics;
using System.Windows.Forms;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;

namespace ControllerCommon.Actions;

[Serializable]
public class AxisActions : IActions
{
    private Vector2 prevVector;
    private Vector2 Vector;

    public AxisActions()
    {
        ActionType = ActionType.Joystick;
        Vector = new Vector2();
        prevVector = new Vector2();
    }

    public AxisActions(AxisLayoutFlags axis) : this()
    {
        Axis = axis;
    }

    public AxisLayoutFlags Axis { get; set; }

    // Axis to axis
    public bool AxisInverted { get; set; } = false;
    public bool AxisRotated { get; set; } = false;
    public int AxisDeadZoneInner { get; set; } = 0;
    public int AxisDeadZoneOuter { get; set; } = 0;
    public float AxisAntiDeadZone { get; set; } = 0.0f;
    public bool ImproveCircularity { get; set; } = false;

    public override void Execute(AxisFlags axis, short value)
    {
        // Apply inner and outer deadzone adjustments
        value = (short)InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, short.MaxValue);

        // Apply anti deadzone adjustments
        switch (axis)
        {
            case AxisFlags.L2:
            case AxisFlags.R2:
                value = (short)InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone);
                break;
        }

        Value = (short)(value * (AxisInverted ? -1 : 1));
    }

    public void Execute(AxisLayout layout)
    {
        layout.vector =
            InputUtils.ThumbScaledRadialInnerOuterDeadzone(layout.vector, AxisDeadZoneInner, AxisDeadZoneOuter);

        if (ImproveCircularity)
            layout.vector = InputUtils.ImproveCircularity(layout.vector);

        if (AutoRotate)
            Vector = ((Orientation & ScreenOrientation.Angle90) == ScreenOrientation.Angle90
                         ? new Vector2(layout.vector.Y, -layout.vector.X)
                         : layout.vector)
                     * ((Orientation & ScreenOrientation.Angle180) == ScreenOrientation.Angle180 ? -1.0f : 1.0f);
        else
            Vector = (AxisRotated ? new Vector2(layout.vector.Y, -layout.vector.X) : layout.vector)
                     * (AxisInverted ? -1.0f : 1.0f);
    }

    public Vector2 GetValue()
    {
        return Vector;
    }
}