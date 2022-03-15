using ControllerCommon;
using ControllerService.Sensors;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Runtime.InteropServices;
using GamepadButtonFlags = ControllerCommon.GamepadButtonFlags;

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

        protected short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;

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
            return Utils.GetDescriptionFromEnumValue(this.HID);
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
            LeftThumbX = Gamepad.LeftThumbX;
            LeftThumbY = Gamepad.LeftThumbY;
            RightThumbX = Gamepad.RightThumbX;
            RightThumbY = Gamepad.RightThumbY;

            // update sensorFusion (todo: call only when needed ?)
            sensorFusion.UpdateReport(xinputController.totalmilliseconds, xinputController.AngularVelocity, xinputController.Acceleration);

            if (xinputController.profile.umc_enabled && (xinputController.profile.umc_trigger & buttons) != 0)
            {
                switch (xinputController.profile.umc_input)
                {
                    case Input.PlayerSpace:
                    case Input.JoystickCamera:
                        {
                            float AngularX, AngularY = 0.0f;

                            switch (xinputController.profile.umc_input)
                            {
                                case Input.PlayerSpace:
                                    AngularX = (float)sensorFusion.CameraYawDelta;
                                    AngularY = (float)sensorFusion.CameraPitchDelta;
                                    break;

                                default:
                                case Input.JoystickCamera:
                                    AngularX = -xinputController.AngularUniversal.Z;
                                    AngularY = xinputController.AngularUniversal.X;
                                    break;
                            }

                            // apply sensivity curve
                            AngularX *= Utils.ApplyCustomSensitivity(AngularX, XInputGirometer.sensorSpec.maxIn, xinputController.profile.aiming_array);
                            AngularY *= Utils.ApplyCustomSensitivity(AngularY, XInputGirometer.sensorSpec.maxIn, xinputController.profile.aiming_array);

                            // apply sensivity
                            float GamepadThumbX = AngularX * xinputController.profile.GetSensiviy();
                            float GamepadThumbY = AngularY * xinputController.profile.GetSensiviy();

                            switch (xinputController.profile.umc_output)
                            {
                                default:
                                case Output.RightStick:
                                    RightThumbX = (short)(Math.Clamp(RightThumbX + GamepadThumbX, short.MinValue, short.MaxValue));
                                    RightThumbY = (short)(Math.Clamp(RightThumbY + GamepadThumbY, short.MinValue, short.MaxValue));
                                    break;
                                case Output.LeftStick:
                                    LeftThumbX = (short)(Math.Clamp(LeftThumbX + GamepadThumbX, short.MinValue, short.MaxValue));
                                    LeftThumbY = (short)(Math.Clamp(LeftThumbY + GamepadThumbY, short.MinValue, short.MaxValue));
                                    break;
                            }
                        }
                        break;

                    case Input.JoystickSteering:
                        {
                            // Todo, need to double check sensor fusion device angle is not inverted!
                            float GamepadThumbX = Utils.Steering(
                                sensorFusion.DeviceAngle.Y,
                                xinputController.profile.steering_max_angle,
                                xinputController.profile.steering_power,
                                xinputController.profile.steering_deadzone,
                                xinputController.profile.steering_deadzone_compensation);

                            switch (xinputController.profile.umc_output)
                            {
                                default:
                                case Output.RightStick:
                                    RightThumbX = (short)GamepadThumbX;
                                    break;
                                case Output.LeftStick:
                                    LeftThumbX = (short)GamepadThumbX;
                                    break;
                            }
                        }
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
