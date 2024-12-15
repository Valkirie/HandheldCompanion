using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class InheritActions : IActions
    {
        public InheritActions()
        {
            this.actionType = ActionType.Inherit;
        }
    }
}
