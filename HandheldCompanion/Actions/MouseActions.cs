using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using HandheldCompanion.Utils;
using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using WindowsInput.Events;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum MouseActionsType
    {
        [Description("Left Button")]
        LeftButton = 1,
        [Description("Right Button")]
        RightButton = 2,
        [Description("Middle Button")]
        MiddleButton = 3,

        [Description("Move Cursor")]
        Move = 4,
        [Description("Scroll Wheel")]
        Scroll = 5,

        [Description("Scroll Up")]
        ScrollUp = 6,
        [Description("Scroll Down")]
        ScrollDown = 7,
    }

    [Serializable]
    public sealed class MouseActions : GyroActions
    {
        public MouseActionsType MouseType;

        private const int scrollAmountInClicks = 20;
        private const float FilterBeta = 0.5f;

        // runtime variables
        private bool IsCursorDown = false;
        private bool IsTouched = false;
        private Vector2 remainder = new();
        private KeyCode[] pressed;
        private OneEuroFilterPair mouseFilter;

        protected override bool GetActualOutputState() => MouseSimulator.IsButtonDown(MouseType);

        // settings click
        public ModifierSet Modifiers = ModifierSet.None;

        // settings axis
        public int Sensivity = 33;
        public float Acceleration = 1.0f;   // Acceleration <= 1.0 => off; > 1.0 => stronger memory/boost
        public int Deadzone = 15;           // stick only
        public bool Filtering = false;      // pad only
        public float FilterCutoff = 0.05f;  // pad only

        // runtime variables
        private float accelMemory = 0.0f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public MouseActions()
        {
            actionType = ActionType.Mouse;
            outBool = false;
            prevBool = false;
            mouseFilter = new(FilterCutoff, FilterBeta);
        }

        public MouseActions(MouseActionsType type) : this()
        {
            MouseType = type;
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            if (outBool)
            {
                if (IsCursorDown) return;
                IsCursorDown = true;
                pressed = ModifierMap[Modifiers];
                KeyboardSimulator.KeyDown(pressed);
                MouseSimulator.MouseDown(MouseType, scrollAmountInClicks);
                SetHaptic(button, false);
            }
            else
            {
                if (!IsCursorDown) return;
                IsCursorDown = false;
                MouseSimulator.MouseUp(MouseType);
                KeyboardSimulator.KeyUp(pressed);
                SetHaptic(button, true);
            }
        }

        private bool IsNewTouch(bool value)
        {
            if (value == IsTouched)
                return false;
            IsTouched = value;
            return IsTouched;
        }

        private void ExecuteButton(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero && !IsCursorDown)
                return;

            var direction = InputUtils.GetDeflectionDirection(outVector, motionThreshold);
            bool press = DirectionMatches(direction, motionDirection);

            Execute(ButtonFlags.None, press, shiftSlot, delta);
        }

        private void ExecuteAxis(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            bool newTouch = IsNewTouch(touched);

            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero)
                return;

            // invert Y
            outVector.Y *= -1;

            Vector2 deltaVector;
            float sensitivityFinetune;

            switch (layout.flags)
            {
                default:
                case AxisLayoutFlags.LeftStick:
                case AxisLayoutFlags.RightStick:
                case AxisLayoutFlags.Gyroscope:
                    {
                        deltaVector = outVector / short.MaxValue;
                        float dz = Deadzone / 100.0f;

                        if (deltaVector.Length() < dz)
                            return;

                        deltaVector *= (deltaVector.Length() - dz) / deltaVector.Length();
                        deltaVector *= 1.0f / (1.0f - dz);

                        sensitivityFinetune = (MouseType == MouseActionsType.Move ? 0.3f : 0.1f);
                        break;
                    }

                case AxisLayoutFlags.LeftPad:
                case AxisLayoutFlags.RightPad:
                    {
                        if (newTouch)
                        {
                            prevVector = outVector;
                            return;
                        }

                        deltaVector = (outVector - prevVector) / short.MaxValue;
                        prevVector = outVector;

                        sensitivityFinetune = (MouseType == MouseActionsType.Move ? 9.0f : 3.0f);
                        break;
                    }
            }

            if (Filtering)
            {
                mouseFilter.SetFilterCutoff(FilterCutoff);
                deltaVector.X = (float)mouseFilter.axis1Filter.Filter(deltaVector.X, 1);
                deltaVector.Y = (float)mouseFilter.axis2Filter.Filter(deltaVector.Y, 1);
            }

            float s = MathF.Max(0f, Acceleration - 1.0f);
            if (s > 0f)
            {
                // decay memory with a half-life that grows with 's'
                // half-life ~ 24 ms when s->0, up to ~144 ms when s=1 (Acceleration=2.0)
                float halfLifeMs = 24f + 120f * s;
                float decay = (float)Math.Exp(-0.69314718056f * (delta / halfLifeMs));

                float mag = deltaVector.Length();
                if (mag > accelMemory)
                    accelMemory = mag;
                else
                    accelMemory *= decay;

                float t = Smooth01(MathF.Min(1f, accelMemory));
                float gainMax = 1f + 2.6f * s; // 2.6f tweak me
                float gain = 1f + (gainMax - 1f) * t;

                deltaVector *= gain;
            }

            deltaVector *= Sensivity * sensitivityFinetune;

            // accumulate fractional remainders to keep precision
            deltaVector += remainder;
            Vector2 intVector = new((int)Math.Truncate(deltaVector.X), (int)Math.Truncate(deltaVector.Y));
            remainder = deltaVector - intVector;

            if (MouseType == MouseActionsType.Move)
            {
                MouseSimulator.MoveBy((int)intVector.X, (int)intVector.Y);
            }
            else if (MouseType == MouseActionsType.Scroll)
            {
                // MouseSimulator.HorizontalScroll((int)-intVector.X);
                MouseSimulator.VerticalScroll((int)-intVector.Y);
            }
        }

        public void Execute(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            switch (MouseType)
            {
                case MouseActionsType.Move:
                case MouseActionsType.Scroll:
                    ExecuteAxis(layout, touched, shiftSlot, delta);
                    break;
                default:
                    ExecuteButton(layout, touched, shiftSlot, delta);
                    break;
            }
        }
    }
}