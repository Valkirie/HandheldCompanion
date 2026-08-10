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
        private static readonly ButtonFlags[] LeftClickSources =
        [
            ButtonFlags.LeftPadClick,
            ButtonFlags.LeftPadClickUp,
            ButtonFlags.LeftPadClickDown,
            ButtonFlags.LeftPadClickLeft,
            ButtonFlags.LeftPadClickRight,
        ];

        private static readonly ButtonFlags[] RightClickSources =
        [
            ButtonFlags.RightPadClick,
            ButtonFlags.RightPadClickUp,
            ButtonFlags.RightPadClickDown,
            ButtonFlags.RightPadClickLeft,
            ButtonFlags.RightPadClickRight,
        ];

        private static readonly ButtonFlags[] AllClickSources = [.. LeftClickSources, .. RightClickSources];

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
        private static MovementHapticState leftHaptics = new();
        private static MovementHapticState rightHaptics = new();
        private static IController? movementController;
        private static bool leftClickPressed;
        private static bool rightClickPressed;
        private static bool genericClickPressed;
        private static ButtonFlags genericClickButton = ButtonFlags.RightPadClick;
        private static IController? clickController;
        private static ClickHapticProfile leftClickProfile;
        private static ClickHapticProfile rightClickProfile;
        private static ClickHapticProfile genericClickProfile;
        private static TouchpadSample?[] outputSamples = new TouchpadSample?[2];

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

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            actionType = ActionType.Touchpad;
            leftHaptics ??= new();
            rightHaptics ??= new();
        }

        public void SetTarget(ButtonFlags button)
        {
            TargetType = TouchpadTargetType.Button;
            Button = button;
            Axis = AxisLayoutFlags.None;
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

        public static bool HasCustomHapticSettings(IActions action) =>
            action.HapticOverride == true;

        public static bool IsTouchpadAxis(AxisLayoutFlags axis) => axis is
            AxisLayoutFlags.LeftPad or AxisLayoutFlags.RightPad;

        private static bool SupportsGestureTargets(IController controller) => controller is
            DummyDualShock4Controller or DummyDualSenseController;

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

            if (outBool != isKeyDown)
            {
                isKeyDown = outBool;
                if (button != ButtonFlags.None)
                    SetHaptic(button, released: !outBool);
            }

            UpdateGestureState(delta);
        }

        public void Execute(AxisLayout layout, bool touched, ShiftSlot shiftSlot, float delta)
        {
            if (TargetType == TouchpadTargetType.Axis)
            {
                outVector = layout.vector;
                base.Execute(layout, shiftSlot, delta);
                bool sourceReportsTouch = ControllerState.AxisTouchButtons.ContainsKey(layout.flags);
                isTouched = !axisSlotDisabled &&
                    (sourceReportsTouch ? touched : outVector != Vector2.Zero);
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

            int x;
            int y;
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
                x = X;
                y = Y;
                active = outBool;
            }

            sample = active
                ? new TouchpadSample(Button, Finger, x, y)
                : default;
            return active;
        }

        internal static void BeginOutputFrame()
        {
            Array.Clear(outputSamples);
            DS4Touch.ClearOutputTouches();
        }

        internal void ApplyOutput(ControllerState outputState,
            AxisFlags outputAxisX = AxisFlags.None, AxisFlags outputAxisY = AxisFlags.None)
        {
            if (TargetType == TouchpadTargetType.Axis)
            {
                Vector2 value = GetAxisValue();
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
                int fingerIndex = sample.Finger - 1;
                if (outputSamples[fingerIndex] is null || sample.Priority > outputSamples[fingerIndex]!.Value.Priority)
                    outputSamples[fingerIndex] = sample;
                return;
            }

            outputState.ButtonState[Button] |= GetButtonValue();
        }

        internal static void CommitOutputFrame()
        {
            foreach (TouchpadSample? sample in outputSamples)
            {
                if (sample is TouchpadSample value)
                    DS4Touch.SetOutputTouch(value.Finger, value.X, value.Y);
            }
        }

        internal static void UpdateClickHaptics(ControllerState state, ShiftSlot shiftSlot,
            IReadOnlyDictionary<ButtonFlags, IActions[]> buttonPlan)
        {
            IController? controller = ControllerManager.GetTarget();
            bool leftPressed = state.ButtonState[ButtonFlags.LeftPadClick] ||
                state.ButtonState[ButtonFlags.LeftPadClickUp] ||
                state.ButtonState[ButtonFlags.LeftPadClickDown] ||
                state.ButtonState[ButtonFlags.LeftPadClickLeft] ||
                state.ButtonState[ButtonFlags.LeftPadClickRight];
            bool rightPressed = state.ButtonState[ButtonFlags.RightPadClick] ||
                state.ButtonState[ButtonFlags.RightPadClickUp] ||
                state.ButtonState[ButtonFlags.RightPadClickDown] ||
                state.ButtonState[ButtonFlags.RightPadClickLeft] ||
                state.ButtonState[ButtonFlags.RightPadClickRight];
            if (controller is null)
            {
                clickController = null;
                leftClickPressed = false;
                rightClickPressed = false;
                genericClickPressed = false;
                leftClickProfile = default;
                rightClickProfile = default;
                genericClickProfile = default;
                return;
            }

            if (!ReferenceEquals(clickController, controller))
            {
                clickController = controller;
                leftClickPressed = leftPressed;
                rightClickPressed = rightPressed;
                genericClickPressed = leftPressed || rightPressed;
                genericClickButton = GetPressedClickButton(leftPressed, rightPressed);
                leftClickProfile = default;
                rightClickProfile = default;
                genericClickProfile = default;
                return;
            }

            bool disabledByFirmware = controller is SteamController steamController && steamController.IsLizardModeEnabled;
            if (disabledByFirmware)
            {
                leftClickPressed = leftPressed;
                rightClickPressed = rightPressed;
                genericClickPressed = leftPressed || rightPressed;
                leftClickProfile = default;
                rightClickProfile = default;
                genericClickProfile = default;
                return;
            }

            if (controller is SteamController)
            {
                UpdateClickHaptic(controller, ButtonFlags.LeftPadClick,
                    leftPressed, ref leftClickPressed, ref leftClickProfile,
                    state, shiftSlot, buttonPlan, LeftClickSources);
                UpdateClickHaptic(controller, ButtonFlags.RightPadClick,
                    rightPressed, ref rightClickPressed, ref rightClickProfile,
                    state, shiftSlot, buttonPlan, RightClickSources);
                return;
            }

            bool pressed = leftPressed || rightPressed;
            if (pressed && !genericClickPressed)
                genericClickButton = GetPressedClickButton(leftPressed, rightPressed);

            UpdateClickHaptic(controller, genericClickButton, pressed, ref genericClickPressed,
                ref genericClickProfile, state, shiftSlot,
                buttonPlan, AllClickSources);
        }

        private static ButtonFlags GetPressedClickButton(bool leftPressed, bool rightPressed) =>
            leftPressed
                ? ButtonFlags.LeftPadClick
                : rightPressed
                    ? ButtonFlags.RightPadClick
                    : ButtonFlags.None;

        private static void UpdateClickHaptic(IController controller, ButtonFlags button, bool pressed,
            ref bool wasPressed, ref ClickHapticProfile profile, ControllerState state, ShiftSlot shiftSlot,
            IReadOnlyDictionary<ButtonFlags, IActions[]> buttonPlan, ButtonFlags[] sources)
        {
            if (pressed == wasPressed)
                return;

            wasPressed = pressed;
            if (pressed)
                profile = ResolveClickHapticProfile(state, shiftSlot, buttonPlan, sources);

            bool released = !pressed;
            if (profile.Mode == HapticMode.Off ||
                profile.Mode == HapticMode.Down && released ||
                profile.Mode == HapticMode.Up && !released)
                return;

            controller.SetTrackpadClickHaptic(profile.Strength, button, released: !pressed);
        }

        private static ClickHapticProfile ResolveClickHapticProfile(ControllerState state,
            ShiftSlot shiftSlot, IReadOnlyDictionary<ButtonFlags, IActions[]> buttonPlan,
            ButtonFlags[] sources)
        {
            bool hasOverride = false;
            int combinedMode = (int)HapticMode.Off;
            HapticStrength strength = HapticStrength.Low;

            foreach (ButtonFlags source in sources)
            {
                if (!state.ButtonState[source] || !buttonPlan.TryGetValue(source, out IActions[]? actions))
                    continue;

                foreach (IActions action in actions)
                {
                    if (!HasCustomHapticSettings(action) ||
                        !IsShiftAllowed(shiftSlot, action.ShiftSlot, action.ShiftMatchAny))
                        continue;

                    hasOverride = true;
                    combinedMode |= (int)action.HapticMode;
                    if (action.HapticMode != HapticMode.Off &&
                        (int)action.HapticStrength > (int)strength)
                        strength = action.HapticStrength;
                }
            }

            if (hasOverride)
                return new ClickHapticProfile((HapticMode)combinedMode, strength);

            int globalStrength = ManagerFactory.settingsManager.GetInt("TrackpadClickHaptics");
            return globalStrength <= 0
                ? default
                : new ClickHapticProfile(HapticMode.Both,
                    (HapticStrength)Math.Clamp(globalStrength - 1, 0, 2));
        }

        internal static void UpdateMovementHaptics(AxisLayoutFlags axis, Vector2 position, bool touched,
            ShiftSlot shiftSlot, IActions[] actions)
        {
            MovementHapticState state = axis == AxisLayoutFlags.LeftPad ? leftHaptics : rightHaptics;
            IActions? hapticAction = null;
            for (int i = 0; i < actions.Length; i++)
            {
                IActions action = actions[i];
                if (action.HapticMode is HapticMode.Down or HapticMode.Both &&
                    IsShiftAllowed(shiftSlot, action.ShiftSlot, action.ShiftMatchAny) &&
                    (action is MouseActions { MouseType: MouseActionsType.Move or MouseActionsType.Scroll } ||
                     action is TouchpadActions { TargetType: TouchpadTargetType.Axis }))
                {
                    hapticAction = action;
                    break;
                }
            }

            IController? controller = ControllerManager.GetTarget();
            if (!ReferenceEquals(movementController, controller))
            {
                movementController = controller;
                leftHaptics.Reset(touched: false, Vector2.Zero);
                rightHaptics.Reset(touched: false, Vector2.Zero);
            }

            if (!touched || hapticAction is null || controller is null ||
                controller is SteamController steamController && steamController.IsLizardModeEnabled)
            {
                state.Reset(touched, position);
                return;
            }

            if (!state.WasTouched)
            {
                state.Reset(touched: true, position);
                return;
            }

            Vector2 delta = position - state.PreviousPosition;
            state.PreviousPosition = position;
            state.Jitter += delta;

            float distance = state.Jitter.Length();
            if (distance < HapticJitterThreshold)
                return;

            state.Jitter = Vector2.Zero;
            state.Distance += distance;
            if (state.Distance < HapticStep)
                return;

            state.Distance %= HapticStep;
            ButtonFlags button = axis == AxisLayoutFlags.LeftPad
                ? ButtonFlags.LeftPadTouch
                : ButtonFlags.RightPadTouch;
            hapticAction.SetHaptic(button, released: false);
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

    internal readonly record struct TouchpadSample(ButtonFlags Target, byte Finger, int X, int Y)
    {
        public int Priority => Target switch
        {
            ButtonFlags.TouchpadClick => 3,
            ButtonFlags.TouchpadSwipe => 2,
            ButtonFlags.TouchpadTouch => 1,
            _ => 0,
        };
    }
}
