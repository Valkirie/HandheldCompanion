using ControllerCommon;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Numerics;
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
        public DS4Touch Touch;
        public HIDmode HID = HIDmode.None;

        protected readonly ILogger logger;

        private Vector3 prevAcceleration;
        public Vector3 Acceleration;
        public Timer AccelerationTimer;
        public void Accelerometer_ReadingChanged(XInputAccelerometer sender, Vector3 Acceleration)
        {
            this.Acceleration = Acceleration;

            AccelerationTimer?.Stop();
            AccelerationTimer?.Start();
        }

        private Vector3 prevAngularVelocity;
        public Vector3 AngularVelocity;
        public Timer AngularVelocityTimer;
        public void Girometer_ReadingChanged(XInputGirometer sender, Vector3 AngularVelocity)
        {
            this.AngularVelocity = AngularVelocity;

            AngularVelocityTimer?.Stop();
            AngularVelocityTimer?.Start();
        }

        public MadgwickAHRS madgwick;

        protected ViGEmClient client { get; }
        protected IVirtualGamepad virtualController;

        protected XInputStateSecret state_s;

        public long microseconds;
        protected float vibrationStrength;

        protected readonly Stopwatch stopwatch;
        protected int UserIndex;

        protected short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;
        public Timer UpdateTimer;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(ViGEmTarget target);

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        protected object updateLock = new();

        protected ViGEmTarget(XInputController xinput, ViGEmClient client, Controller controller, int index, ILogger logger)
        {
            this.logger = logger;
            this.xinputController = xinput;

            // initialize vectors
            AngularVelocity = new();
            Acceleration = new();
            madgwick = new(1f / 14f, 0.1f);

            // initialize touch
            Touch = new();

            // initialize secret state
            state_s = new();

            // initialize controller
            this.client = client;
            this.physicalController = controller;

            // initialize stopwatch
            stopwatch = new Stopwatch();

            // initialize timers
            UpdateTimer = new Timer() { Enabled = false, AutoReset = true };

            AccelerationTimer = new Timer() { Enabled = false, AutoReset = false };
            AccelerationTimer.Elapsed += AccelerationTimer_Elapsed;

            AngularVelocityTimer = new Timer() { Enabled = false, AutoReset = false };
            AngularVelocityTimer.Elapsed += AngularVelocityTimer_Elapsed;
        }

        protected void FeedbackReceived(object sender, EventArgs e)
        {
        }

        public void SetPollRate(int HIDrate)
        {
            UpdateTimer.Interval = HIDrate;
            AccelerationTimer.Interval = HIDrate * 4;
            AngularVelocityTimer.Interval = HIDrate * 4;
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
            stopwatch.Start();

            UpdateTimer.Enabled = true;
            UpdateTimer.Start();

            Connected?.Invoke(this);
            logger.LogInformation("Virtual {0} connected", ToString());
        }

        public virtual void Disconnect()
        {
            stopwatch.Stop();

            UpdateTimer.Enabled = false;
            UpdateTimer.Stop();

            Disconnected?.Invoke(this);
            logger.LogInformation("Virtual {0} disconnected", ToString());
        }

        public virtual unsafe void UpdateReport(object sender, ElapsedEventArgs e)
        {
            Updated?.Invoke(this);

            lock (updateLock)
            {
                // update timestamp
                microseconds = (long)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)));

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
                        default:
                        case InputStyle.RightStick:
                            RightThumbX = Utils.ComputeInput(RightThumbX, -AngularVelocity.Z * 1.25f, sensivity, intensity);
                            RightThumbY = Utils.ComputeInput(RightThumbY, AngularVelocity.X, sensivity, intensity);
                            break;
                        case InputStyle.LeftStick:
                            LeftThumbX = Utils.ComputeInput(LeftThumbX, -AngularVelocity.Z * 1.25f, sensivity, intensity);
                            LeftThumbY = Utils.ComputeInput(LeftThumbY, AngularVelocity.X, sensivity, intensity);
                            break;
                    }
                }
            }
        }

        internal void SubmitReport()
        {
            // do something
        }

        private void AccelerationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Acceleration = new();
        }

        private void AngularVelocityTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            AngularVelocity = new();
        }
    }
}
