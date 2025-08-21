using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public sealed class TriggerActions : IActions
    {
        public AxisLayoutFlags Axis;

        // settings
        public int AxisAntiDeadZone = 0;
        public int AxisDeadZoneInner = 0;
        public int AxisDeadZoneOuter = 0;

        // runtime output (byte fits trigger path)
        private byte current;

        public TriggerActions()
        {
            actionType = ActionType.Trigger;
            current = 0;
        }

        public TriggerActions(AxisLayoutFlags axis) : this()
        {
            Axis = axis;
        }

        // Axis-driven trigger
        public void Execute(AxisFlags axis, float value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(axis, shiftSlot, delta);
            if (axisSlotDisabled) { current = 0; return; }

            // clamp to 0..255 (byte)
            value = Math.Clamp(value, byte.MinValue, byte.MaxValue);

            if (value == 0.0f) { current = 0; return; }

            // deadzones
            value = InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, byte.MaxValue);
            value = InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone, byte.MaxValue);

            current = (byte)value;
        }

        // Button-driven trigger (pressType/toggle/turbo handled in base)
        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);
            if (!outBool) return;

            // Preserve prior behavior (motionThreshold cast to byte as before)
            current = (byte)motionThreshold;
        }

        public byte GetValue() => current;
    }
}