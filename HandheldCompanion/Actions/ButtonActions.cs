using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Numerics;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button;

        // runtime variables
        private bool IsKeyDown = false;

        public ButtonActions()
        {
            this.actionType = ActionType.Button;

            this.Value = false;
            this.prevValue = false;
        }

        public ButtonActions(ButtonFlags button) : this()
        {
            this.Button = button;
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot)
        {
            // call parent, check shiftSlot
            base.Execute(button, value, shiftSlot);

            switch (this.Value)
            {
                case true:
                    {
                        if (IsKeyDown)
                            return;

                        IsKeyDown = true;
                        SetHaptic(button, false);
                    }
                    break;
                case false:
                    {
                        if (!IsKeyDown)
                            return;

                        IsKeyDown = false;
                        SetHaptic(button, true);
                    }
                    break;
            }
        }

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot)
        {
            // update value
            this.Vector = layout.vector;

            // call parent, check shiftSlot
            base.Execute(layout, shiftSlot);

            // skip if zero and button wasn't pressed
            if (this.Vector == Vector2.Zero && (bool)this.Value == false)
                return;

            MotionDirection direction = InputUtils.GetMotionDirection(this.Vector, motionThreshold);
            bool value = (direction.HasFlag(motionDirection) || motionDirection.HasFlag(direction)) && direction != MotionDirection.None;

            // transition to Button Execute()
            Execute(this.Button, value, shiftSlot);
        }

        public bool GetValue()
        {
            return (bool)this.Value;
        }
    }
}
