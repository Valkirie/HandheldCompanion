using ControllerCommon;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using GamepadButtonFlagsExt = ControllerCommon.Utils.GamepadButtonFlagsExt;

namespace ControllerService.Targets
{
    public abstract class ViGEmTarget
    {
        #region imports
        [StructLayout(LayoutKind.Sequential)]
        protected struct XInputStateSecret
        {
            public uint eventCount;
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [DllImport("xinput1_3.dll", EntryPoint = "#100")]
        protected static extern int XInputGetStateSecret13(int playerIndex, out XInputStateSecret struc);
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        protected static extern int XInputGetStateSecret14(int playerIndex, out XInputStateSecret struc);
        #endregion

        public Controller physicalController;
        public XInputController xinputController;
        public FlickStick flickStick;

        public HIDmode HID = HIDmode.None;

        protected readonly ILogger logger;

        protected ViGEmClient client { get; }
        protected IVirtualGamepad virtualController;

        protected XInputStateSecret state_s;

        protected double vibrationStrength;

        protected int UserIndex;

        protected Vector2 LeftThumb;
        protected Vector2 RightThumb;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        protected bool IsConnected;

        protected ViGEmTarget(XInputController xinput, ViGEmClient client, Controller controller, int index, ILogger logger)
        {
            this.logger = logger;
            this.xinputController = xinput;

            // initialize flick stick
            flickStick = new FlickStick(logger);

            // initialize secret state
            state_s = new();

            // initialize controller
            this.client = client;
            this.physicalController = controller;
        }

        protected void FeedbackReceived(object sender, EventArgs e)
        {
        }

        public void SetVibrationStrength(double strength)
        {
            vibrationStrength = strength / 100.0f;
            logger.LogInformation("Virtual {0} vibration strength set to {1}%", this, strength);
        }

        public override string ToString()
        {
            return EnumUtils.GetDescriptionFromEnumValue(this.HID);
        }

        public virtual void Connect()
        {
            IsConnected = true;
            Connected?.Invoke(this);
            logger.LogInformation("Virtual {0} connected", ToString());
        }

        public virtual void Disconnect()
        {
            IsConnected = false;
            Disconnected?.Invoke(this);
            logger.LogInformation("Virtual {0} disconnected", ToString());
        }


