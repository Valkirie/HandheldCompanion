using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
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

        public override void InjectState(ButtonState State, bool IsKeyDown, bool IsKeyUp)
        {
            base.InjectState(State, IsKeyDown, IsKeyUp);
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);
        }

        public override void InjectButton(ButtonFlags button, bool IsKeyDown, bool IsKeyUp)
        {
            base.InjectButton(button, IsKeyDown, IsKeyUp);
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);
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

        public void PumpEvent(Event e)
        {
            switch ((EventType)e.Type)
            {
                case EventType.Quit:
                    // implement me
                    break;

                case EventType.GamepadAxisMotion:
                    {
                        switch ((GamepadAxis)e.GAxis.Axis)
                        {
                            // Left joystick
                            case GamepadAxis.LeftX:
                                Inputs.AxisState[AxisFlags.LeftStickX] = e.GAxis.Value;
                                break;
                            case GamepadAxis.LeftY:
                                Inputs.AxisState[AxisFlags.LeftStickY] = (short)InputUtils.MapRange(
                                    e.GAxis.Value,
                                    short.MinValue,
                                    short.MaxValue,
                                    short.MaxValue,
                                    short.MinValue);
                                break;

                            // Right joystick
                            case GamepadAxis.RightX:
                                Inputs.AxisState[AxisFlags.RightStickX] = e.GAxis.Value;
                                break;
                            case GamepadAxis.RightY:
                                Inputs.AxisState[AxisFlags.RightStickY] = (short)InputUtils.MapRange(
                                    e.GAxis.Value,
                                    short.MinValue,
                                    short.MaxValue,
                                    short.MaxValue,
                                    short.MinValue);
                                break;

                            // Triggers
                            case GamepadAxis.LeftTrigger:
                                Inputs.AxisState[AxisFlags.L2] = (byte)InputUtils.MapRange(
                                    e.GAxis.Value,
                                    ushort.MinValue,
                                    short.MaxValue,
                                    byte.MinValue,
                                    byte.MaxValue);
                                break;
                            case GamepadAxis.RightTrigger:
                                Inputs.AxisState[AxisFlags.R2] = (byte)InputUtils.MapRange(
                                    e.GAxis.Value,
                                    ushort.MinValue,
                                    short.MaxValue,
                                    byte.MinValue,
                                    byte.MaxValue);
                                break;
                        }
                    }
                    break;

                case EventType.GamepadSensorUpdate:
                    {
                        switch ((SensorType)e.GSensor.Sensor)
                        {
                            case SensorType.Accel:
                                unsafe
                                {
                                    aX = e.GSensor.Data[0] / 40.0f * 4.0f;
                                    aY = e.GSensor.Data[1] / 40.0f * 4.0f;
                                    aZ = e.GSensor.Data[2] / 40.0f * 4.0f;
                                }
                                break;

                            case SensorType.Gyro:
                                unsafe
                                {
                                    gX = e.GSensor.Data[0] / 40.0f * 2000.0f;
                                    gY = e.GSensor.Data[1] / 40.0f * 2000.0f;
                                    gZ = e.GSensor.Data[2] / 40.0f * 2000.0f;
                                }
                                break;
                        }
                    }
                    break;

                case EventType.GamepadButtonDown:
                case EventType.GamepadButtonUp:
                    {
                        bool isDown = (EventType)e.Type == EventType.GamepadButtonDown;
                        GamepadButton gpBtn = (GamepadButton)e.GButton.Button;

                        if (_buttonMap.TryGetValue(gpBtn, out var flag))
                            Inputs.ButtonState[flag] = isDown;

                        // edge-case(s)
                        switch (gpBtn)
                        {
                            // lifting one finger while the pad is still clicked will unclick it..
                            // todo: store me in a bool so we can compute all vars at once later in the code
                            case GamepadButton.Touchpad:
                                touchpad = isDown;
                                break;
                        }

                        break;
                    }

                case EventType.GamepadUpdateComplete:
                    // implement me
                    break;

                case EventType.GamepadTouchpadDown:
                case EventType.GamepadTouchpadUp:
                case EventType.GamepadTouchpadMotion:
                    {
                        switch (e.GTouchpad.Touchpad)
                        {
                            case 0:
                                {
                                    switch (e.GTouchpad.Finger)
                                    {
                                        case 0:
                                            Inputs.ButtonState[ButtonFlags.LeftPadTouch] = e.GTouchpad.Pressure == 1 ? true : false;
                                            Inputs.AxisState[AxisFlags.LeftPadX] = (short)InputUtils.MapRange(e.GTouchpad.X, 0.0f, 1.0f, short.MinValue, short.MaxValue);
                                            Inputs.AxisState[AxisFlags.LeftPadY] = (short)InputUtils.MapRange(e.GTouchpad.Y, 1.0f, 0.0f, short.MinValue, short.MaxValue);
                                            break;
                                        case 1:
                                            Inputs.ButtonState[ButtonFlags.RightPadTouch] = e.GTouchpad.Pressure == 1 ? true : false;
                                            Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange(e.GTouchpad.X, 0.0f, 1.0f, short.MinValue, short.MaxValue);
                                            Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange(e.GTouchpad.Y, 1.0f, 0.0f, short.MinValue, short.MaxValue);
                                            break;
                                    }
                                }
                                break;

                            case 1:
                                {
                                    switch (e.GTouchpad.Finger)
                                    {
                                        case 1:
                                        case 0:
                                            Inputs.ButtonState[ButtonFlags.RightPadTouch] = e.GTouchpad.Pressure == 1 ? true : false;
                                            Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange(e.GTouchpad.X, 0.0f, 1.0f, short.MinValue, short.MaxValue);
                                            Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange(e.GTouchpad.Y, 1.0f, 0.0f, short.MinValue, short.MaxValue);
                                            break;
                                    }
                                }
                                break;
                        }
                    }
                    break;

                default:
                    break;
            }

            // touch management
            Inputs.ButtonState[ButtonFlags.LeftPadClick] = touchpad && Inputs.ButtonState[ButtonFlags.LeftPadTouch];
            Inputs.ButtonState[ButtonFlags.RightPadClick] = touchpad && Inputs.ButtonState[ButtonFlags.RightPadTouch];

            // we need to reset the axis
            // todo: make layout manager more resilient so this is not needed
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

            // compute delta (ms)
            ulong now = GetPerformanceCounter();
            ulong tickDelta = now - lastCounter;
            float deltaMillis = (float)tickDelta / freq;

            // store motion
            Inputs.GyroState.SetGyroscope(gX, gY, gZ);
            Inputs.GyroState.SetAccelerometer(aX, aY, aZ);

            // process motion
            if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion))
                gamepadMotion.ProcessMotion(gX, gY, gZ, aX, aY, aZ, deltaMillis);

            // update previous counter
            lastCounter = now;

            base.Tick(TimerManager.GetTimestamp(), deltaMillis);
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
