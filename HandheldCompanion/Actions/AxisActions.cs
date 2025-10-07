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
    public sealed class AxisActions : GyroActions
    {
        public AxisLayoutFlags Axis;

        // Axis to axis
        public int AxisAntiDeadZone = 0;   // percent [0..100]
        public int AxisDeadZoneInner = 0;  // percent [0..100]
        public int AxisDeadZoneOuter = 0;  // percent [0..100]
        public OutputShape OutputShape = OutputShape.Default;

        public bool InvertHorizontal = false;
        public bool InvertVertical = false;

        public AxisActions()
        {
            actionType = ActionType.Joystick;
        }

        public AxisActions(AxisLayoutFlags axis) : this()
        {
            Axis = axis;
        }

        public float XOuput => outVector.X;
        public float YOuput => outVector.Y;

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero)
                return;

            // radial inner/outer (percentages)
            outVector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(outVector, AxisDeadZoneInner, AxisDeadZoneOuter);

            // anti-deadzone (percentage)
            outVector = InputUtils.ApplyAntiDeadzone(outVector, AxisAntiDeadZone);

            switch (OutputShape)
            {
                case OutputShape.Circle:
                    outVector = InputUtils.ImproveCircularity(outVector);
                    break;

                case OutputShape.Cross:
                    // Use percent-based overload (no pre-normalization needed)
                    outVector = InputUtils.CrossDeadzoneMapping(outVector, AxisDeadZoneInner, AxisDeadZoneOuter);
                    outVector = InputUtils.ImproveCircularity(outVector);
                    break;

                case OutputShape.Square:
                    outVector = InputUtils.ImproveSquare(outVector);
                    break;

                default:
                    break;
            }

            // invert axis
            outVector = new Vector2(InvertHorizontal ? -outVector.X : outVector.X,
                                 InvertVertical ? -outVector.Y : outVector.Y);
        }

        public Vector2 GetValue() => outVector;
    }
}