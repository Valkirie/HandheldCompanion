using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
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
        Shift = 6,
        Inherit = 7,
    }

    [Serializable]
    public enum ActionState
    {
        Stopped = 0,
        Running = 1,
        Aborted = 2,
        Succeed = 3,
        Suspended = 4,
        Forced = 5,
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
        Long = 1, // hold for x ms and get an action
        Hold = 2, // press and hold the command for x ms
        Double = 3,
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

        public ActionType actionType = ActionType.Disabled;
        public PressType pressType = PressType.Short;
        public ActionState actionState = ActionState.Stopped;

        protected object Value;
        protected object prevValue;

        protected Vector2 Vector = new();
        protected Vector2 prevVector = new();

        public int ActionTimer = 200; // default value for steam
        public int pressTimer = -1; // -1 inactive, >= 0 active

        private int pressCount = 0; // used to store previous press value for double tap

        public bool HasTurbo = true;
        public bool HasToggle = true;
        public bool HasInterruptable = true;

        public bool Turbo;
        public int TurboDelay = 30;
        protected int TurboIdx;
        protected bool IsTurboed;

        public bool Toggle;
        protected bool IsToggled;

        public bool Interruptable = true;
        public ShiftSlot ShiftSlot = ShiftSlot.Any;

        public HapticMode HapticMode = HapticMode.Off;
        public HapticStrength HapticStrength = HapticStrength.Low;

        public MotionDirection motionDirection = MotionDirection.None;
        public float motionThreshold = 4000;

        public IActions()
        {
        }

        public virtual void SetHaptic(ButtonFlags button, bool up)
        {
            if (this.HapticMode == HapticMode.Off) return;
            if (this.HapticMode == HapticMode.Down && up) return;
            if (this.HapticMode == HapticMode.Up && !up) return;

            ControllerManager.GetTarget()?.SetHaptic(this.HapticStrength, button);
        }

        public virtual void Execute(AxisFlags axis, ShiftSlot shiftSlot)
        {
            // manage shift slot
            switch (ShiftSlot)
            {
                case ShiftSlot.None:
                    if (shiftSlot != ShiftSlot.None)
                        this.Value = (short)0;
                    break;

                case ShiftSlot.Any:
                    // do nothing
                    break;

                default:
                    if (!shiftSlot.HasFlag(ShiftSlot))
                        this.Value = (short)0;
                    break;
            }
        }

        public virtual void Execute(AxisLayout layout, ShiftSlot shiftSlot)
        {
            // manage shift slot
            switch (ShiftSlot)
            {
                case ShiftSlot.None:
                    if (shiftSlot != ShiftSlot.None)
                        this.Vector = Vector2.Zero;
                    break;

                case ShiftSlot.Any:
                    // do nothing
                    break;

                default:
                    if (!shiftSlot.HasFlag(ShiftSlot))
                        this.Vector = Vector2.Zero;
                    break;
            }
        }

        public virtual void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot = Actions.ShiftSlot.None)
        {
            if (actionState == ActionState.Suspended)
            {
                // bypass output
                this.Value = false;
                this.prevValue = value;
                return;
            }
            else if (actionState == ActionState.Forced)
            {
                // bypass output
                value = true;
            }

            switch (ShiftSlot)
            {
                case ShiftSlot.None:
                    if (shiftSlot != ShiftSlot.None)
                        value = false;
                    break;

                case ShiftSlot.Any:
                    // do nothing
                    break;

                default:
                    if (!shiftSlot.HasFlag(ShiftSlot))
                        value = false;
                    break;
            }

            switch (pressType)
            {
                case PressType.Long:
                    {
                        if (value)
                        {
                            // update state
                            actionState = ActionState.Running;

                            // update timer
                            pressTimer += TimerManager.GetPeriod();

                            if (pressTimer >= ActionTimer)
                            {
                                // update state
                                actionState = ActionState.Succeed;
                            }
                            else
                            {
                                // bypass output
                                this.Value = false;
                                this.prevValue = value;
                                return;
                            }
                        }
                        else
                        {
                            // key was released too early
                            if (actionState == ActionState.Running)
                            {
                                // update state
                                actionState = ActionState.Aborted;

                                // update timer
                                pressTimer = Math.Max(50, pressTimer);
                            }
                            else if (actionState == ActionState.Succeed)
                            {
                                // update state
                                actionState = ActionState.Stopped;

                                // update timer
                                pressTimer = -1;
                            }
                            else if (actionState == ActionState.Stopped)
                            {
                                // update timer
                                pressTimer = -1;
                            }

                            if (actionState == ActionState.Aborted)
                            {
                                // set to aborted for a time equal to the actions was "running"
                                if (pressTimer >= 0)
                                {
                                    // update state
                                    actionState = ActionState.Aborted;

                                    // update timer
                                    pressTimer -= TimerManager.GetPeriod();
                                }
                                else
                                {
                                    // update state
                                    actionState = ActionState.Stopped;

                                    // update timer
                                    pressTimer = -1;
                                }
                            }
                        }
                    }
                    break;

                case PressType.Hold:
                    {
                        if (value || (pressTimer <= ActionTimer && pressTimer >= 0))
                        {
                            // update state
                            actionState = ActionState.Running;

                            // update timer
                            pressTimer += TimerManager.GetPeriod();

                            // bypass output (simple)
                            value = true;
                        }
                        else if (pressTimer >= ActionTimer)
                        {
                            // update state
                            actionState = ActionState.Stopped;

                            // reset var(s)
                            pressTimer = -1;
                        }
                    }
                    break;

                case PressType.Double:
                    {
                        if (value)
                        {
                            // increase press count
                            if ((bool)prevValue != value)
                                pressCount++;
                        }

                        switch (pressCount)
                        {
                            default:
                                {
                                    if (actionState != ActionState.Stopped)
                                    {
                                        // update timer
                                        pressTimer += TimerManager.GetPeriod();

                                        if (pressTimer >= 50)
                                        {
                                            // update state
                                            actionState = ActionState.Stopped;

                                            // reset var(s)
                                            pressCount = 0;
                                            pressTimer = 0;
                                        }
                                    }

                                    // bypass output
                                    this.Value = false;
                                    this.prevValue = value;
                                    return;
                                }

                            case 1:
                                {
                                    // update state
                                    actionState = ActionState.Running;

                                    // update timer
                                    pressTimer += TimerManager.GetPeriod();

                                    // too slow to press again ?
                                    if (pressTimer > ActionTimer)
                                    {
                                        // update state
                                        actionState = ActionState.Aborted;

                                        // reset var(s)
                                        pressCount = 0;
                                        pressTimer = 0;
                                    }

                                    // bypass output
                                    this.Value = false;
                                    this.prevValue = value;
                                    return;
                                }

                            case 2:
                                {
                                    // on time
                                    if (pressTimer <= ActionTimer && value)
                                    {
                                        // update state
                                        actionState = ActionState.Succeed;

                                        // reset var(s)
                                        pressCount = 2;
                                        pressTimer = ActionTimer;
                                    }
                                    else
                                    {
                                        // update state
                                        actionState = ActionState.Stopped;

                                        // reset var(s)
                                        pressCount = 0;
                                        pressTimer = 0;
                                    }
                                }
                                break;
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

        public object Clone()
        {
            return CloningHelper.DeepClone(this);
        }
    }
}
