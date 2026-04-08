using System;

namespace HandheldCompanion.Actions
{
    /// <summary>
    /// Sentinel action: instructs the layout system to inherit the mapping from the default layout.
    /// </summary>
    [Serializable]
    public sealed class InheritActions : IActions
    {
        public InheritActions()
        {
            actionType = ActionType.Inherit;
        }
    }
}