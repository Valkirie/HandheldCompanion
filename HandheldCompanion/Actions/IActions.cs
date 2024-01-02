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
        Hold = 2,
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
        }

        public virtual void SetHaptic(ButtonFlags button, bool up)
        {
            if (this.HapticMode == HapticMode.Off) return;
            if (this.HapticMode == HapticMode.Down && up) return;
            if (this.HapticMode == HapticMode.Up && !up) return;

            ControllerManager.GetTargetController()?.SetHaptic(this.HapticStrength, button);
        }

        public virtual void Execute(ButtonFlags button, bool value)
        {
            switch(PressType)
            {
                case PressType.Long:
                    {
                        if (value || (pressTimer <= LongPressTime && pressTimer >= 0))
                        {
                            pressTimer += TimerManager.GetPeriod();
                            value = true;
                        }
                        else if(pressTimer >= LongPressTime)
                        {
                            pressTimer = -1;
                        }
                    }
                    break;

                case PressType.Hold:
                    {
                        if (value)
                        {
                            pressTimer += TimerManager.GetPeriod();
                            if (pressTimer >= LongPressTime)
                            {
                                // do something
                            }
                            else
                            {
                                value = false;
                            }
                        }
                        else
                        {
                            pressTimer = -1;
                        }
                    }
                    break;
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

                    TurboIdx += TimerManager.GetPeriod();
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
