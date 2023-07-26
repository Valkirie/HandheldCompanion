using System;
using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using WindowsInput.Events;

namespace HandheldCompanion.Actions;

[Serializable]
public class KeyboardActions : IActions
{
    public KeyboardActions()
    {
        ActionType = ActionType.Keyboard;
        IsKeyDown = false;

        Value = false;
        prevValue = false;
    }

    public KeyboardActions(VirtualKeyCode key) : this()
    {
        Key = key;
    }

    public VirtualKeyCode Key { get; set; }
    private bool IsKeyDown { get; set; }
    private KeyCode[] pressed;

    // settings
    public ModifierSet Modifiers = ModifierSet.None;

    public override void Execute(ButtonFlags button, bool value, int longTime)
    {
        base.Execute(button, value, longTime);

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

        // update value
        if (Toggle && Turbo)
            Value = IsToggled && IsTurboed;
        else if (Toggle)
            Value = IsToggled;
        else if (Turbo)
            Value = IsTurboed;
        else
            Value = value;

        switch (Value)
        {
            case true:
            {
                if (IsKeyDown)
                    return;

                IsKeyDown = true;
                pressed = ModifierMap[Modifiers];
                KeyboardSimulator.KeyDown(pressed);
                KeyboardSimulator.KeyDown(Key);
            }
                break;
            case false:
            {
                if (!IsKeyDown)
                    return;

                IsKeyDown = false;
                KeyboardSimulator.KeyUp(Key);
                KeyboardSimulator.KeyUp(pressed);
            }
                break;
        }
    }
}