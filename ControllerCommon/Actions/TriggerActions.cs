using ControllerCommon.Inputs;
using System;

namespace ControllerCommon.Actions
{
    [Serializable]
    public class TriggerActions : IActions
    {
        public AxisLayoutFlags Axis { get; set; }

        public TriggerActions()
        {
            this.ActionType = ActionType.Trigger;
            this.Value = (short)0;
        }

        public TriggerActions(AxisLayoutFlags axis) : this()
        {
            this.Axis = axis;
        }

        public override void Execute(AxisFlags axis, short value)
        {
            // Apply inner and outer deadzone adjustments
            // value = (short)InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, short.MaxValue);
            // value = (short)InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone);

            this.Value = (short)(value);
        }

        public short GetValue()
        {
            return (short)this.Value;
        }
    }
}
