using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WindowsInput.Events;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum ActionType
    {
        Disabled = 0,
        Button = 1,
        Joystick = 2,
        Keyboard = 3,
        Mouse = 4,
        Trigger = 5,
        Special = 6,
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
    public enum HapticMode
    {
        Off = 0,
        Down = 1,
        Up = 2,
        Both = 3,
    }

    [Serializable]
    public enum HapticStrength
    {
        Low = 0,
        Medium = 1,
        High = 2,
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

        public ActionType ActionType = ActionType.Disabled;

        protected object Value;
        protected object prevValue;

        // values below are common for button type actions

        protected int Period;

        // TODO: multiple delay, delay ranges
        public PressType PressType = PressType.Short;
        public int LongPressTime = 450; // default value for steam
        protected int pressTimer = -1; // -1 inactive, >= 0 active

        public bool Turbo;
        public int TurboDelay = 30;
        protected int TurboIdx;
        protected bool IsTurboed;

        public bool Toggle;
        protected bool IsToggled;

        public HapticMode HapticMode = HapticMode.Off;
        public HapticStrength HapticStrength = HapticStrength.Low;

        protected ScreenOrientation Orientation = ScreenOrientation.Angle0;
        public bool AutoRotate { get; set; } = false;

        public IActions()
        {
            Period = TimerManager.GetPeriod();
        }

        public virtual void SetHaptic(ButtonFlags button, bool up)
        {
            if (this.HapticMode == HapticMode.Off) return;
            if (this.HapticMode == HapticMode.Down && up) return;
            if (this.HapticMode == HapticMode.Up && !up) return;

            ControllerManager.GetTargetController()?.SetHaptic(this.HapticStrength, button);
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

        // Improve me !
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
