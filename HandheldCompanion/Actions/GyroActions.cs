using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.Steam;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

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

        public List<Vector2> ResponseCurvePoints = new()
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(0.2f, 0.2f),
            new Vector2(0.4f, 0.4f),
            new Vector2(0.6f, 0.6f),
            new Vector2(0.8f, 0.8f),
            new Vector2(1.0f, 1.0f)
        };

        private const float HapticStep = (short.MaxValue - short.MinValue) / 10.0f;
        private const float HapticJitterThreshold = 128.0f;
        [NonSerialized] private MovementHapticState movementHaptics = new();

        // Defaults shared with derived classes
        public const int DefaultAxisAntiDeadZone = 15;
        public const AxisLayoutFlags DefaultAxisLayoutFlags = AxisLayoutFlags.RightStick;
        public const MouseActionsType DefaultMouseActionsType = MouseActionsType.Move;
        public const int DefaultSensivity = 35;
        public const int DefaultDeadzone = 10;
        public const float DefaultGyroWeight = 1.2f;
        public const float DefaultVelocityScale = 1.0f;

        public GyroActions() { }

        protected void ApplyResponseCurve()
        {
            outVector = InputUtils.ApplyResponseCurve(outVector, ResponseCurvePoints);
        }

        protected void UpdateMovementHaptics(AxisLayoutFlags axis, bool touched, Vector2 position)
        {
            if (axis is not (AxisLayoutFlags.LeftPad or AxisLayoutFlags.RightPad) ||
                !touched || HapticMode is not (HapticMode.Down or HapticMode.Both) ||
                ControllerManager.GetTarget() is not IController controller ||
                controller is SteamController { IsLizardModeEnabled: true })
            {
                movementHaptics.Reset(touched, position);
                return;
            }

            if (!movementHaptics.WasTouched)
            {
                movementHaptics.Reset(touched: true, position);
                return;
            }

            Vector2 delta = position - movementHaptics.PreviousPosition;
            movementHaptics.PreviousPosition = position;
            movementHaptics.Jitter += delta;

            float distance = movementHaptics.Jitter.Length();
            if (distance < HapticJitterThreshold)
                return;

            movementHaptics.Jitter = Vector2.Zero;
            movementHaptics.Distance += distance;
            if (movementHaptics.Distance < HapticStep)
                return;

            movementHaptics.Distance %= HapticStep;
            ButtonFlags button = axis == AxisLayoutFlags.LeftPad ? ButtonFlags.LeftPadTouch : ButtonFlags.RightPadTouch;
            controller.SetHaptic(HapticStrength, button, released: false);
        }

        [Serializable]
        private sealed class MovementHapticState
        {
            public bool WasTouched;
            public Vector2 PreviousPosition;
            public Vector2 Jitter;
            public float Distance;

            public void Reset(bool touched, Vector2 position)
            {
                WasTouched = touched;
                PreviousPosition = position;
                Jitter = Vector2.Zero;
                Distance = 0.0f;
            }
        }
    }

    public enum GyroVelocityMode
    {
        Default = 0,      // Legacy behavior (no delta scaling)
        Velocity = 1      // Scale by delta time (velocity-based)
    }
}
