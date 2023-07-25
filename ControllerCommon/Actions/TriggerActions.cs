using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class TriggerActions : IActions
    {
        public AxisLayoutFlags Axis { get; set; }

        public int AxisDeadZoneInner { get; set; } = 0;
        public int AxisDeadZoneOuter { get; set; } = 0;
        public int AxisAntiDeadZone { get; set; } = 0;

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
            value = (short)InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, byte.MaxValue);
            value = (short)InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone, byte.MaxValue);

            this.Value = (short)(value);
        }

        public short GetValue()
        {
            return (short)this.Value;
        }
    }
}