using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using HandheldCompanion.Simulators;
using System;
using System.ComponentModel;
using System.Numerics;
using WindowsInput.Events;
using static ControllerCommon.Utils.CommonUtils;

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
    public class MouseActions : IActions
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
        private Vector2 prevVector = new();

        // settings click
        public ModifierSet Modifiers = ModifierSet.None;

        // settings axis
        public int Sensivity = 33;
        public float Acceleration = 1.0f;
        public int Deadzone = 10;           // stick only
        public bool Filtering = false;      // pad only
        public float FilterCutoff = 0.05f;  // pad only
        public bool AxisRotated = false;
        public bool AxisInverted = false;

        public MouseActions()
        {
            this.ActionType = ActionType.Mouse;

            this.Value = false;
            this.prevValue = false;

            mouseFilter = new(FilterCutoff, FilterBeta);
        }

        public MouseActions(MouseActionsType type) : this()
        {
            this.MouseType = type;
        }

        public override void Execute(ButtonFlags button, bool value, int longTime)
        {
            base.Execute(button, value, longTime);

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
                    }
                    break;
                case false:
                    {
                        if (!IsCursorDown)
                            return;

                        IsCursorDown = false;
                        MouseSimulator.MouseUp(MouseType);
                        KeyboardSimulator.KeyUp(pressed);
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

        public void Execute(AxisLayout layout, bool touched)
        {
            // this line needs to be before the next vector zero check
            bool newTouch = IsNewTouch(touched);

            if (layout.vector == Vector2.Zero)
                return;

            layout.vector.Y *= -1;

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
                        deltaVector = layout.vector / short.MaxValue;
                        float deadzone = Deadzone / 100.0f;

                        // apply deadzone
                        if (deltaVector.Length() < deadzone)
                            return;

                        deltaVector *= (deltaVector.Length() - deadzone) / deltaVector.Length();  // shorten by deadzone
                        deltaVector *= 1.0f / (1.0f - deadzone);                                  // rescale to 0.0 - 1.0

                        sensitivityFinetune = (MouseType == MouseActionsType.Move ? 0.3f : 0.1f);

                        if (Acceleration != 1.0f)
                        {
                            deltaVector.X = (float)(Math.Sign(deltaVector.X) * Math.Pow(Math.Abs(deltaVector.X), Acceleration));
                            deltaVector.Y = (float)(Math.Sign(deltaVector.Y) * Math.Pow(Math.Abs(deltaVector.Y), Acceleration));
                            sensitivityFinetune = (float)Math.Pow(sensitivityFinetune, Acceleration);
                        }
                    }
                    break;

                case AxisLayoutFlags.LeftPad:
                case AxisLayoutFlags.RightPad:
                    {
                        // touchpad was touched, update entry point for delta calculations
                        if (newTouch)
                        {
                            prevVector = layout.vector;
                            return;
                        }

                        // calculate delta and convert to <0.0-1.0> values
                        deltaVector = (layout.vector - prevVector) / short.MaxValue;
                        prevVector = layout.vector;

                        sensitivityFinetune = (MouseType == MouseActionsType.Move ? 9.0f : 3.0f);

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
                    }
                    break;
            }

            // apply sensitivity, rotation and slider finetune
            deltaVector *= Sensivity * sensitivityFinetune;
            if (AxisRotated)
                deltaVector = new(-deltaVector.Y, deltaVector.X);
            deltaVector *= (AxisInverted ? -1.0f : 1.0f);

            // handle the fact that MoveBy()/*Scroll() are int only and we can have movement (0 < abs(delta) < 1)
            deltaVector += remainder;                                               // add partial previous step
            Vector2 intVector = new((int)Math.Truncate(deltaVector.X), (int)Math.Truncate(deltaVector.Y));
            remainder = deltaVector - intVector;                                    // and save the unused rest

            if (MouseType == MouseActionsType.Move)
            {
                MouseSimulator.MoveBy((int)intVector.X, (int)intVector.Y);
            }
            else /* if (MouseType == MouseActionsType.Scroll) */
            {
                // MouseSimulator.HorizontalScroll((int)-intVector.X);
                MouseSimulator.VerticalScroll((int)-intVector.Y);
            }
        }
    }
}