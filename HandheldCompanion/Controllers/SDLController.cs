using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static SDL3.SDL;
using Color = System.Windows.Media.Color;

namespace HandheldCompanion.Controllers
{
    public class SDLController : IController
    {
        public nint gamepad = IntPtr.Zero;
        public uint deviceIndex = 0;
        public uint deviceProperties => GetGamepadProperties(this.gamepad);

        public override bool IsConnected() => GamepadConnected(this.gamepad);
        public override byte UserIndex => (byte)GetGamepadPlayerIndex(this.gamepad);

        private bool HasGyro => GamepadHasSensor(this.gamepad, SensorType.Gyro);
        private bool HasAccel => GamepadHasSensor(this.gamepad, SensorType.Accel);
        private bool HasMotion => HasGyro || HasAccel;

        private bool HasButton(GamepadButton button) => GamepadHasButton(this.gamepad, button);
        private bool HasAxis(GamepadAxis axis) => GamepadHasAxis(this.gamepad, axis);

        private bool HasTouchpad => GetTouchpads() != 0;
        protected virtual int GetTouchpads() => GetNumGamepadTouchpads(this.gamepad);
        protected virtual int GetTouchpadFingers(int touchpad) => GetNumGamepadTouchpadFingers(this.gamepad, touchpad);

        private bool HasMonoLed => GetBooleanProperty(deviceProperties, Props.GamepadCapMonoLedBoolean, false);
        private bool HasRGBLed => GetBooleanProperty(deviceProperties, Props.GamepadCapRGBLedBoolean, false);
        private bool HasPlayerLed => GetBooleanProperty(deviceProperties, Props.GamepadCapPlayerLedBoolean, false);
        private bool HasRumble => GetBooleanProperty(deviceProperties, Props.GamepadCapRumbleBoolean, false);
        private bool HasTriggerRumble => GetBooleanProperty(deviceProperties, Props.GamepadCapTriggerRumbleBoolean, false);

        public override bool IsWireless() => GetGamepadConnectionState(this.gamepad) == JoystickConnectionState.Wireless;

        protected const byte TriggerThreshold = 60;

        private ulong lastCounter = GetPerformanceCounter();
        private readonly float freq = GetPerformanceFrequency();

        public SDLController()
        { }

        public SDLController(nint gamepad, uint deviceIndex, PnPDetails details)
        {
            if (details is null)
                throw new Exception("SDLController PnPDetails is null");

            this.gamepad = gamepad;
            this.deviceIndex = deviceIndex;

            // prepare sensor
            SetGamepadSensorEnabled(gamepad, SensorType.Gyro, HasGyro);
            SetGamepadSensorEnabled(gamepad, SensorType.Accel, HasAccel);

            // Capabilities
            Capabilities |= HasMotion ? ControllerCapabilities.MotionSensor : ControllerCapabilities.None;
            Capabilities |= HasRumble ? ControllerCapabilities.Rumble : ControllerCapabilities.None;

            AttachDetails(details);

            // UI
            GamepadType type = GetGamepadType(gamepad);
            switch (type)
            {
                case GamepadType.Xbox360:
                case GamepadType.XboxOne:
                    ColoredButtons.Add(ButtonFlags.B1, Color.FromArgb(255, 81, 191, 61));
                    ColoredButtons.Add(ButtonFlags.B2, Color.FromArgb(255, 217, 65, 38));
                    ColoredButtons.Add(ButtonFlags.B3, Color.FromArgb(255, 26, 159, 255));
                    ColoredButtons.Add(ButtonFlags.B4, Color.FromArgb(255, 255, 200, 44));
                    break;

                case GamepadType.PS3:
                case GamepadType.PS4:
                case GamepadType.PS5:
                    ColoredButtons.Add(ButtonFlags.B1, Color.FromArgb(255, 116, 139, 255));
                    ColoredButtons.Add(ButtonFlags.B2, Color.FromArgb(255, 255, 73, 75));
                    ColoredButtons.Add(ButtonFlags.B3, Color.FromArgb(255, 244, 149, 193));
                    ColoredButtons.Add(ButtonFlags.B4, Color.FromArgb(255, 73, 191, 115));
                    break;

                default:
                case GamepadType.Unknown:
                case GamepadType.Standard:
                case GamepadType.GameCube:
                case GamepadType.NintendoSwitchPro:
                    break;
            }
        }

