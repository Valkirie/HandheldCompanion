using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class KeyboardActions : IActions
    {
        public VirtualKeyCode Key { get; set; }
        private bool IsKeyDown { get; set; } = false;
        private bool IsKeyUp { get; set; } = true;

        public KeyboardActions()
        {
            this.ActionType = ActionType.Keyboard;
        }

        public KeyboardActions(VirtualKeyCode key) : this()
        {
            this.Key = key;
        }

        public override void Execute(ButtonFlags button, bool value)
        {
            // update current value
            this.Value = value;

            switch (value)
            {
                case true:
                    {
                        if (IsKeyDown || !IsKeyUp)
                            return;

                        IsKeyDown = true;
                        IsKeyUp = false;
                        KeyboardSimulator.KeyDown(Key);
                    }
                    break;
                case false:
                    {
                        if (IsKeyUp || !IsKeyDown)
                            return;

                        IsKeyUp = true;
                        IsKeyDown = false;
                        KeyboardSimulator.KeyUp(Key);
                    }
                    break;
            }
        }
    }
}
