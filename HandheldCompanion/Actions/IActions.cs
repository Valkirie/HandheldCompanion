using HandheldCompanion.Converters;
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
        Windows = 8,
    }

    [Serializable]
    public enum PressType
    {
        Short = 0,
        Long = 1, // hold for x ms then fire
        Hold = 2, // hold the action for x ms
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
            { ModifierSet.Shift,           new[] { KeyCode.LShift } },
            { ModifierSet.Control,         new[] { KeyCode.LControl } },
            { ModifierSet.Alt,             new[] { KeyCode.LMenu } },
            { ModifierSet.ShiftControl,    new[] { KeyCode.LShift, KeyCode.LControl } },
            { ModifierSet.ShiftAlt,        new[] { KeyCode.LShift, KeyCode.LMenu } },
            { ModifierSet.ControlAlt,      new[] { KeyCode.LControl, KeyCode.LMenu } },
            { ModifierSet.ShiftControlAlt, new[] { KeyCode.LShift, KeyCode.LControl, KeyCode.LMenu } },
            { ModifierSet.Windows,         new[] { KeyCode.LWin } },
        };

        // --- Core state ---
        public ActionType actionType = ActionType.Disabled;
        public PressType pressType = PressType.Short;
        public ActionState actionState = ActionState.Stopped;

        protected bool outBool;
        protected bool prevBool;
        protected Vector2 outVector = new();
        protected Vector2 prevVector = new();

        // --- Timing ---
        public float ActionTimer = 200.0f;  // base duration threshold (ms)
        public float PressTimer = -1.0f;   // -1 = inactive, ≥ 0 = counting

        // --- Features ---
        [JsonProperty("HasTurbo")]
        public bool HasTurbo = false;
        [JsonProperty("HasToggle")]
        public bool HasToggle = false;
        [JsonProperty("HasInterruptable")]
        public bool HasInterruptable = true;
        public float TurboDelay = 30.0f;

        // --- Start delay ---
        [JsonProperty("StartDelay")]
        public float StartDelay = 0.0f;
        [JsonIgnore] private float StartDelayTimer = -1.0f;  // -1 = inactive
        [JsonIgnore] private bool StartDelayRisingEdge = false;  // consumed by toggle logic

        // --- Toggle / Turbo runtime ---
        [JsonIgnore] private bool IsToggled = false;
        [JsonIgnore] private bool IsTurboed = false;
        private float TurboCountdown = 0.0f;

        // --- Double-tap counter ---
        private int PressCount = 0;

        // --- Shift gating ---
        [JsonConverter(typeof(ShiftSlotConverter))]
        public ShiftSlot ShiftSlot = ShiftSlot.Any;
        public bool ShiftMatchAny = false; // false = exact, true = OR (any selected shift)

        // --- Haptics ---
        public HapticMode HapticMode = HapticMode.Off;
        public HapticStrength HapticStrength = HapticStrength.Low;

        // --- Axis/motion ---
        public DeflectionDirection motionDirection = DeflectionDirection.None;
        public float motionThreshold = 4000;
        protected bool axisSlotDisabled;

        // --- Legacy save compatibility ---
        #region legacy
        [JsonProperty("IsTurbo")] private bool Legacy_IsTurbo { set => HasTurbo = value; }
        [JsonProperty("IsToggle")] private bool Legacy_IsToggle { set => HasToggle = value; }
        #endregion

        public IActions() { }

        /// <summary>
        /// Override to share toggle state across bindings targeting the same key/button,
        /// and to detect external releases. Default uses local toggle state.
        /// </summary>
        protected virtual (bool useShared, bool toggleState) GetSharedToggleState(bool risingEdge) => (false, false);

        public virtual void SetHaptic(ButtonFlags button, bool released)
        {
            if (HapticMode == HapticMode.Off) return;
            if (HapticMode == HapticMode.Down && released) return;
            if (HapticMode == HapticMode.Up && !released) return;

            ControllerManager.GetTarget()?.SetHaptic(HapticStrength, button);
        }

        /// <summary>AxisFlags version: computes shift-slot gating only.</summary>
        public virtual void Execute(AxisFlags axis, ShiftSlot shiftSlot, float delta)
        {
            axisSlotDisabled = !IsShiftAllowed(shiftSlot, ShiftSlot, ShiftMatchAny);
        }

        /// <summary>AxisLayout version: zeroes the vector when the slot is masked.</summary>
        public virtual void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            if (!IsShiftAllowed(shiftSlot, ShiftSlot, ShiftMatchAny))
                outVector = Vector2.Zero;
        }

        public virtual void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            // Suspended: block output without consuming edge state
            if (actionState == ActionState.Suspended)
            {
                outBool = false;
                prevBool = value;
                return;
            }

            // Forced: override input to pressed
            if (actionState == ActionState.Forced)
                value = true;

            // Shift gating
            if (!IsShiftAllowed(shiftSlot, ShiftSlot, ShiftMatchAny))
                value = false;

            // Start delay — may gate the action for a period before firing
            if (!ProcessStartDelay(ref value, delta))
                return;

            // Press-type modifiers (Long / Hold / Double)
            if (!ProcessPressType(ref value, delta))
                return;

            // Toggle and turbo modifiers
            ProcessToggle(value);
            ProcessTurbo(value, delta);

            // Compose final output from active modifiers
            outBool = (HasToggle, HasTurbo) switch
            {
                (true, true) => IsToggled && IsTurboed,
                (true, false) => IsToggled,
                (false, true) => IsTurboed,
                _ => value,
            };

            prevBool = value;
        }

        /// <summary>
        /// Returns false (and suppresses output) while waiting for the start delay.
        /// Modifies <paramref name="value"/> to true once the delay elapses.
        /// </summary>
        private bool ProcessStartDelay(ref bool value, float delta)
        {
            if (StartDelay <= 0) return true;

            // TimerManager has a minimum 10 ms tick; if StartDelay is shorter, add one tick period.
            int period = TimerManager.GetPeriod();
            float effectiveDelay = StartDelay < period ? period + StartDelay : StartDelay;

            // Begin countdown on rising edge
            if (value && !prevBool)
            {
                StartDelayTimer = 0f;
                StartDelayRisingEdge = true;
            }

            // Still waiting
            if (StartDelayTimer >= 0 && StartDelayTimer < effectiveDelay)
            {
                StartDelayTimer += delta;
                outBool = false;
                return false;   // suppress — do not update prevBool
            }

            // Delay elapsed: fire and reset
            if (StartDelayTimer >= effectiveDelay)
            {
                StartDelayTimer = -1f;
                value = true;
            }

            return true;
        }

        /// <summary>
        /// Applies press-type modifiers. Returns false when the action should be suppressed
        /// (e.g. still waiting for Long/Double threshold).
        /// </summary>
        private bool ProcessPressType(ref bool value, float delta)
        {
            return pressType switch
            {
                PressType.Long => ProcessLongPress(ref value, delta),
                PressType.Hold => ProcessHoldPress(ref value, delta),
                PressType.Double => ProcessDoublePress(ref value, delta),
                _ => true,
            };
        }

        /// <summary>
        /// Suppresses output until the button has been held for <see cref="ActionTimer"/> ms.
        /// If released early the state is Aborted and decays back over the same duration.
        /// </summary>
        private bool ProcessLongPress(ref bool value, float delta)
        {
            if (value)
            {
                actionState = ActionState.Running;
                PressTimer += delta;

                if (PressTimer < ActionTimer)
                {
                    outBool = false;
                    prevBool = value;
                    return false;   // still accumulating — suppress
                }

                actionState = ActionState.Succeed;
            }
            else
            {
                switch (actionState)
                {
                    case ActionState.Running:
                        actionState = ActionState.Aborted;
                        PressTimer = Math.Max(50, PressTimer);
                        break;

                    case ActionState.Succeed:
                        actionState = ActionState.Stopped;
                        PressTimer = -1;
                        break;

                    case ActionState.Stopped:
                        PressTimer = -1;
                        break;
                }

                if (actionState == ActionState.Aborted)
                {
                    if (PressTimer >= 0) PressTimer -= delta;
                    else { actionState = ActionState.Stopped; PressTimer = -1; }
                }
            }

            return true;
        }

        /// <summary>
        /// Keeps the action active for <see cref="ActionTimer"/> ms even after the button
        /// is released (fire-and-hold pattern).
        /// </summary>
        private bool ProcessHoldPress(ref bool value, float delta)
        {
            bool timerActive = PressTimer >= 0 && PressTimer <= ActionTimer;

            if (value || timerActive)
            {
                actionState = ActionState.Running;
                PressTimer += delta;
                value = true;    // keep output active while timer runs
            }
            else if (PressTimer > ActionTimer)
            {
                actionState = ActionState.Stopped;
                PressTimer = -1;
            }

            return true;
        }

        /// <summary>
        /// Fires only when the button is pressed twice within <see cref="ActionTimer"/> ms.
        /// Returns false while waiting for the second tap.
        /// </summary>
        private bool ProcessDoublePress(ref bool value, float delta)
        {
            // Count rising edges
            if (prevBool != value && value)
                PressCount++;

            switch (PressCount)
            {
                // No tap in progress — tick decay timer so the state resets cleanly
                default:
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
                    return false;

                // First tap: wait for second tap within window
                case 1:
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
                    return false;

                // Second tap within window: succeed; otherwise reset
                case 2:
                    if (PressTimer <= ActionTimer && value)
                    {
                        actionState = ActionState.Succeed;
                        PressTimer = ActionTimer;
                    }
                    else
                    {
                        actionState = ActionState.Stopped;
                        PressCount = 0;
                        PressTimer = 0;
                    }
                    return true;
            }
        }

        private void ProcessToggle(bool value)
        {
            if (!HasToggle)
            {
                StartDelayRisingEdge = false;
                IsToggled = false;
                return;
            }

            // A rising edge is either a fresh button press, or a delayed-start rising edge
            bool risingEdge = (prevBool != value && value) || StartDelayRisingEdge;
            StartDelayRisingEdge = false;   // consume

            var (useShared, sharedState) = GetSharedToggleState(risingEdge);
            if (useShared)
                IsToggled = sharedState;
            else if (risingEdge)
                IsToggled = !IsToggled;
        }

        private void ProcessTurbo(bool value, float delta)
        {
            if (!HasTurbo)
            {
                IsTurboed = false;
                return;
            }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool IsShiftAllowed(ShiftSlot current, ShiftSlot required, bool matchAny)
        {
            // Any flag → always enabled
            if (required.HasFlag(ShiftSlot.Any)) return true;

            // None → only when no shift is active
            if (required == ShiftSlot.None) return current == ShiftSlot.None;

            // OR mode: at least one required shift must be active
            if (matchAny)
            {
                ShiftSlot requiredWithoutAny = required & ~ShiftSlot.Any;
                return (current & requiredWithoutAny) != ShiftSlot.None;
            }

            // Strict mode: exact match
            return current == required;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool DirectionMatches(DeflectionDirection direction, DeflectionDirection mask) => direction != DeflectionDirection.None && (direction & mask) != 0;

        //  Cloning
        public object Clone() => CloningHelper.DeepClone(this);
    }
}
