using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class AxisActions : IActions
    {
        public AxisFlags Axis { get; set; }

        // Button to axis
        public double AxisPercentage { get; set; } = 100.0d;
        public byte AxisPolarity { get; set; } = 0;

        // Axis to axis
        public bool AxisInverted { get; set; } = false;
        public int AxisDeadZoneInner { get; set; } = 0;
        public int AxisDeadZoneOuter { get; set; } = 0;
        public float AxisAntiDeadZone { get; set; } = 0.0f;

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

        public override void Execute(AxisFlags axis, short value)
        {
            // Apply inner and outer deadzone adjustments
            value = (short)InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, short.MaxValue);

            // Apply anti deadzone adjustments
            switch (Axis)
            {
                case AxisFlags.L2:
                case AxisFlags.R2:
                    value = (short)InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone);
                    break;
            }

            this.Value = (short)(value * (AxisInverted ? -1 : 1));
        }
    }
}
