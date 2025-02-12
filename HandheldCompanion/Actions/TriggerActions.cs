using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class TriggerActions : IActions
    {
        public AxisLayoutFlags Axis;

        // settings
        public int AxisAntiDeadZone = 0;
        public int AxisDeadZoneInner = 0;
        public int AxisDeadZoneOuter = 0;

        public TriggerActions()
        {
            this.actionType = ActionType.Trigger;
            this.Value = (short)0;
        }

        public TriggerActions(AxisLayoutFlags axis) : this()
        {
            this.Axis = axis;
        }

        public void Execute(AxisFlags axis, short value, ShiftSlot shiftSlot)
        {
            // update value
            this.Value = value;

            // call parent, check shiftSlot
            base.Execute(axis, shiftSlot);

            // skip if zero
            if ((short)this.Value == 0)
                return;

            // Apply inner and outer deadzone adjustments
            value = (short)InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, byte.MaxValue);
            value = (short)InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone, byte.MaxValue);

            this.Value = value;
        }

        public short GetValue()
        {
            return (short)this.Value;
        }
    }
}
