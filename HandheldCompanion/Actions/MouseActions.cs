using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using HandheldCompanion.Simulators;
using System;
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
        public float Sensivity { get; set; } = 10.0f;

        public MouseActions()
        {
            this.ActionType = ActionType.Mouse;
            this.IsCursorDown = false;
            this.IsCursorUp = true;
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
                    TurboIdx += 5;
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
            // update current value
            this.Value = value;

            switch (MouseType)
            {
                case MouseActionsType.MoveByX:
                    short x = (short)((float)value / short.MaxValue * Sensivity);
                    MouseSimulator.MoveBy(x, 0);
                    break;
                case MouseActionsType.MoveByY:
                    short y = (short)((float)value / short.MaxValue * Sensivity);
                    MouseSimulator.MoveBy(0, -y);
                    break;
            }
        }
    }
}
