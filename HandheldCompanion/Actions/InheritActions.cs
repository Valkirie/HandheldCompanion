using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public sealed class InheritActions : IActions
    {
        public InheritActions()
        {
            actionType = ActionType.Inherit;
        }
    }
}