using System;
using ControllerCommon.Inputs;

namespace ControllerCommon.Actions;

[Serializable]
public class ButtonActions : IActions
{
    public ButtonActions()
    {
        ActionType = ActionType.Button;

        Value = false;
        prevValue = false;
    }

    public ButtonActions(ButtonFlags button) : this()
    {
        Button = button;
    }

    public ButtonFlags Button { get; set; }

    public bool GetValue()
    {
        return (bool)Value;
    }
}