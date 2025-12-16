using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using HandheldCompanion.Utils;
using System;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public sealed class KeyboardActions : IActions
    {
        public VirtualKeyCode Key;

        // runtime variables
        private bool IsKeyDown = false;
        private KeyCode[] pressed;

        protected override bool GetActualOutputState() => KeyboardSimulator.IsKeyDown(Key);

        // settings
        public ModifierSet Modifiers = ModifierSet.None;

        public KeyboardActions()
        {
            actionType = ActionType.Keyboard;
            outBool = false;
            prevBool = false;
        }

        public KeyboardActions(VirtualKeyCode key) : this()
        {
            Key = key;
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            if (outBool)
            {
                if (IsKeyDown) return;
                IsKeyDown = true;
                pressed = ModifierMap[Modifiers];
                KeyboardSimulator.KeyDown(pressed);
                KeyboardSimulator.KeyDown(Key);
                SetHaptic(button, false);
            }
            else
            {
                if (!IsKeyDown) return;
                IsKeyDown = false;
                KeyboardSimulator.KeyUp(Key);
                KeyboardSimulator.KeyUp(pressed);
                SetHaptic(button, true);
            }
        }

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero && !IsKeyDown)
                return;

            var direction = InputUtils.GetDeflectionDirection(outVector, motionThreshold);
            bool press = DirectionMatches(direction, motionDirection);

            Execute(ButtonFlags.None, press, shiftSlot, delta);
        }
    }
}