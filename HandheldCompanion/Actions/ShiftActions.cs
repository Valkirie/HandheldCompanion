using System;
using System.ComponentModel;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum ShiftSlot
    {
        [Description("None")]
        None,
        [Description("Shift A")]
        ShiftA,
        [Description("Shift B")]
        ShiftB,
        [Description("Shift C")]
        ShiftC,
        [Description("Shift D")]
        ShiftD,
    }

    [Serializable]
    public class ShiftActions : ButtonActions
    {
        public ShiftSlot ShiftSlot;

        public ShiftActions()
        {
            this.actionType = ActionType.Shift;

            // disable few options
            this.HasInterruptable = false;
            this.HasTurbo = false;

            this.Value = false;
            this.prevValue = false;
        }

        public ShiftActions(ShiftSlot slot) : this()
        {
            this.ShiftSlot = slot;
        }
    }
}
