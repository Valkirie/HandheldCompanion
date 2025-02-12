using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using System;
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

        // override Value
        public new bool Value = false;
        public new bool prevValue = false;

        public KeyboardActions()
        {
            this.actionType = ActionType.Keyboard;
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
    }
}
