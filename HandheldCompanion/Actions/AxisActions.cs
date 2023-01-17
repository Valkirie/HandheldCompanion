using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class AxisActions : IActions
    {
        public AxisFlags Axis { get; }
        public short Value { get; }

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
