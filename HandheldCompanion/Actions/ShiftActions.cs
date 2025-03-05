using System;
using System.ComponentModel;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum ShiftSlot
    {
        [Description("None")]
        None,
        [Description("Enabled with shift A")]
        ShiftA,
        [Description("Enabled with shift B")]
        ShiftB,
        [Description("Enabled with shift C")]
        ShiftC,
        [Description("Enabled with shift D")]
        ShiftD,
        [Description("Always enabled")]
        Any,
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
