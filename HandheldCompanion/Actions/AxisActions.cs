using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HandheldCompanion.Actions
{
    public enum OutputShape
    {
        Default = 0,
        Circle = 1,
        Cross = 2,
        Square = 3,
    }

    [Serializable]
    public sealed class AxisActions : GyroActions
    {
        public AxisLayoutFlags Axis;

        public short ButtonX = 0;
        public short ButtonY = 0;

        // Deadzone / anti-deadzone settings (percent, 0..100)
        public int AxisAntiDeadZone = 0;
        public int AxisDeadZoneInner = 0;
        public int AxisDeadZoneOuter = 0;
        public OutputShape OutputShape = OutputShape.Default;

        public bool InvertHorizontal = false;
        public bool InvertVertical = false;

        // Response curve: 6 control points from 0-1 range, default is linear
        public List<Vector2> ResponseCurvePoints = new List<Vector2>
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(0.2f, 0.2f),
            new Vector2(0.4f, 0.4f),
            new Vector2(0.6f, 0.6f),
            new Vector2(0.8f, 0.8f),
            new Vector2(1.0f, 1.0f)
        };

        public AxisActions()
        {
            actionType = ActionType.Joystick;
        }

        public AxisActions(AxisLayoutFlags axis) : this()
        {
            Axis = axis;
        }

        public float XOuput { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => outVector.X; }
        public float YOuput { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => outVector.Y; }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            if (!outBool)
            {
                outVector = Vector2.Zero;
                return;
            }

            outVector = new Vector2(ButtonX, ButtonY);
            ApplyAxisModifiers();
        }

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            ApplyAxisModifiers();
        }

        private void ApplyAxisModifiers()
        {
            if (outVector == Vector2.Zero) return;

            // Apply radial deadzones
            outVector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(outVector, AxisDeadZoneInner, AxisDeadZoneOuter);

            // Apply anti-deadzone
            outVector = InputUtils.ApplyAntiDeadzone(outVector, AxisAntiDeadZone);

            // Apply response curve
            outVector = InputUtils.ApplyResponseCurve(outVector, ResponseCurvePoints);

            // Reshape the output
            outVector = OutputShape switch
            {
                OutputShape.Circle => InputUtils.ImproveCircularity(outVector),
                OutputShape.Cross => InputUtils.ImproveCircularity(InputUtils.CrossDeadzoneMapping(outVector, AxisDeadZoneInner, AxisDeadZoneOuter)),
                OutputShape.Square => InputUtils.ImproveSquare(outVector),

                _ => outVector,
            };

            // Axis inversion
            if (InvertHorizontal) outVector.X = -outVector.X;
            if (InvertVertical) outVector.Y = -outVector.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 GetValue() => outVector;
    }
}
