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
        public MouseActionsType Type { get; set; }
        private short Value { get; set; }

        private bool IsCursorDown { get; set; } = false;
        private bool IsCursorUp { get; set; } = true;

        // settings
        public float Sensivity { get; set; } = 10.0f;

        public MouseActions()
        {
            this.ActionType = ActionType.Mouse;
        }

        public MouseActions(MouseActionsType type) : this()
        {
            this.Type = type;
        }

        public override bool Execute(ButtonFlags button, bool value)
        {
            switch (value)
            {
                case true:
                    {
                        if (IsCursorDown || !IsCursorUp)
                            return false;

                        IsCursorDown = true;
                        IsCursorUp = false;
                        MouseSimulator.MouseDown(Type);
                    }
                    break;
                case false:
                    {
                        if (IsCursorUp || !IsCursorDown)
                            return false;

                        IsCursorUp = true;
                        IsCursorDown = false;
                        MouseSimulator.MouseUp(Type);
                    }
                    break;
            }

            return true;
        }

        public override short Execute(AxisFlags axis, short value)
        {
            // update current value
            this.Value = value;

            switch (Type)
            {
                case MouseActionsType.MoveByX:
                    short x = (short)((float)Value / short.MaxValue * Sensivity);
                    MouseSimulator.MoveBy(x, 0);
                    break;
                case MouseActionsType.MoveByY:
                    short y = (short)((float)Value / short.MaxValue * Sensivity);
                    MouseSimulator.MoveBy(0, -y);
                    break;
            }

            return Value;
        }
    }
}
