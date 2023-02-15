using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using HandheldCompanion.Simulators;
using PrecisionTiming;
using System;
using System.Diagnostics;
using System.Drawing;
using static HandheldCompanion.Simulators.MouseSimulator;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class MouseActions : IActions
    {
        public MouseActionsType MouseType { get; set; }

        private bool IsCursorDown { get; set; }
        private bool IsCursorUp { get; set; }

        // settings
        public float Sensivity { get; set; } = 20.0f;
        public bool IsTrackpad { get; set; } = true;

        public MouseActions()
        {
            this.ActionType = ActionType.Mouse;
            this.IsCursorDown = false;
            this.IsCursorUp = true;

            this.Value = (short)0;
            this.prevValue = (short)0;

            this.UpdateTimer = new PrecisionTimer();
            this.UpdateTimer.SetInterval(300);
            this.UpdateTimer.SetAutoResetMode(false);
            this.UpdateTimer.Tick += (e, sender) => ResetCursor();
        }

        private void ResetCursor()
        {
            entrypoint = Convert.ToInt16(prevValue);
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

                    TurboIdx += UPDATE_INTERVAL;
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
                        MouseSimulator.MouseDown(MouseType);
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

        private short entrypoint = 0;
        private Point entryMousePos = new(0, 0);

        public override void Execute(AxisFlags axis, short value)
        {
            if (IsTrackpad)
            {
                // no touch input
                if (value == 0)
                {
                    entrypoint = 0;
                    prevValue = 0;
                    return;
                }

                // touch input
                var MousePosition = MouseSimulator.GetMousePosition();
                if (Convert.ToInt16(prevValue) == 0)
                {
                    entrypoint = value;
                    prevValue = value;
                    entryMousePos = MousePosition;
                    return;
                }

                // get travel distance between two ticks (10ms)
                double dist = Math.Abs(value - Convert.ToInt16(prevValue));

                // get relative distance between entry point and current point
                var output = (value - Convert.ToInt16(entrypoint)) / 300;

                // update previous value
                prevValue = value;

                switch (MouseType)
                {
                    case MouseActionsType.MoveByX:
                        int outputX = (int)(entryMousePos.X + output);
                        MouseSimulator.MoveTo(outputX, MousePosition.Y);
                        break;
                    case MouseActionsType.MoveByY:
                        int outputY = (int)(entryMousePos.Y - output);
                        MouseSimulator.MoveTo(MousePosition.X, outputY);
                        break;
                }

                return;
            }

            short MoveBy = (short)(Convert.ToDouble(value) / short.MaxValue * Sensivity);

            switch (MouseType)
            {
                case MouseActionsType.MoveByX:
                    MouseSimulator.MoveBy(MoveBy, 0);
                    break;
                case MouseActionsType.MoveByY:
                    MouseSimulator.MoveBy(0, -MoveBy);
                    break;
            }

            this.prevValue = value;
        }
    }
}
