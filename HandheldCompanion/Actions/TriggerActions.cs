using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Runtime.CompilerServices;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public sealed class TriggerActions : IActions
    {
        public AxisLayoutFlags Axis;

        // Deadzone / anti-deadzone settings (percent, 0..100)
        public int AxisAntiDeadZone = 0;
        public int AxisDeadZoneInner = 0;
        public int AxisDeadZoneOuter = 0;

        // Output value in [0, 255] — byte matches the trigger's native resolution
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

        /// <summary>Axis-driven trigger (from a stick or pad axis).</summary>
        public void Execute(AxisFlags axis, float value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(axis, shiftSlot, delta);

            if (axisSlotDisabled) { current = 0; return; }

            // Clamp raw value into the byte range before processing
            value = Math.Clamp(value, byte.MinValue, byte.MaxValue);
            if (value == 0f) { current = 0; return; }

            value = InputUtils.InnerOuterDeadzone(value, AxisDeadZoneInner, AxisDeadZoneOuter, byte.MaxValue);
            value = InputUtils.ApplyAntiDeadzone(value, AxisAntiDeadZone, byte.MaxValue);

            current = (byte)value;
        }

        /// <summary>Button-driven trigger (press/toggle/turbo handled in base).</summary>
        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            // Preserve prior behavior: threshold cast to byte when pressed
            current = (byte)(outBool ? motionThreshold : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetValue() => current;
    }
}