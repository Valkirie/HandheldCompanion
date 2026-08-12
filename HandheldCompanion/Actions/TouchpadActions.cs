using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.Dummies;
using HandheldCompanion.Controllers.Steam;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum TouchpadTargetType
    {
        Button = 0,
        Axis = 1,
    }

    [Serializable]
    public sealed class TouchpadActions : GyroActions
    {
        public const float MinimumSwipeDuration = 16.0f;
        public const float MaximumSwipeDuration = 5000.0f;
        public const float DefaultSwipeDuration = 300.0f;

        private const float HapticStep = (short.MaxValue - short.MinValue) / 10.0f;
        private const float HapticJitterThreshold = 128.0f;

        internal static readonly ButtonFlags[] GestureTargets =
        [
            ButtonFlags.TouchpadSwipe,
        ];

        public TouchpadTargetType TargetType = TouchpadTargetType.Button;
        public ButtonFlags Button;
        public AxisLayoutFlags Axis;
        public short ButtonX;
        public short ButtonY;
        public int AxisAntiDeadZone = 0;
        public int AxisDeadZoneInner = 0;
        public int AxisDeadZoneOuter = 0;
        public bool UseCoordinates;
        private byte finger = 1;

        private int x = DS4Touch.TOUCHPAD_WIDTH / 2;
        private int y = DS4Touch.TOUCHPAD_HEIGHT / 2;
        private int endX = DS4Touch.TOUCHPAD_WIDTH * 3 / 4;
        private int endY = DS4Touch.TOUCHPAD_HEIGHT / 2;
        private float swipeDuration = DefaultSwipeDuration;

        public int X { get => x; set => x = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_WIDTH - 1); }
        public int Y { get => y; set => y = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_HEIGHT - 1); }
        public int EndX { get => endX; set => endX = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_WIDTH - 1); }
        public int EndY { get => endY; set => endY = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_HEIGHT - 1); }
        public byte Finger { get => finger; set => finger = (byte)Math.Clamp((int)value, 1, 2); }
        public float SwipeDuration
        {
            get => swipeDuration;
            set => swipeDuration = float.IsFinite(value)
                ? Math.Clamp(value, MinimumSwipeDuration, MaximumSwipeDuration)
                : DefaultSwipeDuration;
        }

        [NonSerialized] private bool wasPressed;
        [NonSerialized] private bool swipeActive;
        [NonSerialized] private bool swipeEndsAfterReport;
        [NonSerialized] private float swipeElapsed;
        [NonSerialized] private bool isKeyDown;
        [NonSerialized] private bool isTouched;
        [NonSerialized] private bool clickPressed;
        [NonSerialized] private MovementHapticState movementHaptics = new();

        public TouchpadActions()
        {
            actionType = ActionType.Touchpad;
        }

        public TouchpadActions(ButtonFlags button) : this()
        {
            SetTarget(button);
        }

        public TouchpadActions(AxisLayoutFlags axis) : this()
        {
            SetTarget(axis);
        }

        public void SetTarget(ButtonFlags button)
        {
            TargetType = TouchpadTargetType.Button;
            Button = button;
            Axis = AxisLayoutFlags.None;

            if (button == ButtonFlags.TouchpadSwipe)
                UseCoordinates = true;
        }

        public void SetTarget(AxisLayoutFlags axis)
        {
            TargetType = TouchpadTargetType.Axis;
            Axis = axis;
            Button = ButtonFlags.None;
        }

        public static bool IsGestureTarget(ButtonFlags button) => button is
            ButtonFlags.TouchpadClick or ButtonFlags.TouchpadTouch or ButtonFlags.TouchpadSwipe;

        public static bool IsTouchpadButton(ButtonFlags button) => button is
            ButtonFlags.LeftPadTouch or ButtonFlags.RightPadTouch or
            ButtonFlags.LeftPadClick or ButtonFlags.RightPadClick or
            ButtonFlags.LeftPadClickUp or ButtonFlags.LeftPadClickDown or
            ButtonFlags.LeftPadClickLeft or ButtonFlags.LeftPadClickRight or
            ButtonFlags.RightPadClickUp or ButtonFlags.RightPadClickDown or
            ButtonFlags.RightPadClickLeft or ButtonFlags.RightPadClickRight or
            ButtonFlags.TouchpadClick or ButtonFlags.TouchpadTouch or ButtonFlags.TouchpadSwipe;

        public static bool IsLeftPadClick(ButtonFlags button) => button is
            ButtonFlags.LeftPadClick or ButtonFlags.LeftPadClickUp or ButtonFlags.LeftPadClickDown or
            ButtonFlags.LeftPadClickLeft or ButtonFlags.LeftPadClickRight;

        public static bool IsRightPadClick(ButtonFlags button) => button is
            ButtonFlags.RightPadClick or ButtonFlags.RightPadClickUp or ButtonFlags.RightPadClickDown or
            ButtonFlags.RightPadClickLeft or ButtonFlags.RightPadClickRight;

        public static bool IsPhysicalClick(ButtonFlags button) => IsLeftPadClick(button) || IsRightPadClick(button);

        public static bool IsTouchpadAxis(AxisLayoutFlags axis) => axis is
            AxisLayoutFlags.LeftPad or AxisLayoutFlags.RightPad;

        private static bool SupportsGestureTargets(IController controller) => controller is
            DummyDualShock4Controller or DummyDualSenseController or SteamController;

        public static bool HasTargets(IController controller) =>
            controller.GetTargetButtons().Any(IsTouchpadButton) ||
            controller.GetTargetAxis().Any(IsTouchpadAxis) ||
            SupportsGestureTargets(controller);

        public static IEnumerable<ButtonFlags> GetButtonTargets(IController controller)
        {
            foreach (ButtonFlags button in controller.GetTargetButtons().Where(IsTouchpadButton))
                yield return button;

            if (SupportsGestureTargets(controller))
            {
                foreach (ButtonFlags button in GestureTargets)
                    yield return button;
            }
        }

        public static IEnumerable<AxisLayoutFlags> GetAxisTargets(IController controller) =>
            controller.GetTargetAxis().Where(IsTouchpadAxis);

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            if (TargetType == TouchpadTargetType.Axis)
            {
                outVector = outBool ? new Vector2(ButtonX, ButtonY) : Vector2.Zero;
                isTouched = outBool;
                return;
            }

            isTouched = outBool;

            if (outBool)
            {
                if (!isKeyDown)
                {
                    isKeyDown = true;
                    SetHaptic(button, released: false);
                }
            }
            else if (isKeyDown)
            {
                isKeyDown = false;
                SetHaptic(button, released: true);
            }

            UpdateGestureState(delta);
        }

        public void Execute(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            if (TargetType == TouchpadTargetType.Axis)
            {
                outVector = layout.vector;
                base.Execute(layout, shiftSlot, delta);
                ApplyAxisDeadzones();
                bool sourceReportsTouch = ControllerState.AxisTouchButtons.ContainsKey(layout.flags);
                isTouched = !axisSlotDisabled &&
                    outVector != Vector2.Zero &&
                    (!sourceReportsTouch || touched);
                UpdateMovementHaptics(isTouched, outVector);
                return;
            }

            outVector = layout.vector;
            base.Execute(layout, shiftSlot, delta);

            DeflectionDirection direction = InputUtils.GetDeflectionDirection(outVector, motionThreshold);
            bool pressed = DirectionMatches(direction, motionDirection);
            ButtonFlags sourceButton = layout.flags switch
            {
                AxisLayoutFlags.LeftStick => ButtonFlags.LeftStickTouch,
                AxisLayoutFlags.RightStick => ButtonFlags.RightStickTouch,
                AxisLayoutFlags.LeftPad => ButtonFlags.LeftPadTouch,
                AxisLayoutFlags.RightPad => ButtonFlags.RightPadTouch,
                AxisLayoutFlags.L2 => ButtonFlags.L2Soft,
                AxisLayoutFlags.R2 => ButtonFlags.R2Soft,
                _ => ButtonFlags.None,
            };
            Execute(sourceButton, pressed, shiftSlot, delta);
        }

        private void ApplyAxisDeadzones()
        {
            if (outVector == Vector2.Zero)
                return;

            outVector = InputUtils.ThumbScaledRadialInnerOuterDeadzone(outVector, AxisDeadZoneInner, AxisDeadZoneOuter);
            outVector = InputUtils.ApplyAntiDeadzone(outVector, AxisAntiDeadZone);
        }

        public bool GetButtonValue() => outBool;

        public Vector2 GetAxisValue() => outVector;

        public bool GetTouchValue() => isTouched;

        private void UpdateGestureState(float delta)
        {
            if (TargetType != TouchpadTargetType.Button)
                return;

            bool pressed = outBool;
            if (Button == ButtonFlags.TouchpadSwipe && pressed && !wasPressed)
            {
                swipeActive = true;
                swipeEndsAfterReport = false;
                swipeElapsed = 0.0f;
            }
            else if (swipeActive)
            {
                swipeElapsed += delta;
                if (swipeElapsed >= SwipeDuration)
                {
                    swipeElapsed = SwipeDuration;
                    swipeEndsAfterReport = true;
                }
            }

            wasPressed = pressed;
        }

        internal bool TryGetTouch(out TouchpadSample sample)
        {
            if (TargetType != TouchpadTargetType.Button || !IsGestureTarget(Button))
            {
                sample = default;
                return false;
            }

            int? x;
            int? y;
            bool active;

            if (Button == ButtonFlags.TouchpadSwipe)
            {
                float progress = Math.Clamp(swipeElapsed / SwipeDuration, 0.0f, 1.0f);
                x = (int)MathF.Round(X + (EndX - X) * progress);
                y = (int)MathF.Round(Y + (EndY - Y) * progress);
                active = swipeActive;
                if (swipeEndsAfterReport)
                {
                    swipeActive = false;
                    swipeEndsAfterReport = false;
                }
            }
            else
            {
                x = UseCoordinates ? X : null;
                y = UseCoordinates ? Y : null;
                active = outBool;
            }

            sample = active
                ? new TouchpadSample(Button, Finger, x, y)
                : default;
            return active;
        }

        internal void ApplyOutput(ControllerState outputState)
        {
            if (TargetType == TouchpadTargetType.Axis)
            {
                Vector2 value = GetAxisValue();
                var layout = AxisLayout.Layouts[Axis];
                AxisFlags outputAxisX = layout.GetAxisFlags('X');
                AxisFlags outputAxisY = layout.GetAxisFlags('Y');
                outputState.AxisState[outputAxisX] = ClampShort(outputState.AxisState[outputAxisX] + value.X);
                outputState.AxisState[outputAxisY] = ClampShort(outputState.AxisState[outputAxisY] + value.Y);

                if (ControllerState.AxisTouchButtons.TryGetValue(Axis, out ButtonFlags touchButton))
                    outputState.ButtonState[touchButton] |= GetTouchValue();
                return;
            }

            if (IsGestureTarget(Button))
            {
                if (!TryGetTouch(out TouchpadSample sample))
                    return;

                outputState.ButtonState[Button] |= true;
                if (sample.X is int x && sample.Y is int y)
                    DS4Touch.SetOutputTouch(sample.Finger, x, y);
                return;
            }

            outputState.ButtonState[Button] |= GetButtonValue();
        }

        internal void ApplyGyroOutput(ControllerState outputState)
        {
            var layout = AxisLayout.Layouts[Axis];
            var outputAxisX = layout.GetAxisFlags('X');
            var outputAxisY = layout.GetAxisFlags('Y');
            var current = new Vector2(outputState.AxisState[outputAxisX], outputState.AxisState[outputAxisY]);
            float padNorm = Math.Clamp(current.Length() / short.MaxValue, 0f, 1f);
            float weightFactor = gyroWeight - padNorm;
            var blended = current + GetAxisValue() * weightFactor;

            outputState.AxisState[outputAxisX] = ClampShort(blended.X);
            outputState.AxisState[outputAxisY] = ClampShort(blended.Y);

            if (ControllerState.AxisTouchButtons.TryGetValue(Axis, out ButtonFlags touchButton))
                outputState.ButtonState[touchButton] |= GetTouchValue();
        }

        private void UpdateMovementHaptics(bool touched, Vector2 position)
        {
            if (!touched || HapticMode is not (HapticMode.Down or HapticMode.Both) ||
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
            ButtonFlags button = Axis == AxisLayoutFlags.LeftPad ? ButtonFlags.LeftPadTouch : ButtonFlags.RightPadTouch;

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

        private readonly record struct ClickHapticProfile(HapticMode Mode, HapticStrength Strength);

        private static short ClampShort(float value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }

    internal readonly record struct TouchpadSample(ButtonFlags Target, byte Finger, int? X, int? Y)
    { }
}
