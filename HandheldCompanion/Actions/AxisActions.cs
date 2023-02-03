using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class AxisActions : IActions
    {
        public AxisFlags Axis { get; set; }

        public AxisActions()
        {
            this.ActionType = ActionType.Axis;
            this.Value = (short)0;
        }

        public AxisActions(AxisFlags axis) : this()
        {
            this.Axis = axis;
        }

        public short GetValue()
        {
            return (short)this.Value;
        }

        public override void Execute(ButtonFlags button, bool value)
        {
        }
    }
}
