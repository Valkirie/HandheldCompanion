﻿using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using System;
using System.Numerics;

namespace ControllerCommon.Actions
{
    [Serializable]
    public class AxisActions : IActions
    {
        public AxisLayoutFlags Axis { get; set; }
        private Vector2 Vector;
        private Vector2 prevVector;

        // Axis to axis
        public bool AxisInverted { get; set; } = false;
        public int AxisDeadZoneInner { get; set; } = 0;
        public int AxisDeadZoneOuter { get; set; } = 0;
        public float AxisAntiDeadZone { get; set; } = 0.0f;
        public bool ImproveCircularity { get; set; } = false;

        public AxisActions()
        {
            this.ActionType = ActionType.Joystick;
            this.Vector = new();
            this.prevVector = new();
        }

        public AxisActions(AxisLayoutFlags axis) : this()
        {
            this.Axis = axis;
        }

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

            this.Value = (short)(value * (AxisInverted ? -1 : 1));
        }

        public void Execute(AxisLayout layout)
        {
            layout.vector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(layout.vector, AxisDeadZoneInner, AxisDeadZoneOuter);

            if (ImproveCircularity)
                layout.vector = InputUtils.ImproveCircularity(layout.vector);

            this.Vector = layout.vector * (AxisInverted ? -1.0f : 1.0f);
        }

        public Vector2 GetValue()
        {
            return this.Vector;
        }
    }
}
