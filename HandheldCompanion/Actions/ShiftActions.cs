using HandheldCompanion.Inputs;
using System;
using System.ComponentModel;

namespace HandheldCompanion.Actions
{
    [Serializable]
    [Flags]
    public enum ShiftSlot
    {
        [Description("Disabled on shift")] None = 0,
        [Description("Shift A")] ShiftA = 1 << 0,
        [Description("Shift B")] ShiftB = 1 << 1,
        [Description("Shift C")] ShiftC = 1 << 2,
        [Description("Shift D")] ShiftD = 1 << 3,
        [Description("Always enabled")] Any = 1 << 7,
    }

    [Serializable]
    public sealed class ShiftActions : ButtonActions
    {
        // Which shift slot this button activates — distinct from IActions.ShiftSlot (execution gating).
        // IActions.ShiftSlot intentionally stays as Any so ShiftActions always execute regardless
        // of the current shift state (they are the buttons that ACTIVATE shifts).
        public ShiftSlot ActivationSlot = ShiftSlot.Any;

        public ShiftActions()
        {
            actionType = ActionType.Shift;

            // Shift keys are not interruptable, toggleable, or turboable
            HasInterruptable = false;
            HasTurbo = false;
            HasToggle = false;

            outBool = false;
            prevBool = false;
        }

        public ShiftActions(ShiftSlot slot) : this()
        {
            ActivationSlot = slot;
        }
    }
}