        public GamepadType GamepadType => GetGamepadType(this.gamepad);

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);
        }

        protected override void InitializeInputOutput()
        {
            // Additional controller specific source buttons
            if (HasTouchpad)
            {
                int touchpads = GetTouchpads();
                int touchpadFingers = 0;

                if (touchpads >= 1)
                {
                    touchpadFingers = GetTouchpadFingers(0);

                    SourceButtons.Add(ButtonFlags.LeftPadClick);
                    SourceButtons.Add(ButtonFlags.LeftPadTouch);
                    SourceAxis.Add(AxisLayoutFlags.LeftPad);

                    if (IsVirtual())
                    {
                        TargetButtons.Add(ButtonFlags.LeftPadClick);
                        TargetButtons.Add(ButtonFlags.LeftPadTouch);
                        TargetAxis.Add(AxisLayoutFlags.LeftPad);
                    }
                }

                if (touchpads >= 2 || touchpadFingers >= 2)
                {
                    SourceButtons.Add(ButtonFlags.RightPadClick);
                    SourceButtons.Add(ButtonFlags.RightPadTouch);
                    SourceAxis.Add(AxisLayoutFlags.RightPad);

                    if (IsVirtual())
                    {
                        TargetButtons.Add(ButtonFlags.RightPadClick);
                        TargetButtons.Add(ButtonFlags.RightPadTouch);
                        TargetAxis.Add(AxisLayoutFlags.RightPad);
                    }
                }
            }

            if (HasGyro)
            {
                SourceAxis.Add(AxisLayoutFlags.Gyroscope);
            }
        }

        ~SDLController()
        {
            Dispose();
        }

        public override void Dispose()
        {
            Unplug();

            base.Dispose();
        }

        public override string ToString()
        {
            string? SDLName = GetGamepadName(gamepad);
            if (!string.IsNullOrEmpty(SDLName))
                return SDLName;

            return base.ToString();
        }

        public override void Plug()
        {
            if (!IsConnected())
                return;

            base.Plug();
        }

        private bool touchpad = false;
        private static readonly Dictionary<GamepadButton, ButtonFlags> _buttonMap = new()
        {
            [GamepadButton.North] = ButtonFlags.B4,
            [GamepadButton.South] = ButtonFlags.B1,
            [GamepadButton.West] = ButtonFlags.B3,
            [GamepadButton.East] = ButtonFlags.B2,
            [GamepadButton.DPadUp] = ButtonFlags.DPadUp,
            [GamepadButton.DPadDown] = ButtonFlags.DPadDown,
            [GamepadButton.DPadLeft] = ButtonFlags.DPadLeft,
            [GamepadButton.DPadRight] = ButtonFlags.DPadRight,
            [GamepadButton.Start] = ButtonFlags.Start,
            [GamepadButton.Back] = ButtonFlags.Back,
            [GamepadButton.LeftShoulder] = ButtonFlags.L1,
            [GamepadButton.RightShoulder] = ButtonFlags.R1,
            [GamepadButton.Guide] = ButtonFlags.Special,
            [GamepadButton.LeftStick] = ButtonFlags.LeftStickClick,
            [GamepadButton.RightStick] = ButtonFlags.RightStickClick,
        };

        // Hack, SDL_CS has wrong implementation ?
        [DllImport("SDL3", EntryPoint = "SDL_GetGamepadSensorData", ExactSpelling = true)]
        internal static extern unsafe sbyte SDL_GetGamepadSensorData(nint gamepad, SensorType type, float* data, int numValues);

        public override void Tick(long ticks, float delta, bool commit)
        {
            if (!IsConnected() || Inputs is null || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
                return;

            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

            // --- BUTTONS ---
            foreach (KeyValuePair<GamepadButton, ButtonFlags> kvp in _buttonMap)
            {
                GamepadButton sdlBtn = kvp.Key;
                ButtonFlags flag = kvp.Value;

                bool down = HasButton(sdlBtn) && GetGamepadButton(gamepad, sdlBtn);
                down |= InjectedButtons[flag];
                Inputs.ButtonState[flag] = down;
            }

            // touchpad button (edge-case)
            if (HasButton(GamepadButton.Touchpad))
                touchpad = GetGamepadButton(gamepad, GamepadButton.Touchpad);

            // --- AXES / TRIGGERS ---
            if (HasAxis(GamepadAxis.LeftX))
                Inputs.AxisState[AxisFlags.LeftStickX] = GetGamepadAxis(gamepad, GamepadAxis.LeftX);

            if (HasAxis(GamepadAxis.LeftY))
                Inputs.AxisState[AxisFlags.LeftStickY] = (short)InputUtils.MapRange(
                    GetGamepadAxis(gamepad, GamepadAxis.LeftY),
                    short.MinValue, short.MaxValue,
                    short.MaxValue, short.MinValue);

            if (HasAxis(GamepadAxis.RightX))
                Inputs.AxisState[AxisFlags.RightStickX] = GetGamepadAxis(gamepad, GamepadAxis.RightX);

            if (HasAxis(GamepadAxis.RightY))
                Inputs.AxisState[AxisFlags.RightStickY] = (short)InputUtils.MapRange(
                    GetGamepadAxis(gamepad, GamepadAxis.RightY),
                    short.MinValue, short.MaxValue,
                    short.MaxValue, short.MinValue);

            if (HasAxis(GamepadAxis.LeftTrigger))
            {
                Inputs.AxisState[AxisFlags.L2] = (byte)InputUtils.MapRange(
                    GetGamepadAxis(gamepad, GamepadAxis.LeftTrigger),
                    ushort.MinValue, short.MaxValue,
                    byte.MinValue, byte.MaxValue);

                Inputs.ButtonState[ButtonFlags.L2Soft] |= Inputs.AxisState[AxisFlags.L2] > TriggerThreshold;
                Inputs.ButtonState[ButtonFlags.L2Full] |= Inputs.AxisState[AxisFlags.L2] > TriggerThreshold * 2;
            }

            if (HasAxis(GamepadAxis.RightTrigger))
            {
                Inputs.AxisState[AxisFlags.R2] = (byte)InputUtils.MapRange(
                    GetGamepadAxis(gamepad, GamepadAxis.RightTrigger),
                    ushort.MinValue, short.MaxValue,
                    byte.MinValue, byte.MaxValue);

                Inputs.ButtonState[ButtonFlags.R2Soft] |= Inputs.AxisState[AxisFlags.R2] > TriggerThreshold;
                Inputs.ButtonState[ButtonFlags.R2Full] |= Inputs.AxisState[AxisFlags.R2] > TriggerThreshold * 2;
            }

            // --- TOUCHPADS ---
            if (HasTouchpad)
            {
                // left pad: touchpad 0, finger 0
                if (GetGamepadTouchpadFinger(gamepad, 0, 0, out bool lDown, out float lx, out float ly, out float lp))
                {
                    Inputs.ButtonState[ButtonFlags.LeftPadTouch] = lDown;
                    Inputs.AxisState[AxisFlags.LeftPadX] = (short)InputUtils.MapRange(lx, 0.0f, 1.0f, short.MinValue, short.MaxValue);
                    Inputs.AxisState[AxisFlags.LeftPadY] = (short)InputUtils.MapRange(ly, 1.0f, 0.0f, short.MinValue, short.MaxValue);
                }

                // right pad: either (pad 0, finger 1) or (pad 1, finger 0/1)
                bool rOk =
                    GetGamepadTouchpadFinger(gamepad, 0, 1, out bool rDown, out float rx, out float ry, out float rp) ||
                    GetGamepadTouchpadFinger(gamepad, 1, 0, out rDown, out rx, out ry, out rp) ||
                    GetGamepadTouchpadFinger(gamepad, 1, 1, out rDown, out rx, out ry, out rp);

                if (rOk)
                {
                    Inputs.ButtonState[ButtonFlags.RightPadTouch] = rDown;
                    Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange(rx, 0.0f, 1.0f, short.MinValue, short.MaxValue);
                    Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange(ry, 1.0f, 0.0f, short.MinValue, short.MaxValue);
                }

                // clicks follow: touchpad-button && respective touch
                Inputs.ButtonState[ButtonFlags.LeftPadClick] = touchpad && Inputs.ButtonState[ButtonFlags.LeftPadTouch];
                Inputs.ButtonState[ButtonFlags.RightPadClick] = touchpad && Inputs.ButtonState[ButtonFlags.RightPadTouch];

                // zero axes when not touched
                if (!Inputs.ButtonState[ButtonFlags.LeftPadTouch])
                {
                    Inputs.AxisState[AxisFlags.LeftPadX] = 0;
                    Inputs.AxisState[AxisFlags.LeftPadY] = 0;
                }
                if (!Inputs.ButtonState[ButtonFlags.RightPadTouch])
                {
                    Inputs.AxisState[AxisFlags.RightPadX] = 0;
                    Inputs.AxisState[AxisFlags.RightPadY] = 0;
                }
            }

            float aX = 0, aY = 0, aZ = 0, gX = 0, gY = 0, gZ = 0;

            unsafe
            {
                if (HasAccel)
                {
                    Span<float> acc = stackalloc float[3];
                    fixed (float* p = acc)
                    {
                        if (SDL_GetGamepadSensorData(gamepad, SensorType.Accel, p, 3) != 0)
                        {
                            aX = acc[0] / 40.0f * 4.0f;
                            aY = acc[1] / 40.0f * 4.0f;
                            aZ = acc[2] / 40.0f * 4.0f;
                        }
                    }
                }

                if (HasGyro)
                {
                    Span<float> gyr = stackalloc float[3];
                    fixed (float* p = gyr)
                    {
                        if (SDL_GetGamepadSensorData(gamepad, SensorType.Gyro, p, 3) != 0)
                        {
                            gX = gyr[0] / 40.0f * 2000.0f;
                            gY = gyr[1] / 40.0f * 2000.0f;
                            gZ = gyr[2] / 40.0f * 2000.0f;
                        }
                    }
                }
            }

            // store motion
            Inputs.GyroState.SetGyroscope(gX, gY, gZ);
            Inputs.GyroState.SetAccelerometer(aX, aY, aZ);

            // process motion
            if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion))
                gamepadMotion.ProcessMotion(gX, gY, gZ, aX, aY, aZ, delta);

            base.Tick(ticks, delta);
        }

        public override void Unplug()
        {
            if (!IsConnected())
                return;

            base.Unplug();
        }

        public override void SetVibration(byte LargeMotor, byte SmallMotor)
        {
            if (!HasRumble)
                return;

            RumbleGamepad(
                gamepad,
                (ushort)InputUtils.MapRange((float)(LargeMotor * VibrationStrength), byte.MinValue, byte.MaxValue, ushort.MinValue, ushort.MaxValue),
                (ushort)InputUtils.MapRange((float)(SmallMotor * VibrationStrength), byte.MinValue, byte.MaxValue, ushort.MinValue, ushort.MaxValue),
                1000);
        }

        public override void SetLightColor(byte R, byte G, byte B)
        {
            if (!HasRGBLed)
                return;

            SetGamepadLED(gamepad, R, G, B);

            base.SetLightColor(R, G, B);
        }
    }
}
