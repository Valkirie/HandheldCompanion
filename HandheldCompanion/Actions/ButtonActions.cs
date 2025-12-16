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

        protected override bool GetActualOutputState() => IsKeyDown;

        public ButtonActions()
        {
            actionType = ActionType.Button;
            outBool = false;
            prevBool = false;
        }

        public ButtonActions(ButtonFlags button) : this()
        {
            Button = button;
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            if (outBool)
            {
                if (IsKeyDown) return;
                IsKeyDown = true;
                SetHaptic(button, false);
            }
            else
            {
                if (!IsKeyDown) return;
                IsKeyDown = false;
                SetHaptic(button, true);
            }
        }

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero)
            {
                Execute(Button, false, shiftSlot, delta);
                return;
            }

            var direction = InputUtils.GetDeflectionDirection(outVector, motionThreshold);
            bool press = DirectionMatches(direction, motionDirection);

            // transition to Button Execute()
            Execute(Button, press, shiftSlot, delta);
        }

        public bool GetValue() => outBool;
    }
}