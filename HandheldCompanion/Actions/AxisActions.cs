using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Numerics;

namespace HandheldCompanion.Actions
{
    public enum OutputShape
    {
        Default = 0,
        Circle  = 1,
        Cross   = 2,
        Square  = 3,
    }

    [Serializable]
    public sealed class AxisActions : GyroActions
    {
        public AxisLayoutFlags Axis;

        // Deadzone / anti-deadzone settings (percent, 0..100)
        public int         AxisAntiDeadZone  = 0;
        public int         AxisDeadZoneInner = 0;
        public int         AxisDeadZoneOuter = 0;
        public OutputShape OutputShape       = OutputShape.Default;

        public bool InvertHorizontal = false;
        public bool InvertVertical   = false;

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

            if (outVector == Vector2.Zero) return;

            // Apply radial deadzones
            outVector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(outVector, AxisDeadZoneInner, AxisDeadZoneOuter);

            // Apply anti-deadzone
            outVector = InputUtils.ApplyAntiDeadzone(outVector, AxisAntiDeadZone);

            // Reshape the output
            outVector = OutputShape switch
            {
                OutputShape.Circle => InputUtils.ImproveCircularity(outVector),
                OutputShape.Cross  => InputUtils.ImproveCircularity(InputUtils.CrossDeadzoneMapping(outVector, AxisDeadZoneInner, AxisDeadZoneOuter)),
                OutputShape.Square => InputUtils.ImproveSquare(outVector),

                _ => outVector,
            };

            // Axis inversion
            if (InvertHorizontal) outVector.X = -outVector.X;
            if (InvertVertical)   outVector.Y = -outVector.Y;
        }

        public Vector2 GetValue() => outVector;
    }
}
