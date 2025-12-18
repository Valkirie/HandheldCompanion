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

        /// <summary>
        /// Use shared toggle state from KeyboardSimulator.
        /// This allows multiple bindings targeting the same key to share toggle state,
        /// and detects when the key is released externally (by user or other app).
        /// </summary>
        protected override (bool useShared, bool toggleState) GetSharedToggleState(bool risingEdge)
        {
            // First, check current state (this also detects external key releases)
            bool currentState = KeyboardSimulator.GetToggleState(Key);

            // Flip toggle on rising edge (button press)
            if (risingEdge)
                currentState = KeyboardSimulator.FlipToggle(Key);

            return (true, currentState);
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