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
            this.Value = new Vector2();
        }

        public AxisActions(AxisLayoutFlags axis) : this()
        {
            this.Axis = axis;
        }

        public void Execute(AxisLayout layout)
        {
            layout.vector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(layout.vector, AxisDeadZoneInner, AxisDeadZoneOuter);
            layout.vector = InputUtils.ApplyAntiDeadzone(layout.vector, AxisAntiDeadZone);

            switch (OutputShape)
            {
                default:
                    break;
                case OutputShape.Circle:
                    layout.vector = InputUtils.ImproveCircularity(layout.vector);
                    break;
                case OutputShape.Cross:
                    layout.vector = InputUtils.CrossDeadzoneMapping(layout.vector, AxisDeadZoneInner, AxisDeadZoneOuter);
                    layout.vector = InputUtils.ImproveCircularity(layout.vector);
                    break;
                case OutputShape.Square:
                    layout.vector = InputUtils.ImproveSquare(layout.vector);
                    break;
            }

            // invert axis
            layout.vector = new(InvertHorizontal ? -layout.vector.X : layout.vector.X, InvertVertical ? -layout.vector.Y : layout.vector.Y);

            this.Value = layout.vector;
        }

        public Vector2 GetValue()
        {
            return (Vector2)this.Value;
        }
    }
}
