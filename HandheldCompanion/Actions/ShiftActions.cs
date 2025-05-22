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
