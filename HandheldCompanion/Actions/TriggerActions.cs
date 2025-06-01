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

        public void Execute(AxisFlags axis, byte value, ShiftSlot shiftSlot)
        {
            // update value
            this.Value = value;

            // call parent, check shiftSlot
            base.Execute(axis, shiftSlot);

            // skip if zero
            if ((byte)this.Value == 0)
                return;

            // Apply inner and outer deadzone adjustments
            value = (byte)InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, byte.MaxValue);
            value = (byte)InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone, byte.MaxValue);

            this.Value = value;
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot = Actions.ShiftSlot.None)
        {
            // call parent, check shiftSlot
            base.Execute(button, value, shiftSlot);

            // skip if value is false
            if (this.Value is bool bValue && !bValue)
                return;

            this.Value = (byte)motionThreshold;
        }

        public byte GetValue()
        {
            if (this.Value is byte sValue)
                return sValue;

            return 0;
        }
    }
}
