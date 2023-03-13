using ControllerCommon.Actions;
using System;

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
