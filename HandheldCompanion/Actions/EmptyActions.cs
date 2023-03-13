using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class EmptyActions : IActions
    {
        public EmptyActions()
        {
            this.ActionType = ActionType.None;
        }
    }
}
