using ControllerCommon;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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

        public Profile profile;
        public Controller Controller;
        public Gamepad Gamepad;
        public DS4Touch touch;

        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        protected ViGEmClient Client { get; }
        protected IVirtualGamepad vcontroller;

        protected XInputStateSecret state_s;

        public long microseconds;
        protected readonly Stopwatch stopwatch;
        protected int index;

        protected ViGEmTarget(ViGEmClient client, Controller controller, int index)
        {
            // initialize vectors
            AngularVelocity = new();
            Acceleration = new();

            // initialize profile
            profile = new();
            touch = new();

            // initialize secret state
            state_s = new();

            Client = client;
            Controller = controller;

            stopwatch = new Stopwatch();
        }

        protected short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;

        public void UpdateReport()
        {
            // update timestamp
            microseconds = (long)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)));

            // get current gamepad state
            XInputGetStateSecret13(index, out state_s);
            State state = Controller.GetState();
            Gamepad = state.Gamepad;

            // get buttons values
            ushort buttons = (ushort)Gamepad.Buttons;
            buttons |= (Gamepad.LeftTrigger > 0 ? (ushort)1024 : (ushort)0);
            buttons |= (Gamepad.RightTrigger > 0 ? (ushort)2048 : (ushort)0);

            // get sticks values
            LeftThumbX = Gamepad.LeftThumbX;
            LeftThumbY = Gamepad.LeftThumbY;
            RightThumbX = Gamepad.RightThumbX;
            RightThumbY = Gamepad.RightThumbY;

            if (profile.umc_enabled && ((buttons + ProfileButton.AlwaysOn.Value) & profile.umc_trigger) != 0)
            {
                float intensity = profile.GetIntensity();
                float sensivity = profile.GetSensiviy();

                switch (profile.umc_input)
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

        internal void Disconnect()
        {
            throw new NotImplementedException();
        }
    }
}
