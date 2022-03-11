using ControllerCommon;
using ControllerService.Sensors;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Runtime.InteropServices;
using System.Timers;
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

        public Gamepad Gamepad;
        public HIDmode HID = HIDmode.None;

        protected readonly ILogger logger;

        protected ViGEmClient client { get; }
        protected IVirtualGamepad virtualController;

        protected XInputStateSecret state_s;

        protected double vibrationStrength;

        protected int UserIndex;

        protected short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;
        public Timer UpdateTimer;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        protected object updateLock = new();
        protected bool isConnected;

        protected ViGEmTarget(XInputController xinput, ViGEmClient client, Controller controller, int index, ILogger logger)
        {
            this.logger = logger;
            this.xinputController = xinput;

            // initialize secret state
            state_s = new();

            // initialize controller
            this.client = client;
            this.physicalController = controller;

            // initialize timers
            UpdateTimer = new Timer() { Enabled = false, AutoReset = true };
        }

        protected void FeedbackReceived(object sender, EventArgs e)
        {
        }

        public void SetPollRate(int HIDrate)
        {
            UpdateTimer.Interval = HIDrate;
            logger.LogInformation("Virtual {0} report interval set to {1}ms", this, HIDrate);
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
            UpdateTimer.Enabled = true;
            UpdateTimer.Start();

            isConnected = true;
            Connected?.Invoke(this);
            logger.LogInformation("Virtual {0} connected", ToString());
        }

        public virtual void Disconnect()
        {
            UpdateTimer.Enabled = false;
            UpdateTimer.Stop();

            isConnected = false;
            Disconnected?.Invoke(this);
            logger.LogInformation("Virtual {0} disconnected", ToString());
        }

        public virtual unsafe void UpdateReport(object sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                // get current gamepad state
                XInputGetStateSecret13(UserIndex, out state_s);
                State state = physicalController.GetState();
                Gamepad = state.Gamepad;

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

                if (xinputController.profile.umc_enabled && (xinputController.profile.umc_trigger & buttons) != 0)
                {
                    switch (xinputController.profile.umc_input)
                    {
                        case Input.JoystickCamera:
                            {
                                float AngularX = -xinputController.AngularUniversal.Z;
                                float AngularY = xinputController.AngularUniversal.X;

                                // apply sensivity curve
                                AngularX *= Utils.ApplyCustomSensitivity(AngularX, XInputGirometer.sensorSpec.maxIn, xinputController.profile.aiming_array);
                                AngularY *= Utils.ApplyCustomSensitivity(AngularY, XInputGirometer.sensorSpec.maxIn, xinputController.profile.aiming_array);

                                // get profile vars
                                float intensity = xinputController.profile.GetIntensity();
                                float sensivity = xinputController.profile.GetSensiviy();

                                // apply sensivity, intensity sliders (deprecated ?)
                                float GamepadThumbX = Utils.ComputeInput(AngularX, sensivity, intensity, XInputGirometer.sensorSpec.maxIn);
                                float GamepadThumbY = Utils.ComputeInput(AngularY, sensivity, intensity, XInputGirometer.sensorSpec.maxIn);

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
                                float GamepadThumbX = Utils.Steering(
                                    xinputController.Angle.Y,
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
        }

        internal void SubmitReport()
        {
            // do something
        }
    }
}
