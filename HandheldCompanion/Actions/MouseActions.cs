using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using HandheldCompanion.Utils;
using System;
using System.ComponentModel;
using System.Numerics;
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
    public class MouseActions : GyroActions
    {
        public MouseActionsType MouseType;

        // const settings
        private const int scrollAmountInClicks = 20;
        private const float FilterBeta = 0.5f;

        // runtime variables
        private bool IsCursorDown = false;
        private bool IsTouched = false;
        private Vector2 remainder = new();
        private KeyCode[] pressed;
        private OneEuroFilterPair mouseFilter;

        // settings click
        public ModifierSet Modifiers = ModifierSet.None;

        // settings axis
        public int Sensivity = 33;
        public float Acceleration = 1.0f;
        public int Deadzone = 15;           // stick only
        public bool Filtering = false;      // pad only
        public float FilterCutoff = 0.05f;  // pad only

        public MouseActions()
        {
            this.actionType = ActionType.Mouse;

            this.Value = false;
            this.prevValue = false;

            mouseFilter = new(FilterCutoff, FilterBeta);
        }

        public MouseActions(MouseActionsType type) : this()
        {
            this.MouseType = type;
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot)
        {
            base.Execute(button, value, shiftSlot);

            switch (this.Value)
            {
                case true:
                    {
                        if (IsCursorDown)
                            return;

                        IsCursorDown = true;
                        pressed = ModifierMap[Modifiers];
                        KeyboardSimulator.KeyDown(pressed);
                        MouseSimulator.MouseDown(MouseType, scrollAmountInClicks);
                        SetHaptic(button, false);
                    }
                    break;
                case false:
                    {
                        if (!IsCursorDown)
                            return;

                        IsCursorDown = false;
                        MouseSimulator.MouseUp(MouseType);
                        KeyboardSimulator.KeyUp(pressed);
                        SetHaptic(button, true);
                    }
                    break;
            }
        }

        private bool IsNewTouch(bool value)
        {
            if (value == IsTouched)
                return false;
            if (IsTouched = value)
                return true;
            return false;
        }

        private void ExecuteButton(AxisLayout layout, bool touched, ShiftSlot shiftSlot)
        {
            // update value
            this.Vector = layout.vector;

            // call parent, check shiftSlot
            base.Execute(layout, shiftSlot);

            // skip if zero
            if (this.Vector == Vector2.Zero && !IsCursorDown)
                return;

            MotionDirection direction = InputUtils.GetMotionDirection(this.Vector, motionThreshold);
            bool value = (direction.HasFlag(motionDirection) || motionDirection.HasFlag(direction)) && direction != MotionDirection.None;

            // transition to Button Execute()
            Execute(ButtonFlags.None, value, shiftSlot);
        }

        private void ExecuteAxis(AxisLayout layout, bool touched, ShiftSlot shiftSlot)
        {
            // this line needs to be before the next vector zero check
            bool newTouch = IsNewTouch(touched);

            // update value
            this.Vector = layout.vector;

            // call parent, check shiftSlot
            base.Execute(layout, shiftSlot);

            // skip if zero
            if (this.Vector == Vector2.Zero)
                return;

            // invert axis
            this.Vector.Y *= -1;

            Vector2 deltaVector;
            float sensitivityFinetune;

            switch (layout.flags)
            {
                case AxisLayoutFlags.LeftStick:
                case AxisLayoutFlags.RightStick:
                case AxisLayoutFlags.Gyroscope:
                default:
                    {
                        // convert to <0.0-1.0> values
                        deltaVector = this.Vector / short.MaxValue;
                        float deadzone = Deadzone / 100.0f;

                        // apply deadzone
                        if (deltaVector.Length() < deadzone)
                            return;

                        deltaVector *= (deltaVector.Length() - deadzone) / deltaVector.Length();  // shorten by deadzone
                        deltaVector *= 1.0f / (1.0f - deadzone);                                  // rescale to 0.0 - 1.0

                        sensitivityFinetune = (MouseType == MouseActionsType.Move ? 0.3f : 0.1f);
                    }
                    break;

                case AxisLayoutFlags.LeftPad:
                case AxisLayoutFlags.RightPad:
                    {
                        // touchpad was touched, update entry point for delta calculations
                        if (newTouch)
                        {
                            prevVector = this.Vector;
                            return;
                        }

                        // calculate delta and convert to <0.0-1.0> values
                        deltaVector = (this.Vector - prevVector) / short.MaxValue;
                        prevVector = this.Vector;

                        sensitivityFinetune = (MouseType == MouseActionsType.Move ? 9.0f : 3.0f);
                    }
                    break;
            }

            if (Filtering)
            {
                mouseFilter.SetFilterCutoff(FilterCutoff);
                deltaVector.X = (float)mouseFilter.axis1Filter.Filter(deltaVector.X, 1);
                deltaVector.Y = (float)mouseFilter.axis2Filter.Filter(deltaVector.Y, 1);
            }

            if (Acceleration != 1.0f)
            {
                deltaVector.X = (float)(Math.Sign(deltaVector.X) * Math.Pow(Math.Abs(deltaVector.X), Acceleration));
                deltaVector.Y = (float)(Math.Sign(deltaVector.Y) * Math.Pow(Math.Abs(deltaVector.Y), Acceleration));
                sensitivityFinetune = (float)Math.Pow(sensitivityFinetune, Acceleration);
            }

            // apply sensitivity and slider finetune
            deltaVector *= Sensivity * sensitivityFinetune;

            // handle the fact that MoveBy()/*Scroll() are int only and we can have movement (0 < abs(delta) < 1)
            deltaVector += remainder;                                               // add partial previous step
            Vector2 intVector = new((int)Math.Truncate(deltaVector.X), (int)Math.Truncate(deltaVector.Y));
            remainder = deltaVector - intVector;                                    // and save the unused rest

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

        public void Execute(AxisLayout layout, bool touched, ShiftSlot shiftSlot)
        {
            switch (MouseType)
            {
                case MouseActionsType.Move:
                case MouseActionsType.Scroll:
                    ExecuteAxis(layout, touched, shiftSlot);
                    break;
                default:
                    ExecuteButton(layout, touched, shiftSlot);
                    break;
            }
        }
    }
}
