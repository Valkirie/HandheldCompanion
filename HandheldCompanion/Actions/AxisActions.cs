using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class AxisActions : IActions
    {
        public AxisFlags Axis { get; set; }
        public short Value { get; set; }

        public AxisActions()
        {
            this.ActionType = ActionType.Axis;
        }

        public AxisActions(AxisFlags axis) : this()
        {
            this.Axis = axis;
        }

        public AxisActions(AxisFlags axis, short value) : this()
        {
            this.Axis = axis;
            this.Value = value;
        }
    }
}
