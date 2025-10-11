using System;
using System.ComponentModel;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum ShiftSlot
    {
        [Description("None")]
        None,
        [Description("Enable shift A")]
        ShiftA,
        [Description("Enable shift B")]
        ShiftB,
        [Description("Enable shift C")]
        ShiftC,
        [Description("Enable shift D")]
        ShiftD,
        [Description("Always enabled")]
        Any,
    }

    [Serializable]
    public sealed class ShiftActions : ButtonActions
    {
        public ShiftSlot ShiftSlot;

        public ShiftActions()
        {
            actionType = ActionType.Shift;

            // disable few options
            HasInterruptable = false;
            HasTurbo = false;

            outBool = false;
            prevBool = false;
        }

        public ShiftActions(ShiftSlot slot) : this()
        {
            ShiftSlot = slot;
        }
    }
}