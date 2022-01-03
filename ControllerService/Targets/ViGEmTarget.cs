using ControllerCommon;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Timers;

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

        public Profile Profile;
        public Profile DefaultProfile;

        public Controller Controller;
        public Gamepad Gamepad;
        public DS4Touch Touch;
        public HIDmode HID = HIDmode.None;
        protected readonly ILogger logger;

        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        protected ViGEmClient Client { get; }
        protected IVirtualGamepad vcontroller;

        protected XInputStateSecret state_s;

        public long microseconds;
        public float strength; // rename me

        protected readonly Stopwatch stopwatch;
        protected int UserIndex;

        protected short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;
        public Timer UpdateTimer;

        public event SubmitedEventHandler Submited;
        public delegate void SubmitedEventHandler(Object sender);
        protected object updateLock = new();

        protected ViGEmTarget(ViGEmClient client, Controller controller, int index, int HIDrate, ILogger logger)
        {
            this.logger = logger;

            // initialize vectors
            AngularVelocity = new();
            Acceleration = new();

            // initialize profile
            Profile = new();
            DefaultProfile = new();
            Touch = new();

            // initialize secret state
            state_s = new();

            // initialize controller
            Client = client;
            Controller = controller;

            // initialize stopwatch
            stopwatch = new Stopwatch();

            // initialize timers
            UpdateTimer = new Timer(HIDrate)
            {
                Enabled = false,
                AutoReset = true
            };
        }

        protected void FeedbackReceived(object sender, EventArgs e)
        {
        }

        public override string ToString()
        {
            return Utils.GetDescription(this.HID);
        }

        public void Connect()
        {
            stopwatch.Start();

            UpdateTimer.Enabled = true;
            UpdateTimer.Start();

            logger.LogInformation("Virtual {0} connected", ToString());
        }

        public void Disconnect()
        {
            stopwatch.Stop();

            UpdateTimer.Enabled = false;
            UpdateTimer.Stop();

            logger.LogInformation("Virtual {0} disconnected", ToString());
        }

        public unsafe void UpdateReport(object sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                // update timestamp
                microseconds = (long)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)));

                // get current gamepad state
                XInputGetStateSecret13(UserIndex, out state_s);
                State state = Controller.GetState();
                Gamepad = state.Gamepad;

                // get buttons values
                uint buttons = (ushort)Gamepad.Buttons;
                buttons |= (Gamepad.LeftTrigger > 0 ? (uint)Utils.GamepadButtonFlags.LeftTrigger : 0);
                buttons |= (Gamepad.RightTrigger > 0 ? (uint)Utils.GamepadButtonFlags.RightTrigger : 0);

                // get sticks values
                LeftThumbX = Gamepad.LeftThumbX;
                LeftThumbY = Gamepad.LeftThumbY;
                RightThumbX = Gamepad.RightThumbX;
                RightThumbY = Gamepad.RightThumbY;

                if (Profile.umc_enabled && ((Profile.umc_trigger & buttons) != 0 || (Profile.umc_trigger & (uint)Utils.GamepadButtonFlags.AlwaysOn) != 0))
                {
                    float intensity = Profile.GetIntensity();
                    float sensivity = Profile.GetSensiviy();

                    switch (Profile.umc_input)
                    {
                        default:
                        case InputStyle.RightStick:
                            RightThumbX = Utils.ComputeInput(RightThumbX, -AngularVelocity.Z * 1.5f, sensivity, intensity);
                            RightThumbY = Utils.ComputeInput(RightThumbY, AngularVelocity.X, sensivity, intensity);
                            break;
                        case InputStyle.LeftStick:
                            LeftThumbX = Utils.ComputeInput(LeftThumbX, -AngularVelocity.Z * 1.5f, sensivity, intensity);
                            LeftThumbY = Utils.ComputeInput(LeftThumbY, AngularVelocity.X, sensivity, intensity);
                            break;
                    }
                }
            }
        }

        internal void SubmitReport()
        {
            Submited?.Invoke(this);
        }
    }
}
