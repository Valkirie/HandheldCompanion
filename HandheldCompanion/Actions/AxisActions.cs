using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Numerics;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class AxisActions : GyroActions
    {
        public AxisLayoutFlags Axis;

        // Axis to axis
        public bool ImproveCircularity = false;
        public int AxisAntiDeadZone = 0;
        public int AxisDeadZoneInner = 0;
        public int AxisDeadZoneOuter = 0;

        public AxisActions()
        {
            this.actionType = ActionType.Joystick;
            this.Value = new Vector2();
        }

        public AxisActions(AxisLayoutFlags axis) : this()
        {
            this.Axis = axis;
        }

        public void Execute(AxisLayout layout)
        {
            layout.vector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(layout.vector, AxisDeadZoneInner, AxisDeadZoneOuter);
            layout.vector = InputUtils.ApplyAntiDeadzone(layout.vector, AxisAntiDeadZone);

            if (ImproveCircularity)
                layout.vector = InputUtils.ImproveCircularity(layout.vector);

            this.Value = layout.vector;
        }

        public Vector2 GetValue()
        {
            return (Vector2)this.Value;
        }
    }
}
