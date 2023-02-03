using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;
using System.Diagnostics;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button { get; set; }

        public bool Turbo { get; set; }
        public byte TurboDelay { get; set; } = 90;
        private short TurboIdx;
        private bool IsTurboed;

        public bool Toggle { get; set; }
        private bool IsToggled;

        public ButtonActions()
        {
            this.ActionType = ActionType.Button;
            this.Value = (bool)false;
        }

        public ButtonActions(ButtonFlags button) : this()
        {
            this.Button = button;
        }

        public bool GetValue()
        {
            return (bool)this.Value;
        }

        public override void Execute(ButtonFlags button, bool value)
        {
            if (Toggle)
            {
                if ((bool)prevValue != value && value)
                    IsToggled = !IsToggled;
            }

            if (Turbo)
            {
                if (value || IsToggled)
                {
                    if (TurboIdx % TurboDelay == 0)
                        IsTurboed = !IsTurboed;
                    TurboIdx+=5;
                }
                else
                {
                    IsTurboed = false;
                    TurboIdx = 0;
                }
            }

            if (Toggle && Turbo)
                this.Value = IsToggled && IsTurboed;
            else if (Toggle)
                this.Value = IsToggled;
            else if (Turbo)
                this.Value = IsTurboed;
            else
                this.Value = value;

            // update previous value
            prevValue = value;
        }
    }
}
