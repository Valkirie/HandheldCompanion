using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class GyroActions : IActions
    {
        public MotionInput MotionInput = MotionInput.JoystickCamera;
        public MotionMode MotionMode = MotionMode.Off;

        public ButtonState MotionTrigger = new();

        public const int DefaultAxisAntiDeadZone = 15;
        public const AxisLayoutFlags DefaultAxisLayoutFlags = AxisLayoutFlags.RightStick;

        public const MouseActionsType DefaultMouseActionsType = MouseActionsType.Move;
        public const int DefaultSensivity = 33;
        public const int DefaultDeadzone = 10;

        public GyroActions()
        {
        }
    }
}
