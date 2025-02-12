using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Numerics;

namespace HandheldCompanion.Actions
{
    public enum OutputShape
    {
        Default = 0,
        Circle = 1,
        Cross = 2,
        Square = 3
    }

    [Serializable]
    public class AxisActions : GyroActions
    {
        public AxisLayoutFlags Axis;

        // Axis to axis
        public int AxisAntiDeadZone = 0;
        public int AxisDeadZoneInner = 0;
        public int AxisDeadZoneOuter = 0;
        public OutputShape OutputShape = OutputShape.Default;

        public bool InvertHorizontal = false;
        public bool InvertVertical = false;

        public AxisActions()
        {
            this.actionType = ActionType.Joystick;
        }

        public AxisActions(AxisLayoutFlags axis) : this()
        {
            this.Axis = axis;
        }

        public float XOuput => this.Vector.X;
        public float YOuput => this.Vector.Y;

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot)
        {
            // update value
            this.Vector = layout.vector;

            // call parent, check shiftSlot
            base.Execute(layout, shiftSlot);

            // skip if zero
            if (this.Vector == Vector2.Zero)
                return;

            this.Vector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(this.Vector, AxisDeadZoneInner, AxisDeadZoneOuter);
            this.Vector = InputUtils.ApplyAntiDeadzone(this.Vector, AxisAntiDeadZone);

            switch (OutputShape)
            {
                default:
                    break;
                case OutputShape.Circle:
                    this.Vector = InputUtils.ImproveCircularity(this.Vector);
                    break;
                case OutputShape.Cross:
                    this.Vector = InputUtils.CrossDeadzoneMapping(this.Vector, AxisDeadZoneInner, AxisDeadZoneOuter);
                    this.Vector = InputUtils.ImproveCircularity(this.Vector);
                    break;
                case OutputShape.Square:
                    this.Vector = InputUtils.ImproveSquare(this.Vector);
                    break;
            }

            // invert axis
            this.Vector = new Vector2(InvertHorizontal ? -this.Vector.X : this.Vector.X, InvertVertical ? -this.Vector.Y : this.Vector.Y);
        }

        public Vector2 GetValue()
        {
            return this.Vector;
        }
    }
}
