using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Simulators;
using LiveCharts.Wpf;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Documents;
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

        public override void Execute(AxisFlags axis, short value)
        {
            /* if (IsTrackpad)
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
            } */
        }

        private short entrypoint = 0;
        private Vector2 entryMousePos = new();
        private bool IsPressed = false;

        private Vector2 Vector = new();
        private Vector2 prevVector = new();

        public void Execute(AxisLayout layout)
        {
            if (layout.vector.Length() < ControllerState.AxisDeadzones[layout.flags])
                layout.vector *= 0.0f;

            layout.vector.Y *= -1;

            switch (MouseType)
            {
                case MouseActionsType.MoveBy:
                    {
                        if (layout.vector == Vector2.Zero)
                            return;

                        // apply sensivity
                        Vector = (layout.vector / short.MaxValue) * Sensivity;

                        MouseSimulator.MoveBy((int)Vector.X, (int)Vector.Y);
                    }
                    break;

                case MouseActionsType.MoveTo:
                    {
                        if (layout.vector == Vector2.Zero)
                        {
                            IsPressed = false;
                            prevVector = Vector2.Zero;
                            return;
                        }
                        else
                        {
                            // update entry point
                            if (!IsPressed)
                            {
                                prevVector = layout.vector;
                                entryMousePos = new Vector2(MouseSimulator.GetMousePosition().X, MouseSimulator.GetMousePosition().Y);
                                IsPressed = true;
                                return;
                            }

                            // get travel distance between ticks
                            Vector2 distance = (prevVector - layout.vector) / short.MaxValue;

                            Debug.WriteLine($"dist: {distance.Length()}");

                            // compute
                            Vector = entryMousePos + (layout.vector / short.MaxValue) * Sensivity * 10.0f * (1.0f + distance.Length());
                            MouseSimulator.MoveTo((int)Vector.X, (int)Vector.Y);

                            // update previous position
                            prevVector = layout.vector;
                        }
                    }
                    break;
            }
        }
    }
}
