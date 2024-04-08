using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class GyroActions : IActions
    {
        public MotionInput MotionInput = MotionInput.LocalSpace;
        public MotionMode MotionMode = MotionMode.Off;
        public bool MotionToggleStatus = false;
        public bool MotionTogglePressed = false; // for debouncing

        public ButtonState MotionTrigger = new();

        public float gyroWeight = DefaultGyroWeight;

        // const vars
        public const int DefaultAxisAntiDeadZone = 15;
        public const AxisLayoutFlags DefaultAxisLayoutFlags = AxisLayoutFlags.RightStick;

        public const MouseActionsType DefaultMouseActionsType = MouseActionsType.Move;
        public const int DefaultSensivity = 33;
        public const int DefaultDeadzone = 10;
        public const float DefaultGyroWeight = 1.2f;

        public GyroActions()
        {
        }
    }
}
