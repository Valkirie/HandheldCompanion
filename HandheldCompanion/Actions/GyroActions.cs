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
        public bool MotionTogglePressed = false; // debounce flag

        public ButtonState MotionTrigger = new();

        public float gyroWeight = DefaultGyroWeight;

        // Gyro velocity scaling mode
        public GyroVelocityMode VelocityMode = GyroVelocityMode.Default;

        // Velocity scaling factor (only used when VelocityMode is enabled)
        // Higher values = faster movements produce more displacement
        public float VelocityScale = DefaultVelocityScale;

        // Defaults shared with derived classes
        public const int DefaultAxisAntiDeadZone = 15;
        public const AxisLayoutFlags DefaultAxisLayoutFlags = AxisLayoutFlags.RightStick;
        public const MouseActionsType DefaultMouseActionsType = MouseActionsType.Move;
        public const int DefaultSensivity = 35;
        public const int DefaultDeadzone = 10;
        public const float DefaultGyroWeight = 1.2f;
        public const float DefaultVelocityScale = 1.0f;

        public GyroActions() { }
    }

    public enum GyroVelocityMode
    {
        Default = 0,      // Legacy behavior (no delta scaling)
        Velocity = 1      // Scale by delta time (velocity-based)
    }
}
