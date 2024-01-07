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

        public override void Execute(ButtonFlags button, bool value)
        {
            base.Execute(button, value);

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
