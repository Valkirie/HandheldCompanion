using System;
using ControllerCommon.Actions;

namespace HandheldCompanion.Actions;

[Serializable]
public class EmptyActions : IActions
{
    public EmptyActions()
    {
        ActionType = ActionType.Disabled;
    }
}