using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button;

        // Runtime: tracks whether the virtual button is currently held down
        private bool isKeyDown = false;

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
                if (isKeyDown) return;
                isKeyDown = true;
                SetHaptic(button, released: false);
            }
            else
            {
                if (!isKeyDown) return;
                isKeyDown = false;
                SetHaptic(button, released: true);
            }
        }

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero)
            {
                Execute(Button, value: false, shiftSlot, delta);
                return;
            }

            var direction = InputUtils.GetDeflectionDirection(outVector, motionThreshold);
            bool press = DirectionMatches(direction, motionDirection);

            Execute(Button, press, shiftSlot, delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetValue() => outBool;
    }
}