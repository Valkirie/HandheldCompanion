using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;
using System.Diagnostics;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum ButtonModes
    {
        Default = 0,
        Toggle = 1,
        Repeat = 2
    }

    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button { get; set; }
        public ButtonModes Mode { get; set; }

        private bool Value;
        private bool prevValue;

        private short RepeatIdx;

        public ButtonActions()
        {
            this.ActionType = ActionType.Button;
        }

        public ButtonActions(ButtonFlags button) : this()
        {
            this.Button = button;
        }

        public override bool Execute(ButtonFlags button, bool value)
        {
            switch(Mode)
            {
                case ButtonModes.Default:
                    this.Value = value;
                    break;
                case ButtonModes.Toggle:
                    if (prevValue != value && value)
                        this.Value = !this.Value;
                    break;
                case ButtonModes.Repeat:
                    {
                        if (value)
                        {
                            if (RepeatIdx % 20 == 0)
                                this.Value = !this.Value;
                            RepeatIdx++;
                        }
                        else
                        {
                            this.Value = false;
                            RepeatIdx = 0;
                        }
                    }
                    break;
            }

            // update previous value
            prevValue = value;

            return Value;
        }
    }
}
