using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Simulators;
using System;
using System.ComponentModel;
using System.Numerics;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum MouseActionsType
    {
        [Description("Left Button")]
        LeftButton = 0,
        [Description("Right Button")]
        RightButton = 1,
        [Description("Middle Button")]
        MiddleButton = 2,

        [Description("Move Cursor")]
        Move = 3,
        [Description("Scroll Wheel")]
        Scroll = 4,

        [Description("Scroll Up")]
        ScrollUp = 5,
        [Description("Scroll Down")]
        ScrollDown = 6,
    }

    [Serializable]
    public class MouseActions : IActions
    {
        public MouseActionsType MouseType { get; set; }

        private bool IsCursorDown { get; set; }
        private bool IsCursorUp { get; set; }
        private int scrollAmountInClicks { get; set; } = 1;

        // settings
        public bool EnhancePrecision { get; set; } = false;
        public float Sensivity { get; set; } = 25.0f;
        public float Deadzone { get; set; } = 25.0f;
        public bool AxisInverted { get; set; } = false;

        public MouseActions()
        {
            this.ActionType = ActionType.Mouse;
            this.IsCursorDown = false;
            this.IsCursorUp = true;

            this.Value = false;
            this.prevValue = false;
        }

        public MouseActions(MouseActionsType type) : this()
        {
            this.MouseType = type;
        }

        public override void Execute(ButtonFlags button, bool value)
        {
            if (Toggle)
            {
                if ((bool)prevValue != value && value)
                    IsToggled = !IsToggled;
            }
            else
                IsToggled = false;

            if (Turbo)
            {
                if (value || IsToggled)
                {
                    if (TurboIdx % TurboDelay == 0)
                        IsTurboed = !IsTurboed;

                    TurboIdx += Period;
                }
                else
                {
                    IsTurboed = false;
                    TurboIdx = 0;
                }
            }
            else
                IsTurboed = false;

            // update previous value
            prevValue = value;

            // update value
            if (Toggle && Turbo)
                this.Value = IsToggled && IsTurboed;
            else if (Toggle)
                this.Value = IsToggled;
            else if (Turbo)
                this.Value = IsTurboed;
            else
                this.Value = value;

            switch (this.Value)
            {
                case true:
                    {
                        if (IsCursorDown || !IsCursorUp)
                            return;

                        IsCursorDown = true;
                        IsCursorUp = false;
                        MouseSimulator.MouseDown(MouseType, scrollAmountInClicks);
                    }
                    break;
                case false:
                    {
                        if (IsCursorUp || !IsCursorDown)
                            return;

                        IsCursorUp = true;
                        IsCursorDown = false;
                        MouseSimulator.MouseUp(MouseType);
                    }
                    break;
            }
        }

        public override void Execute(AxisFlags axis, short value)
        {
        }

        private bool IsTouched = false;

        private bool IsNewTouch(bool value)
        {
            if (value == IsTouched)
                return false;
            if (IsTouched = value)
                return true;
            return false;
        }

        private Vector2 prevVector = new();
        private Vector2 restVector = new();

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
                default:
                case AxisLayoutFlags.LeftThumb:
                case AxisLayoutFlags.RightThumb:
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
                    }
                    break;
            }

            // apply sensitivity, invert and slider finetune
            deltaVector *= Sensivity * sensitivityFinetune;
            deltaVector *= (AxisInverted ? -1.0f : 1.0f);

            // handle the fact that MoveBy()/*Scroll() are int only and we can have movement (0 < abs(delta) < 1)
            deltaVector += restVector;                                               // add partial previous step
            Vector2 intVector = new((int)Math.Truncate(deltaVector.X), (int)Math.Truncate(deltaVector.Y));
            restVector = deltaVector - intVector;                                    // and save the unused rest

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
