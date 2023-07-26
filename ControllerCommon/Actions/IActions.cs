using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using WindowsInput.Events;

namespace ControllerCommon.Actions;

[Serializable]
public enum ActionType
{
    Disabled = 0,
    Button = 1,
    Joystick = 2,
    Keyboard = 3,
    Mouse = 4,
    Trigger = 5
}

[Serializable]
public enum ModifierSet
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 3,
    ShiftControl = 4,
    ShiftAlt = 5,
    ControlAlt = 6,
    ShiftControlAlt = 7,
}

[Serializable]
public enum PressType
{
    Short = 0,
    Long = 1,
}

[Serializable]
public abstract class IActions : ICloneable
{
    public static Dictionary<ModifierSet, KeyCode[]> ModifierMap = new()
    {
        { ModifierSet.None,            new KeyCode[] { } },
        { ModifierSet.Shift,           new KeyCode[] { KeyCode.LShift } },
        { ModifierSet.Control,         new KeyCode[] { KeyCode.LControl } },
        { ModifierSet.Alt,             new KeyCode[] { KeyCode.LMenu } },
        { ModifierSet.ShiftControl,    new KeyCode[] { KeyCode.LShift, KeyCode.LControl } },
        { ModifierSet.ShiftAlt,        new KeyCode[] { KeyCode.LShift, KeyCode.LMenu } },
        { ModifierSet.ControlAlt,      new KeyCode[] { KeyCode.LControl, KeyCode.LMenu } },
        { ModifierSet.ShiftControlAlt, new KeyCode[] { KeyCode.LShift, KeyCode.LControl, KeyCode.LMenu } },
    };

    protected bool IsToggled;
    protected bool IsTurboed;

    protected ScreenOrientation Orientation = ScreenOrientation.Angle0;

    protected int Period;
    protected object prevValue;
    protected int TurboIdx;

    // TODO: make it configurable, multiple times, etc
    public PressType PressType = PressType.Short;
    public int LongPressTime = 450; // default value for steam
    protected int pressTimer = -1; // -1 inactive, >= 0 active

    protected object Value;

    public IActions()
    {
        Period = TimerManager.GetPeriod();
    }

    public ActionType ActionType { get; set; } = ActionType.Disabled;

    public bool Turbo { get; set; }
    public int TurboDelay { get; set; } = 30;

    public bool Toggle { get; set; }
    public bool AutoRotate { get; set; } = false;

    // Improve me !
    public object Clone()
    {
        return MemberwiseClone();
    }

    // if longDelay == 0 no new logic will be executed
    public virtual void Execute(ButtonFlags button, bool value, int longTime)
    {
        // reset failed attempts on button release
        if (pressTimer >= 0 && !value &&
            ((PressType == PressType.Short && pressTimer >= longTime) ||
             (PressType == PressType.Long && pressTimer < longTime)))
        {
            pressTimer = -1;
            prevValue = false;
            return;
        }

        // some long presses exist and button was just pressed, start the timer and quit
        if (longTime > 0 && value && !(bool)prevValue)
        {
            pressTimer = 0;
            prevValue = true;
            return;
        }

        if (pressTimer >= 0)
        {
            pressTimer += Period;

            // conditions were met to trigger either short or long, reset state, press buttons
            if ((!value && PressType == PressType.Short && pressTimer < longTime) ||
                (value && PressType == PressType.Long && pressTimer >= longTime))
            {
                pressTimer = -1;
                prevValue = false;  // simulate a situation where the button was just pressed
                value = true;       // prev = false, current = true, this way toggle works
            }
            // timer active, conditions not met, carry on, maybe smth happens, maybe failed attempt
            else
                return;
        }

        if (Toggle)
        {
            if ((bool)prevValue != value && value)
                IsToggled = !IsToggled;
        }
        else
            IsToggled = false;

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
            IsTurboed = false;

        // update previous value
        prevValue = value;

        // update value
        if (Toggle && Turbo)
            this.Value = IsToggled && IsTurboed;
        else if (Toggle)
            this.Value = IsToggled;
        else if (Turbo)
            this.Value = IsTurboed;
        else
            this.Value = value;
    }

    public virtual void SetOrientation(ScreenOrientation orientation)
    {
        Orientation = orientation;
    }
}