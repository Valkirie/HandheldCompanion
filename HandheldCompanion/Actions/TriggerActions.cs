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

        public void Execute(AxisFlags axis, short value)
        {
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
