using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.Steam;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class ContinuousActions : IActions
    {
        public bool ContinuousHaptics = false;

        public List<Vector2> ResponseCurvePoints = new()
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(0.2f, 0.2f),
            new Vector2(0.4f, 0.4f),
            new Vector2(0.6f, 0.6f),
            new Vector2(0.8f, 0.8f),
            new Vector2(1.0f, 1.0f)
        };

        private const float HapticJitterThreshold = 128.0f;
        private const float HapticStep = (short.MaxValue - short.MinValue) / 10.0f;
        [NonSerialized] private MovementHapticState movementHaptics = new();

        protected void ApplyResponseCurve()
        {
            outVector = InputUtils.ApplyResponseCurve(outVector, ResponseCurvePoints);
        }

        protected void UpdateMovementHaptics(AxisLayoutFlags axis, bool touched, Vector2 position)
        {
            if (axis is not (AxisLayoutFlags.LeftPad or AxisLayoutFlags.RightPad) ||
                !touched || !ContinuousHaptics ||
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

            Vector2 movementDelta = position - movementHaptics.PreviousPosition;
            movementHaptics.PreviousPosition = position;
            movementHaptics.Jitter += movementDelta;

            float distance = movementHaptics.Jitter.Length();
            if (distance < HapticJitterThreshold)
                return;

            movementHaptics.Jitter = Vector2.Zero;
            movementHaptics.Distance += distance;
            if (movementHaptics.Distance < HapticStep)
                return;

            movementHaptics.Distance %= HapticStep;
            ButtonFlags button = axis == AxisLayoutFlags.LeftPad ? ButtonFlags.LeftPadTouch : ButtonFlags.RightPadTouch;

            _ = Task.Run(() =>
            {
                // prevent haptic feedback from slowing down the main thread, but ignore any exceptions that may occur
                try { controller.SetHaptic(HapticStrength, button, released: false); }
                catch { }
            });
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
}
