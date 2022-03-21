using ControllerCommon;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using GamepadButtonFlags = ControllerCommon.Utils.GamepadButtonFlags;

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
        public SensorFusion sensorFusion;

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

            // initialize sensorfusion
            sensorFusion = new SensorFusion(logger);

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
            GamepadButtonFlags buttons = (GamepadButtonFlags)Gamepad.Buttons;
            buttons |= (Gamepad.LeftTrigger > 0 ? GamepadButtonFlags.LeftTrigger : 0);
            buttons |= (Gamepad.RightTrigger > 0 ? GamepadButtonFlags.RightTrigger : 0);

            // get custom buttons values
            buttons |= xinputController.profile.umc_trigger.HasFlag(GamepadButtonFlags.AlwaysOn) ? GamepadButtonFlags.AlwaysOn : 0;

            // get sticks values
            LeftThumb = new Vector2(Gamepad.LeftThumbX, Gamepad.LeftThumbY);
            RightThumb = new Vector2(Gamepad.RightThumbX, Gamepad.RightThumbY);

            // update sensorFusion (todo: call only when needed ?)
            sensorFusion.UpdateReport(xinputController.totalmilliseconds, xinputController.AngularVelocity, xinputController.Acceleration);

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
                                        Angular = new Vector2((float)sensorFusion.CameraYawDelta, (float)sensorFusion.CameraPitchDelta);
                                        break;

                                    default:
                                    case Input.JoystickCamera:
                                        Angular = new Vector2(-xinputController.AngularUniversal.Z, xinputController.AngularUniversal.X);
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
                                        RightThumb.X = (short)(Math.Clamp(RightThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                        RightThumb.Y = (short)(Math.Clamp(RightThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));
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
                                    sensorFusion.DeviceAngle.Y,
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
