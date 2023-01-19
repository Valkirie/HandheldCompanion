using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button { get; set; }

        public ButtonActions()
        {
            this.ActionType = ActionType.Button;
        }

        public ButtonActions(ButtonFlags button) : this()
        {
            this.Button = button;
        }
    }
}
