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
        [Description("Left Button")]           LeftButton   = 1,
        [Description("Right Button")]          RightButton  = 2,
        [Description("Middle Button")]         MiddleButton = 3,
        [Description("Move Cursor (Relative)")] Move        = 4,
        [Description("Scroll Wheel")]          Scroll       = 5,
        [Description("Scroll Up")]             ScrollUp     = 6,
        [Description("Scroll Down")]           ScrollDown   = 7,
        [Description("Move Cursor (Absolute)")] MoveTo      = 8,
    }

    [Serializable]
    public sealed class MouseActions : GyroActions
    {
        public MouseActionsType MouseType;

        private const int   ScrollAmountInClicks = 20;
        private const float FilterBeta           = 0.5f;

        // Runtime
        private bool              isCursorDown = false;
        private bool              isTouched    = false;
        private Vector2           remainder    = new();
        private KeyCode[]         modifiersPressed;
        private OneEuroFilterPair mouseFilter;
        private float             accelMemory = 0f;

        // Click settings
        public ModifierSet Modifiers = ModifierSet.None;

        // Axis settings (sticks / pads)
        public int   Sensivity      = 33;
        public float Acceleration   = 1.0f;     // ≤ 1.0 = off; > 1.0 = boost
        public int   Deadzone       = 15;        // stick only
        public bool  Filtering      = false;     // pad only
        public float FilterCutoff   = 0.05f;    // pad only

        // MoveTo settings
        public double MoveToX           = 0;
        public double MoveToY           = 0;
        public bool   MoveToPrevious    = true;
        private double moveToPrevX      = 0;
        private double moveToPrevY      = 0;
        private bool   moveToRestorePending = false;

        public MouseActions()
        {
            actionType  = ActionType.Mouse;
            outBool     = false;
            prevBool    = false;
            mouseFilter = new(FilterCutoff, FilterBeta);
        }

        public MouseActions(MouseActionsType type) : this()
        {
            MouseType = type;
        }

        /// <summary>
        /// Shares toggle state across all bindings targeting the same mouse button,
        /// and detects external button releases.
        /// Only applicable to button-type actions (not Move / Scroll).
        /// </summary>
        protected override (bool useShared, bool toggleState) GetSharedToggleState(bool risingEdge)
        {
            if (IsAxisType(MouseType)) return (false, false);

            bool state = MouseSimulator.GetToggleState(MouseType);

            if (risingEdge)
                state = MouseSimulator.FlipToggle(MouseType);

            return (true, state);
        }

        private static bool IsAxisType(MouseActionsType t) => t is MouseActionsType.Move or MouseActionsType.Scroll or MouseActionsType.ScrollUp or MouseActionsType.ScrollDown;

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            if (outBool)
            {
                if (isCursorDown) return;
                isCursorDown = true;

                modifiersPressed = ModifierMap[Modifiers];
                if (modifiersPressed is not null)
                    KeyboardSimulator.KeyDown(modifiersPressed);

                switch (MouseType)
                {
                    case MouseActionsType.MoveTo:
                        if (MoveToPrevious && !moveToRestorePending)
                        {
                            moveToPrevX          = MouseSimulator.MouseX;
                            moveToPrevY          = MouseSimulator.MouseY;
                            moveToRestorePending = true;
                        }
                        MouseSimulator.MoveTo(MoveToX, MoveToY);
                        break;

                    default:
                        MouseSimulator.MouseDown(MouseType, ScrollAmountInClicks);
                        break;
                }

                SetHaptic(button, released: false);
            }
            else
            {
                if (!isCursorDown) return;
                isCursorDown = false;

                switch (MouseType)
                {
                    case MouseActionsType.MoveTo:
                        if (MoveToPrevious && moveToRestorePending)
                        {
                            MouseSimulator.MoveTo(moveToPrevX, moveToPrevY);
                            moveToRestorePending = false;
                        }
                        break;

                    default:
                        MouseSimulator.MouseUp(MouseType);
                        break;
                }

                if (modifiersPressed is not null)
                    KeyboardSimulator.KeyUp(modifiersPressed);

                SetHaptic(button, released: true);
            }
        }

        public void Execute(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            switch (MouseType)
            {
                case MouseActionsType.Move:
                case MouseActionsType.Scroll:
                    ExecuteAxisContinuous(layout, touched, shiftSlot, delta);
                    break;

                case MouseActionsType.MoveTo:
                    ExecuteAxisMoveTo(layout, touched, shiftSlot, delta);
                    break;

                default:
                    ExecuteAxisAsButton(layout, touched, shiftSlot, delta);
                    break;
            }
        } 

        private void ExecuteAxisAsButton(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero && !isCursorDown)
                return;

            var  direction = InputUtils.GetDeflectionDirection(outVector, motionThreshold);
            bool press     = DirectionMatches(direction, motionDirection);

            Execute(ButtonFlags.None, press, shiftSlot, delta);
        }

        private void ExecuteAxisContinuous(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            bool firstTouch = ConsumeNewTouch(touched);

            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero) return;

            // Invert Y so that "up" on a stick or pad moves the cursor up
            outVector.Y *= -1;

            Vector2 deltaVector;
            float   sensitivityScale;

            switch (layout.flags)
            {
                default:
                case AxisLayoutFlags.LeftStick:
                case AxisLayoutFlags.RightStick:
                case AxisLayoutFlags.Gyroscope:
                    deltaVector      = ComputeStickDelta(outVector);
                    sensitivityScale = MouseType == MouseActionsType.Move ? 0.3f : 0.1f;
                    break;

                case AxisLayoutFlags.LeftPad:
                case AxisLayoutFlags.RightPad:
                    if (firstTouch)
                    {
                        prevVector = outVector;
                        return;
                    }
                    deltaVector      = (outVector - prevVector) / short.MaxValue;
                    prevVector       = outVector;
                    sensitivityScale = MouseType == MouseActionsType.Move ? 9.0f : 3.0f;
                    break;
            }

            if (Filtering)
            {
                mouseFilter.SetFilterCutoff(FilterCutoff);
                deltaVector.X = (float)mouseFilter.axis1Filter.Filter(deltaVector.X, 1);
                deltaVector.Y = (float)mouseFilter.axis2Filter.Filter(deltaVector.Y, 1);
            }

            deltaVector  = ApplyAcceleration(deltaVector, delta);
            deltaVector *= Sensivity * sensitivityScale;

            // Accumulate fractional pixels to maintain sub-pixel precision
            deltaVector += remainder;
            var intDelta = new Vector2((int)Math.Truncate(deltaVector.X), (int)Math.Truncate(deltaVector.Y));
            remainder    = deltaVector - intDelta;

            if (MouseType == MouseActionsType.Move)
                MouseSimulator.MoveBy((int)intDelta.X, (int)intDelta.Y);
            else
                MouseSimulator.VerticalScroll((int)-intDelta.Y);
        }

        private Vector2 ComputeStickDelta(Vector2 raw)
        {
            var    delta = raw / short.MaxValue;
            float  dz    = Deadzone / 100f;

            if (delta.Length() < dz) return Vector2.Zero;

            // Remap so the dead-zone edge starts at 0
            delta *= (delta.Length() - dz) / delta.Length() / (1f - dz);
            return delta;
        }

        /// <summary>
        /// Smoothstep used for soft acceleration curves.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private Vector2 ApplyAcceleration(Vector2 delta, float deltaMs)
        {
            float s = MathF.Max(0f, Acceleration - 1f);
            if (s <= 0f) return delta;

            // Half-life grows with s: ~24 ms at s→0, ~144 ms at s=1 (Acceleration=2)
            float halfLifeMs = 24f + 120f * s;
            float decay      = (float)Math.Exp(-0.693147f * (deltaMs / halfLifeMs));

            float mag = delta.Length();
            if (mag > accelMemory) accelMemory = mag;
            else                   accelMemory *= decay;

            float t       = Smooth01(MathF.Min(1f, accelMemory));
            float gainMax = 1f + 2.6f * s;
            float gain    = 1f + (gainMax - 1f) * t;

            return delta * gain;
        }

        private void ExecuteAxisMoveTo(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            ConsumeNewTouch(touched);

            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            float threshold  = motionThreshold / short.MaxValue;
            bool  hasMovement = outVector.Length() > threshold;

            if (hasMovement && !isCursorDown)
            {
                isCursorDown = true;

                if (MoveToPrevious && !moveToRestorePending)
                {
                    moveToPrevX          = MouseSimulator.MouseX;
                    moveToPrevY          = MouseSimulator.MouseY;
                    moveToRestorePending = true;
                }

                MouseSimulator.MoveTo(MoveToX, MoveToY);
            }
            else if (!hasMovement && isCursorDown)
            {
                isCursorDown = false;

                if (MoveToPrevious && moveToRestorePending)
                {
                    MouseSimulator.MoveTo(moveToPrevX, moveToPrevY);
                    moveToRestorePending = false;
                }
            }
        }

        /// <summary>
        /// Returns true (and updates state) on the first frame the pad is touched.
        /// </summary>
        private bool ConsumeNewTouch(bool touched)
        {
            if (touched == isTouched) return false;
            isTouched = touched;
            return isTouched;
        }
    }
}
