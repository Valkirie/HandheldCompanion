using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class ButtonActions : IActions
    {
        public ButtonFlags Button { get; }

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
