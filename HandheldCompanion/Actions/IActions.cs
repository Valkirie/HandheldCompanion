using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
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
        public static readonly Dictionary<ModifierSet, KeyCode[]> ModifierMap = new()
        {
            { ModifierSet.None,            Array.Empty<KeyCode>() },
            { ModifierSet.Shift,           new [] { KeyCode.LShift } },
            { ModifierSet.Control,         new [] { KeyCode.LControl } },
            { ModifierSet.Alt,             new [] { KeyCode.LMenu } },
            { ModifierSet.ShiftControl,    new [] { KeyCode.LShift, KeyCode.LControl } },
            { ModifierSet.ShiftAlt,        new [] { KeyCode.LShift, KeyCode.LMenu } },
            { ModifierSet.ControlAlt,      new [] { KeyCode.LControl, KeyCode.LMenu } },
            { ModifierSet.ShiftControlAlt, new [] { KeyCode.LShift, KeyCode.LControl, KeyCode.LMenu } },
        };

        public ActionType actionType = ActionType.Disabled;
        public PressType pressType = PressType.Short;
        public ActionState actionState = ActionState.Stopped;

        // Replaces boxed Value/prevValue
        protected bool outBool;      // current output for button-like actions
        protected bool prevBool;     // last input state for edge detection

        protected Vector2 outVector = new();
        protected Vector2 prevVector = new();

        public float ActionTimer = 200.0f;   // default value for steam
        public float PressTimer = -1.0f;     // -1 inactive, >= 0 active

        [JsonProperty("HasTurbo")]
        public bool HasTurbo = false;
        [JsonProperty("HasToggle")]
        public bool HasToggle = false;
        [JsonProperty("HasInterruptable")]
        public bool HasInterruptable = true;
        public float TurboDelay = 30.0f;

        [JsonIgnore]
        private bool IsToggled = false;
        [JsonIgnore]
        private bool IsTurboed = false;
        private float TurboCountdown = 0.0f;     // countdown (ms) before flipping

        #region legacy
        // legacy aliases: read old saves, map to new fields
        [JsonProperty("IsTurbo")]
        private bool Legacy_IsTurbo { set { HasTurbo = value; } }
        [JsonProperty("IsToggle")]
        private bool Legacy_IsToggle { set { HasToggle = value; } }
        #endregion

        private int PressCount = 0;     // used for double tap

        public ShiftSlot ShiftSlot = ShiftSlot.Any;

        public HapticMode HapticMode = HapticMode.Off;
        public HapticStrength HapticStrength = HapticStrength.Low;

        public DeflectionDirection motionDirection = DeflectionDirection.None;
        public float motionThreshold = 4000;

        // Axis-only shift mask flag to avoid sentinel assignments
        protected bool axisSlotDisabled;

        public IActions() { }

        /// <summary>
        /// Returns the actual output state tracked by derived classes or shared simulators.
        /// Used for toggle desync detection when external actions modify the output.
        /// </summary>
        protected virtual bool GetActualOutputState() => outBool;

        public virtual void SetHaptic(ButtonFlags button, bool released)
        {
            if (HapticMode == HapticMode.Off) return;
            if (HapticMode == HapticMode.Down && released) return;
            if (HapticMode == HapticMode.Up && !released) return;

            ControllerManager.GetTarget()?.SetHaptic(HapticStrength, button);
        }

        // AxisFlags version: just compute shift-slot gating (no work)
        public virtual void Execute(AxisFlags axis, ShiftSlot shiftSlot, float delta)
        {
            axisSlotDisabled = !IsShiftAllowed(shiftSlot, ShiftSlot);
        }

        // AxisLayout version: zero vector when masked to skip downstream work
        public virtual void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            if (!IsShiftAllowed(shiftSlot, ShiftSlot))
                outVector = Vector2.Zero;
        }

        public virtual void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            if (actionState == ActionState.Suspended)
            {
                outBool = false;
                prevBool = value;
                return;
            }
            else if (actionState == ActionState.Forced)
            {
                value = true;
            }

            // shift gating
            if (!IsShiftAllowed(shiftSlot, ShiftSlot))
                value = false;

            switch (pressType)
            {
                case PressType.Long:
                    {
                        if (value)
                        {
                            actionState = ActionState.Running;
                            PressTimer += delta;

                            if (PressTimer >= ActionTimer)
                            {
                                actionState = ActionState.Succeed;
                            }
                            else
                            {
                                outBool = false;
                                prevBool = value;
                                return;
                            }
                        }
                        else
                        {
                            if (actionState == ActionState.Running)
                            {
                                actionState = ActionState.Aborted;
                                PressTimer = Math.Max(50, PressTimer);
                            }
                            else if (actionState == ActionState.Succeed || actionState == ActionState.Stopped)
                            {
                                actionState = (actionState == ActionState.Succeed) ? ActionState.Stopped : actionState;
                                PressTimer = -1;
                            }

                            if (actionState == ActionState.Aborted)
                            {
                                // keep Aborted for the time it was "running"
                                if (PressTimer >= 0) PressTimer -= delta;
                                else { actionState = ActionState.Stopped; PressTimer = -1; }
                            }
                        }
                        break;
                    }

                case PressType.Hold:
                    {
                        if (value || (PressTimer <= ActionTimer && PressTimer >= 0))
                        {
                            actionState = ActionState.Running;
                            PressTimer += delta;
                            value = true; // simple bypass
                        }
                        else if (PressTimer >= ActionTimer)
                        {
                            actionState = ActionState.Stopped;
                            PressTimer = -1;
                        }
                        break;
                    }

                case PressType.Double:
                    {
                        if (prevBool != value && value)
                            PressCount++;

                        switch (PressCount)
                        {
                            default:
                                {
                                    if (actionState != ActionState.Stopped)
                                    {
                                        PressTimer += delta;
                                        if (PressTimer >= 50)
                                        {
                                            actionState = ActionState.Stopped;
                                            PressCount = 0;
                                            PressTimer = 0;
                                        }
                                    }
                                    outBool = false;
                                    prevBool = value;
                                    return;
                                }

                            case 1:
                                {
                                    actionState = ActionState.Running;
                                    PressTimer += delta;

                                    if (PressTimer > ActionTimer)
                                    {
                                        actionState = ActionState.Aborted;
                                        PressCount = 0;
                                        PressTimer = 0;
                                    }
                                    outBool = false;
                                    prevBool = value;
                                    return;
                                }

                            case 2:
                                {
                                    if (PressTimer <= ActionTimer && value)
                                    {
                                        actionState = ActionState.Succeed;
                                        PressCount = 2;
                                        PressTimer = ActionTimer;
                                    }
                                    else
                                    {
                                        actionState = ActionState.Stopped;
                                        PressCount = 0;
                                        PressTimer = 0;
                                    }
                                    break;
                                }
                        }
                        break;
                    }
            }

            // Toggle
            if (HasToggle)
            {
                // Detect desync: toggle thinks it's ON but actual output is OFF (externally released)
                bool actualState = GetActualOutputState();
                if (IsToggled && !actualState)
                {
                    IsToggled = false; // Reset toggle to match reality
                }

                if (prevBool != value && value) IsToggled = !IsToggled;
            }
            else
            {
                IsToggled = false;
            }

            // Turbo (countdown, no modulo)
            if (HasTurbo)
            {
                if (value || IsToggled)
                {
                    TurboCountdown -= delta;
                    if (TurboCountdown <= 0)
                    {
                        IsTurboed = !IsTurboed;
                        TurboCountdown += Math.Max(1, TurboDelay);
                    }
                }
                else
                {
                    IsTurboed = false;
                    TurboCountdown = TurboDelay;
                }
            }
            else
            {
                IsTurboed = false;
            }

            // final outBool
            if (HasToggle && HasTurbo) outBool = IsToggled && IsTurboed;
            else if (HasToggle) outBool = IsToggled;
            else if (HasTurbo) outBool = IsTurboed;
            else outBool = value;

            prevBool = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool IsShiftAllowed(ShiftSlot current, ShiftSlot required)
        {
            switch (required)
            {
                case ShiftSlot.None: return current == ShiftSlot.None;
                case ShiftSlot.Any: return true;
                default: return (current & required) != 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool DirectionMatches(DeflectionDirection direction, DeflectionDirection mask)
        {
            return direction != DeflectionDirection.None && ((direction & mask) != 0);
        }

        public object Clone() => CloningHelper.DeepClone(this);
    }
}