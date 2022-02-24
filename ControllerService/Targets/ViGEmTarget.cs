using ControllerCommon;
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

        public MadgwickAHRS madgwick;

        protected ViGEmClient client { get; }
        protected IVirtualGamepad virtualController;

        protected XInputStateSecret state_s;

        protected float vibrationStrength;

        protected int UserIndex;

        protected short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;
        public Timer UpdateTimer;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        protected object updateLock = new();

        protected ViGEmTarget(XInputController xinput, ViGEmClient client, Controller controller, int index, ILogger logger)
        {
            this.logger = logger;
            this.xinputController = xinput;

            // initialize madgwick
            madgwick = new(1f / 14f, 0.1f);

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

        public void SetVibrationStrength(float strength)
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

            Connected?.Invoke(this);
            logger.LogInformation("Virtual {0} connected", ToString());
        }

        public virtual void Disconnect()
        {
            UpdateTimer.Enabled = false;
            UpdateTimer.Stop();

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
                    float intensity = xinputController.profile.GetIntensity();
                    float sensivity = xinputController.profile.GetSensiviy();

                    switch (xinputController.profile.umc_input)
                    {
                        // TODO @Benjamin Switch case or if statements for style of input (Joystick Move or Steering)

                        default:
                        case InputStyle.RightStick:
                            RightThumbX = Utils.ComputeInput(RightThumbX, -xinputController.AngularVelocity.Z, sensivity, intensity);
                            RightThumbY = Utils.ComputeInput(RightThumbY, xinputController.AngularVelocity.X, sensivity, intensity);
                            break;
                        case InputStyle.LeftStick:
                            LeftThumbX = Utils.ComputeInput(LeftThumbX, -xinputController.AngularVelocity.Z, sensivity, intensity);
                            LeftThumbY = Utils.ComputeInput(LeftThumbY, xinputController.AngularVelocity.X, sensivity, intensity);

                            // TODO @Benjamin Remove/update/replace with new GUI profile variables, see notes on needed sliders
                            // TODO @Benjamin Perhaps use a struct for the profile options?
                            float MaxDeviceAngle = 30; // Max steering angle 10 to 80 degrees in 5 degree increments, default 35 degrees
                            float ToThePowerOf = 1; // 0.1 to 5 in 0.1 increments, default 1.0 (lineair)
                            float DeadzoneAngle = 2; // 0 to 5 degrees in 1 degree increments, default 0 degrees
                            float DeadzoneCompensation = 0; // 0 to 100 %, in 1% increments, default 0 %

                            LeftThumbX = Utils.Steering(xinputController.Angle.Y, 
                                                        MaxDeviceAngle,
                                                        ToThePowerOf,
                                                        DeadzoneAngle,
                                                        DeadzoneCompensation);
                            LeftThumbY = 0;

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
