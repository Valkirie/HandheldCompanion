using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class AxisActions : IActions
    {
        public AxisFlags Axis { get; set; }

        public double AxisPercentage { get; set; } = 100.0d;
        public byte AxisPolarity { get; set; } = 0;

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
            return Convert.ToInt16(this.Value);
        }

        public override void Execute(ButtonFlags button, bool value)
        {
            this.Value = (short)(this.AxisPercentage * short.MaxValue / 100.0d) * (AxisPolarity == 0 ? 1 : -1);
        }
    }
}
