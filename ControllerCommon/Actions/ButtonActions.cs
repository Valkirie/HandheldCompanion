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

    public override void Execute(ButtonFlags button, bool value)
    {
        if (Toggle)
        {
            if ((bool)prevValue != value && value)
                IsToggled = !IsToggled;
        }
        else
        {
            IsToggled = false;
        }

        if (Turbo)
        {
            if (value || IsToggled)
            {
                if (TurboIdx % TurboDelay == 0)
                    IsTurboed = !IsTurboed;

                TurboIdx += Period;
            }
            else
            {
                IsTurboed = false;
                TurboIdx = 0;
            }
        }
        else
        {
            IsTurboed = false;
        }

        // update previous value
        prevValue = value;

        if (Toggle && Turbo)
            Value = IsToggled && IsTurboed;
        else if (Toggle)
            Value = IsToggled;
        else if (Turbo)
            Value = IsTurboed;
        else
            Value = value;
    }
}