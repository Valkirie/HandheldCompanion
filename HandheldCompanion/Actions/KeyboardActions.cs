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
        private bool IsKeyDown { get; set; }
        private bool IsKeyUp { get; set; }

        public KeyboardActions()
        {
            this.ActionType = ActionType.Keyboard;
            this.IsKeyDown = false;
            this.IsKeyUp = true;
        }

        public KeyboardActions(VirtualKeyCode key) : this()
        {
            this.Key = key;
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

                    TurboIdx += Period;
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
