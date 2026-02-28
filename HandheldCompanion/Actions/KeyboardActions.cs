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

        // Runtime
        private bool     isKeyDown = false;
        private KeyCode[] modifiersPressed;

        // Settings
        public ModifierSet Modifiers = ModifierSet.None;

        public KeyboardActions()
        {
            actionType = ActionType.Keyboard;
            outBool    = false;
            prevBool   = false;
        }

        public KeyboardActions(VirtualKeyCode key) : this()
        {
            Key = key;
        }

        /// <summary>
        /// Shares toggle state across all bindings targeting the same key, and detects
        /// external key releases (e.g. the user physically pressing the key).
        /// </summary>
        protected override (bool useShared, bool toggleState) GetSharedToggleState(bool risingEdge)
        {
            bool state = KeyboardSimulator.GetToggleState(Key);

            if (risingEdge)
                state = KeyboardSimulator.FlipToggle(Key);

            return (true, state);
        }

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            if (outBool)
            {
                if (isKeyDown) return;
                isKeyDown = true;

                modifiersPressed = ModifierMap[Modifiers];
                KeyboardSimulator.KeyDown(modifiersPressed);
                KeyboardSimulator.KeyDown(Key);

                SetHaptic(button, released: false);
            }
            else
            {
                if (!isKeyDown) return;
                isKeyDown = false;

                KeyboardSimulator.KeyUp(Key);
                KeyboardSimulator.KeyUp(modifiersPressed);

                SetHaptic(button, released: true);
            }
        }

        public override void Execute(AxisLayout layout, ShiftSlot shiftSlot, float delta)
        {
            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            if (outVector == Vector2.Zero && !isKeyDown)
                return;

            var  direction = InputUtils.GetDeflectionDirection(outVector, motionThreshold);
            bool press     = DirectionMatches(direction, motionDirection);

            Execute(ButtonFlags.None, press, shiftSlot, delta);
        }
    }
}
