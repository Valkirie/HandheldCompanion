using System;
using ControllerCommon.Inputs;

namespace ControllerCommon.Actions;

[Serializable]
public class TriggerActions : IActions
{
    public TriggerActions()
    {
        ActionType = ActionType.Trigger;
        Value = (short)0;
    }

    public TriggerActions(AxisLayoutFlags axis) : this()
    {
        Axis = axis;
    }

    public AxisLayoutFlags Axis { get; set; }

    public override void Execute(AxisFlags axis, short value)
    {
        // Apply inner and outer deadzone adjustments
        // value = (short)InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, short.MaxValue);
        // value = (short)InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone);

        Value = value;
    }

    public short GetValue()
    {
        return (short)Value;
    }
}