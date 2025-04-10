using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using HandheldCompanion.Utils;
using System;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class KeyboardActions : IActions
    {
        public VirtualKeyCode Key;

        // runtime variables
        private bool IsKeyDown = false;
        private KeyCode[] pressed;

        // settings
        public ModifierSet Modifiers = ModifierSet.None;

        public KeyboardActions()
        {
            this.actionType = ActionType.Keyboard;

            this.Value = false;
            this.prevValue = false;
        }

        public KeyboardActions(VirtualKeyCode key) : this()
        {
            this.Key = key;
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot)
        {
            base.Execute(button, value, shiftSlot);

            switch (this.Value)
            {
                case true:
                    {
                        if (IsKeyDown)
                            return;

                        IsKeyDown = true;
                        pressed = ModifierMap[Modifiers];
                        KeyboardSimulator.KeyDown(pressed);
                        KeyboardSimulator.KeyDown(Key);
                        SetHaptic(button, false);
                    }
                    break;
                case false:
                    {
                        if (!IsKeyDown)
                            return;

                        IsKeyDown = false;
                        KeyboardSimulator.KeyUp(Key);
                        KeyboardSimulator.KeyUp(pressed);
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

            // skip if zero
            if (this.Vector == Vector2.Zero && !IsKeyDown)
                return;

            MotionDirection direction = InputUtils.GetMotionDirection(this.Vector, motionThreshold);
            bool value = (direction.HasFlag(motionDirection) || motionDirection.HasFlag(direction)) && direction != MotionDirection.None;

            // transition to Button Execute()
            Execute(ButtonFlags.None, value, shiftSlot);
        }
    }
}
