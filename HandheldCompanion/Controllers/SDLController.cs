using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SDL3;
using SharpDX.XInput;
using System;
using System.Threading;
using Windows.Gaming.Input;
using static SDL3.SDL;
using ThreadPriority = System.Threading.ThreadPriority;

namespace HandheldCompanion.Controllers
{
    public class SDLController : IController
    {
        private nint gamepad = IntPtr.Zero;
        private uint deviceIndex = 0;

        public override bool IsConnected() => GamepadConnected(gamepad);

        private Thread pumpThread;
        private bool pumpThreadRunning;

        public SDLController()
        { }

        public SDLController(nint ctrl, uint deviceIndex, PnPDetails details)
        {
            if (details is null)
                throw new Exception("SDLController PnPDetails is null");

            this.gamepad = ctrl;
            this.deviceIndex = deviceIndex;
            this.UserIndex = (byte)GetGamepadPlayerIndex(ctrl);

            if (GamepadHasSensor(gamepad, SensorType.Gyro))
                SetGamepadSensorEnabled(ctrl, SensorType.Gyro, true);

            if (GamepadHasSensor(gamepad, SensorType.Accel))
                SetGamepadSensorEnabled(ctrl, SensorType.Accel, true);

            // Capabilities
            Capabilities |= GamepadHasSensor(gamepad, SensorType.Gyro) || GamepadHasSensor(gamepad, SensorType.Accel) ? ControllerCapabilities.MotionSensor : ControllerCapabilities.None;
            Capabilities |= ControllerCapabilities.Rumble;

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

            // manage pump thread
            pumpThreadRunning = true;
            pumpThread = new Thread(pumpThreadLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            pumpThread.Start();

            base.Plug();
        }

        private ulong lastCounter = GetPerformanceCounter();
        private readonly float freq = GetPerformanceFrequency();

        private void pumpThreadLoop(object? obj)
        {
            while (pumpThreadRunning)
            {
                if (WaitEventTimeout(out Event e, TimerManager.GetPeriod()))
                {
                    if (e.GDevice.Which != deviceIndex)
                        continue;

                    switch ((EventType)e.Type)
                    {
                        case EventType.Quit:
                            pumpThreadRunning = false;
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
                                switch((SensorType)e.GSensor.Sensor)
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
                            {
                                switch((GamepadButton)e.GButton.Button)
                                {
                                    case GamepadButton.North:
                                        Inputs.ButtonState[ButtonFlags.B4] = true;
                                        break;
                                    case GamepadButton.South:
                                        Inputs.ButtonState[ButtonFlags.B1] = true;
                                        break;
                                    case GamepadButton.West:
                                        Inputs.ButtonState[ButtonFlags.B3] = true;
                                        break;
                                    case GamepadButton.East:
                                        Inputs.ButtonState[ButtonFlags.B2] = true;
                                        break;
                                }
                            }
                            break;

                        case EventType.GamepadButtonUp:
                            {
                                switch ((GamepadButton)e.GButton.Button)
                                {
                                    case GamepadButton.North:
                                        Inputs.ButtonState[ButtonFlags.B4] = false;
                                        break;
                                    case GamepadButton.South:
                                        Inputs.ButtonState[ButtonFlags.B1] = false;
                                        break;
                                    case GamepadButton.West:
                                        Inputs.ButtonState[ButtonFlags.B3] = false;
                                        break;
                                    case GamepadButton.East:
                                        Inputs.ButtonState[ButtonFlags.B2] = false;
                                        break;
                                }
                            }
                            break;

                        case EventType.GamepadUpdateComplete:
                            break;

                        case EventType.JoystickAxisMotion:
                        case EventType.JoystickUpdateComplete:
                        case EventType.JoystickButtonDown:
                        case EventType.JoystickButtonUp:
                        case EventType.JoystickHatMotion:
                            break;

                        case EventType.GamepadTouchpadDown:
                            break;

                        case EventType.GamepadTouchpadUp:
                            break;

                        case EventType.GamepadTouchpadMotion:
                            break;

                        default:
                            break;
                    }

                    /*
                    Inputs.ButtonState[ButtonFlags.B1] = A;
                    Inputs.ButtonState[ButtonFlags.B2] = B;
                    Inputs.ButtonState[ButtonFlags.B3] = X;
                    Inputs.ButtonState[ButtonFlags.B4] = Y;
                    */

                    ulong now = GetPerformanceCounter();
                    ulong tickDelta = now - lastCounter;
                    lastCounter = now;

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

                    base.UpdateInputs(TimerManager.GetTimestamp(), deltaMillis);
                }
            }
        }

        public override void Unplug()
        {
            if (!IsConnected())
                return;

            // kill pump thread
            if (pumpThread is not null)
            {
                pumpThreadRunning = false;
                // Ensure the thread has finished execution
                if (pumpThread.IsAlive)
                    pumpThread.Join(3000);
                pumpThread = null;
            }

            base.Unplug();
        }

        public override void SetVibration(byte LargeMotor, byte SmallMotor)
        {
            RumbleGamepad(
                gamepad,
                (ushort)InputUtils.MapRange((float)(LargeMotor * VibrationStrength), byte.MinValue, byte.MaxValue, ushort.MinValue, ushort.MaxValue),
                (ushort)InputUtils.MapRange((float)(SmallMotor * VibrationStrength), byte.MinValue, byte.MaxValue, ushort.MinValue, ushort.MaxValue),
                1000);
        }
    }
}
