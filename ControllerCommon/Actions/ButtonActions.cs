using ControllerCommon.Inputs;
using System;

namespace ControllerCommon.Actions
{
    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button { get; set; }

        public ButtonActions()
        {
            this.ActionType = ActionType.Button;

            this.Value = false;
            this.prevValue = false;
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

            if (Toggle && Turbo)
                this.Value = IsToggled && IsTurboed;
            else if (Toggle)
                this.Value = IsToggled;
            else if (Turbo)
                this.Value = IsTurboed;
            else
                this.Value = value;
        }
    }
}
