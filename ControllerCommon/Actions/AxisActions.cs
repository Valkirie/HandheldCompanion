using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using System;
using System.Numerics;
using System.Windows.Forms;

namespace ControllerCommon.Actions
{
    [Serializable]
    public class AxisActions : IActions
    {
        public AxisLayoutFlags Axis { get; set; }
        private Vector2 Vector;
        private Vector2 prevVector;
        private ScreenOrientation currentAutoOrientation = ScreenOrientation.Angle0;

        // Axis to axis
        public bool AxisInverted { get; set; } = false;
        public bool AxisRotated { get; set; } = false;
        public bool AutoRotate { get; set; } = false;
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

        public void SetAutoOrientation(ScreenOrientation autoOrientation)
        {
            this.currentAutoOrientation = autoOrientation;
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

            if (AutoRotate)
                this.Vector = (((this.currentAutoOrientation & ScreenOrientation.Angle90) == ScreenOrientation.Angle90) ? new(layout.vector.Y, -layout.vector.X) : layout.vector) * (((this.currentAutoOrientation & ScreenOrientation.Angle180) == ScreenOrientation.Angle180) ? -1.0f : 1.0f);
            else
                this.Vector = (AxisRotated ? new(layout.vector.Y, -layout.vector.X) : layout.vector) * (AxisInverted ? -1.0f : 1.0f);
        }

        public Vector2 GetValue()
        {
            return this.Vector;
        }
    }
}
