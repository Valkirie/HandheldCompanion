using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SDL3;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Gaming.Input;
using static SDL3.SDL;
using ThreadPriority = System.Threading.ThreadPriority;

namespace HandheldCompanion.Controllers
{
    public class SDLController : IController
    {
        public nint gamepad = IntPtr.Zero;
        public uint deviceIndex = 0;
        public uint deviceProperties => GetGamepadProperties(this.gamepad);

        public override bool IsConnected() => GamepadConnected(gamepad);

        private bool HasGyro => GamepadHasSensor(this.gamepad, SensorType.Gyro);
        private bool HasAccel => GamepadHasSensor(this.gamepad, SensorType.Accel);
        private bool HasMotion => HasGyro || HasAccel;

        private bool HasMonoLed => GetBooleanProperty(deviceProperties, Props.GamepadCapMonoLedBoolean, false);
        private bool HasRGBLed => GetBooleanProperty(deviceProperties, Props.GamepadCapRGBLedBoolean, false);
        private bool HasPlayerLed => GetBooleanProperty(deviceProperties, Props.GamepadCapPlayerLedBoolean, false);
        private bool HasRumble => GetBooleanProperty(deviceProperties, Props.GamepadCapRumbleBoolean, false);
        private bool HasTriggerRumble => GetBooleanProperty(deviceProperties, Props.GamepadCapTriggerRumbleBoolean, false);

        public override bool IsWireless() => GetGamepadConnectionState(gamepad) == JoystickConnectionState.Wireless;

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
            this.UserIndex = (byte)GetGamepadPlayerIndex(gamepad);

            // prepare sensor
            SetGamepadSensorEnabled(gamepad, SensorType.Gyro, HasGyro);
            SetGamepadSensorEnabled(gamepad, SensorType.Accel, HasAccel);

            // Capabilities
            Capabilities |= HasMotion ? ControllerCapabilities.MotionSensor : ControllerCapabilities.None;
            Capabilities |= HasRumble ? ControllerCapabilities.Rumble : ControllerCapabilities.None;

            AttachDetails(details);
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);
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
                                    float x = e.GSensor.Data[0] / 40.0f * 4.0f;
                                    float y = e.GSensor.Data[1] / 40.0f * 4.0f;
                                    float z = e.GSensor.Data[2] / 40.0f * 4.0f;
                                    Inputs.GyroState.SetAccelerometer(x, y, z);
                                }
                                break;

                            case SensorType.Gyro:
                                unsafe
                                {
                                    float x = e.GSensor.Data[0] / 40.0f * 2000.0f;
                                    float y = e.GSensor.Data[1] / 40.0f * 2000.0f;
                                    float z = e.GSensor.Data[2] / 40.0f * 2000.0f;
                                    Inputs.GyroState.SetGyroscope(x, y, z);
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
                        break;
                    }

                case EventType.GamepadUpdateComplete:
                    // implement me
                    break;

                case EventType.GamepadTouchpadDown:
                    // implement me
                    break;

                case EventType.GamepadTouchpadUp:
                    // implement me
                    break;

                case EventType.GamepadTouchpadMotion:
                    // implement me
                    break;

                default:
                    break;
            }

            // compute delta (ms)
            ulong now = GetPerformanceCounter();
            ulong tickDelta = now - lastCounter;
            float deltaMillis = (float)tickDelta / freq;

            // process motion
            if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion))
                gamepadMotion.ProcessMotion(
                    Inputs.GyroState.Gyroscope[GyroState.SensorState.GamepadMotion].X,
                    Inputs.GyroState.Gyroscope[GyroState.SensorState.GamepadMotion].Y,
                    Inputs.GyroState.Gyroscope[GyroState.SensorState.GamepadMotion].Z,
                    Inputs.GyroState.Accelerometer[GyroState.SensorState.GamepadMotion].X,
                    Inputs.GyroState.Accelerometer[GyroState.SensorState.GamepadMotion].Y,
                    Inputs.GyroState.Accelerometer[GyroState.SensorState.GamepadMotion].Z,
                    deltaMillis);

            // update previous counter
            lastCounter = now;

            base.UpdateInputs(TimerManager.GetTimestamp(), deltaMillis);
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
    }
}
