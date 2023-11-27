using HandheldCompanion.Inputs;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button;

        // runtime variables
        private bool IsKeyDown = false;

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
                        SetHaptic(button, false);
                    }
                    break;
                case false:
                    {
                        if (!IsKeyDown)
                            return;

                        IsKeyDown = false;
                        SetHaptic(button, true);
                    }
                    break;
            }
        }

        public bool GetValue()
        {
            return (bool)this.Value;
        }
    }
}
