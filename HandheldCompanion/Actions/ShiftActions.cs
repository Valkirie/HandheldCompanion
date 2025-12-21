using System;
using System.ComponentModel;

namespace HandheldCompanion.Actions
{
    [Serializable]
    [Flags]
    public enum ShiftSlot
    {
        [Description("Disabled on shift")]
        None = 0,
        [Description("Shift A")]
        ShiftA = 1 << 0,
        [Description("Shift B")]
        ShiftB = 1 << 1,
        [Description("Shift C")]
        ShiftC = 1 << 2,
        [Description("Shift D")]
        ShiftD = 1 << 3,
        [Description("Always enabled")]
        Any = 1 << 7,
    }

    [Serializable]
    public sealed class ShiftActions : ButtonActions
    {
        public ShiftSlot ShiftSlot = ShiftSlot.ShiftA;

        public ShiftActions()
        {
            actionType = ActionType.Shift;

            // disable few options
            HasInterruptable = false;
            HasTurbo = false;
            HasToggle = false;

            outBool = false;
            prevBool = false;
        }

        public ShiftActions(ShiftSlot slot) : this()
        {
            ShiftSlot = slot;
        }
    }
}