        public virtual unsafe void UpdateReport(Gamepad Gamepad)
        {
            // get current gamepad state
            XInputGetStateSecret13(UserIndex, out state_s);

            // get buttons values
            GamepadButtonFlagsExt buttons = (GamepadButtonFlagsExt)Gamepad.Buttons;
            buttons |= (Gamepad.LeftTrigger > 0 ? GamepadButtonFlagsExt.LeftTrigger : 0);
            buttons |= (Gamepad.RightTrigger > 0 ? GamepadButtonFlagsExt.RightTrigger : 0);

            // get custom buttons values
            buttons |= xinputController.profile.umc_trigger.HasFlag(GamepadButtonFlagsExt.AlwaysOn) ? GamepadButtonFlagsExt.AlwaysOn : 0;

            // get sticks values
            LeftThumb = new Vector2(Gamepad.LeftThumbX, Gamepad.LeftThumbY);
            RightThumb = new Vector2(Gamepad.RightThumbX, Gamepad.RightThumbY);

            if (xinputController.profile.umc_enabled)
            {
                if ((xinputController.profile.umc_trigger & buttons) != 0)
                {
                    switch (xinputController.profile.umc_input)
                    {
                        case Input.PlayerSpace:
                        case Input.JoystickCamera:
                            {
                                Vector2 Angular;

                                switch (xinputController.profile.umc_input)
                                {
                                    case Input.PlayerSpace:
                                        Angular = new Vector2((float)xinputController.sensorFusion.CameraYawDelta, (float)xinputController.sensorFusion.CameraPitchDelta);
                                        break;

                                    default:
                                    case Input.JoystickCamera:
                                        Angular = new Vector2(-xinputController.AngularVelocityC.Z, xinputController.AngularVelocityC.X);
                                        break;
                                }

                                // apply sensivity curve
                                Angular.X *= InputUtils.ApplyCustomSensitivity(Angular.X, XInputGirometer.sensorSpec.maxIn, xinputController.profile.aiming_array);
                                Angular.Y *= InputUtils.ApplyCustomSensitivity(Angular.Y, XInputGirometer.sensorSpec.maxIn, xinputController.profile.aiming_array);

                                // apply sensivity
                                Vector2 GamepadThumb = new Vector2(
                                    Angular.X * xinputController.profile.GetSensiviy(),
                                    Angular.Y * xinputController.profile.GetSensiviy());

                                switch (xinputController.profile.umc_output)
                                {
                                    default:
                                    case Output.RightStick:

                                        if (xinputController.profile.flickstick_enabled)
                                        {
                                            // Flick Stick:
                                            // - Detect flicking
                                            // - Filter stick input
                                            // - Determine and compute either flick or stick output
                                            float FlickStickX = flickStick.Handle(RightThumb,
                                                                                  xinputController.profile.flick_duration,
                                                                                  xinputController.profile.stick_sensivity,
                                                                                  xinputController.TotalMilliseconds);

                                            // X input combines motion controls plus flick stick result
                                            // Y input only from motion controls
                                            RightThumb.X = (short)(Math.Clamp(GamepadThumb.X - FlickStickX, short.MinValue, short.MaxValue));
                                            RightThumb.Y = (short)(Math.Clamp(GamepadThumb.Y, short.MinValue, short.MaxValue));
                                        }
                                        else
                                        {
                                            RightThumb.X = (short)(Math.Clamp(RightThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                            RightThumb.Y = (short)(Math.Clamp(RightThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));
                                        }
                                        break;

                                    case Output.LeftStick:
                                        LeftThumb.X = (short)(Math.Clamp(LeftThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                        LeftThumb.Y = (short)(Math.Clamp(LeftThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));
                                        break;
                                }
                            }
                            break;

                        case Input.JoystickSteering:
                            {
                                float GamepadThumbX = InputUtils.Steering(
                                    xinputController.sensorFusion.DeviceAngle.Y,
                                    xinputController.profile.steering_max_angle,
                                    xinputController.profile.steering_power,
                                    xinputController.profile.steering_deadzone);

                                switch (xinputController.profile.umc_output)
                                {
                                    default:
                                    case Output.RightStick:
                                        RightThumb.X = (short)GamepadThumbX;

                                        break;
                                    case Output.LeftStick:
                                        LeftThumb.X = (short)GamepadThumbX;

                                        logger.LogInformation("Vibrate motor {0} {1}", xinputController.sensorFusion.DeviceAngle.Y, xinputController.profile.steering_max_angle);

                                        // Turn right is negative angle, turn left is postive angle
                                        // Default to 0
                                        // TODO take existing from game in to account
                                        // TODO take multiplier into account
                                        Vibration inputMotor = new()
                                        {
                                            LeftMotorSpeed = 0,
                                            RightMotorSpeed = 0,
                                        };

                                        if (xinputController.sensorFusion.DeviceAngle.Y > xinputController.profile.steering_max_angle)
                                        {
                                            inputMotor = new()
                                            {
                                                LeftMotorSpeed = (ushort)(ushort.MaxValue * 0.3),
                                                RightMotorSpeed = 0,
                                            };
                                            logger.LogInformation("Vibrate motor left");

                                        }

                                        if (xinputController.sensorFusion.DeviceAngle.Y < xinputController.profile.steering_max_angle)
                                        {
                                            inputMotor = new()
                                            {
                                                LeftMotorSpeed = 0,
                                                RightMotorSpeed = (ushort)(ushort.MaxValue * 0.3),
                                            };
                                            logger.LogInformation("Vibrate motor right");

                                        }

                                        // Within normal range
                                        if (Math.Abs(xinputController.sensorFusion.DeviceAngle.Y) < xinputController.profile.steering_max_angle)
                                        {
                                            inputMotor = new()
                                            {
                                                LeftMotorSpeed = 0,
                                                RightMotorSpeed = 0,
                                            };
                                            logger.LogInformation("No vibration, in abs max range");

                                        }

                                        physicalController.SetVibration(inputMotor);

                                        break;
                                }
                            }
                            break;
                    }
                }

                // Apply user defined in game deadzone setting compensation
                switch (xinputController.profile.umc_output)
                {
                    default:
                    case Output.RightStick:
                        RightThumb = InputUtils.ApplyAntiDeadzone(RightThumb, xinputController.profile.antideadzone);
                        break;
                    case Output.LeftStick:
                        LeftThumb = InputUtils.ApplyAntiDeadzone(LeftThumb, xinputController.profile.antideadzone);
                        break;
                }
            }
        }

        internal void SubmitReport()
        {
            // do something
        }
    }
}