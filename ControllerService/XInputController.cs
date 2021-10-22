using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ControllerService
{
    public class XInputController
    {
        public Controller controller;
        private Gamepad gamepad;

        private UdpServer server;
        private IDualShock4Controller vcontroller;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;

        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        public UserIndex index;
        public bool muted;

        private long microseconds;
        private Stopwatch stopwatch;
        
        struct _DS4_TOUCH
        {
            public byte bPacketCounter;         // timestamp / packet counter associated with touch event
            public byte bIsUpTrackingNum1;      // 0 means down; active low
                                                // unique to each finger down, so for a lift and repress the value is incremented
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] bTouchData1;          // Two 12 bits values (for X and Y) 
                                                // middle byte holds last 4 bits of X and the starting 4 bits of Y
            public byte bIsUpTrackingNum2;      // second touch data immediately follows data of first touch
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] bTouchData2;          // resolution is 1920x942
        }

        public XInputController(UserIndex _idx)
        {
            // initilize controller
            controller = new Controller(_idx);
            index = _idx;

            if (!controller.IsConnected)
                return;

            // initialize vectors
            AngularVelocity = new Vector3();
            Acceleration = new Vector3();

            // initialize stopwatch
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        public void SetVirtualController(IDualShock4Controller _controller)
        {
            vcontroller = _controller;
            vcontroller.AutoSubmitReport = false;

            Thread UpdateThread = new Thread(UpdateDS4);
            UpdateThread.Start();
        }

        public void SetGyroscope(XInputGirometer _gyrometer)
        {
            gyrometer = _gyrometer;
            gyrometer.ReadingChanged += Girometer_ReadingChanged;
        }

        public void SetAccelerometer(XInputAccelerometer _accelerometer)
        {
            accelerometer = _accelerometer;
            accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
        }

        public void SetUdpServer(UdpServer _server)
        {
            server = _server;

            Thread ThreadServer = new Thread(UpdateUDP);
            ThreadServer.Start();
        }

        private void UpdateUDP()
        {
            while(true)
            {
                if (controller.IsConnected)
                {
                    microseconds = stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
                    server.NewReportIncoming(this, microseconds);
                }
                Thread.Sleep(10);
            }
        }

        private void Accelerometer_ReadingChanged(object sender, XInputAccelerometerReadingChangedEventArgs e)
        {
            Acceleration.X = e.AccelerationX;
            Acceleration.Y = e.AccelerationY;
            Acceleration.Z = e.AccelerationZ;
        }

        private void Girometer_ReadingChanged(object sender, XInputGirometerReadingChangedEventArgs e)
        {
            AngularVelocity.X = e.AngularVelocityX;
            AngularVelocity.Y = e.AngularVelocityY;
            AngularVelocity.Z = e.AngularVelocityZ;
        }

        private byte NormalizeInput(short input)
        {
            input = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, input));
            float output = (float)input / (float)ushort.MaxValue * (float)byte.MaxValue + (float)(byte.MaxValue / 2.0f);
            return (byte)output;
        }

        private State previousState;
        private ushort tempButtons;
        private DualShock4DPadDirection tempDPad;
        private byte[] buffer;

        private void UpdateDS4()
        {
            _DS4_TOUCH touch1 = new _DS4_TOUCH()
            {
                bPacketCounter = 1,
                bIsUpTrackingNum1 = (0 << 7) + 1
            };

            while (true)
            {
                if (controller.IsConnected && !muted)
                {
                    State state = controller.GetState();
                    if (previousState.PacketNumber != state.PacketNumber)
                    {
                        gamepad = controller.GetState().Gamepad;
                        buffer = new byte[63];

                        tempButtons = 0;
                        tempDPad = DualShock4DPadDirection.None;

                        buffer[0] = NormalizeInput(gamepad.LeftThumbX); // Left Stick X
                        buffer[1] = (byte)(byte.MaxValue - NormalizeInput(gamepad.LeftThumbY)); // Left Stick Y

                        buffer[2] = NormalizeInput(gamepad.RightThumbX); ; // Right Stick X
                        buffer[3] = (byte)(byte.MaxValue - NormalizeInput(gamepad.RightThumbY)); // Right Stick Y

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
                            tempButtons |= DualShock4Button.Cross.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.B))
                            tempButtons |= DualShock4Button.Circle.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.X))
                            tempButtons |= DualShock4Button.Square.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
                            tempButtons |= DualShock4Button.Triangle.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
                            tempButtons |= DualShock4Button.Options.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
                            tempButtons |= DualShock4Button.Share.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb))
                            tempButtons |= DualShock4Button.ThumbRight.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb))
                            tempButtons |= DualShock4Button.ThumbLeft.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                            tempButtons |= DualShock4Button.ShoulderRight.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
                            tempButtons |= DualShock4Button.ShoulderLeft.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight)) tempDPad = DualShock4DPadDirection.Northeast;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.Northwest;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)) tempDPad = DualShock4DPadDirection.North;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)) tempDPad = DualShock4DPadDirection.Southeast;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight)) tempDPad = DualShock4DPadDirection.East;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.Southwest;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)) tempDPad = DualShock4DPadDirection.South;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.West;

                        tempButtons |= tempDPad.Value;
                        buffer[4] = (byte)tempButtons; // dpad
                        buffer[5] = (byte)((short)tempButtons >> 8); // dpad

                        buffer[7] = gamepad.LeftTrigger; // Left Trigger
                        buffer[8] = gamepad.RightTrigger; // Right Trigger

                        buffer[9] = (byte)microseconds;                    // timestamp
                        buffer[10] = (byte)((ushort)microseconds >> 8);    // timestamp

                        buffer[11] = (byte)0xff; // battery

                        // wGyro
                        buffer[12] = (byte)AngularVelocity.X;
                        buffer[13] = (byte)((short)AngularVelocity.X >> 8);
                        buffer[14] = (byte)AngularVelocity.Y;
                        buffer[15] = (byte)((short)AngularVelocity.Y >> 8);
                        buffer[16] = (byte)AngularVelocity.Z;
                        buffer[17] = (byte)((short)AngularVelocity.Z >> 8);

                        // wAccel
                        buffer[18] = (byte)Acceleration.X;
                        buffer[19] = (byte)((short)Acceleration.X >> 8);
                        buffer[20] = (byte)Acceleration.Y;
                        buffer[21] = (byte)((short)Acceleration.Y >> 8);
                        buffer[22] = (byte)Acceleration.Z;
                        buffer[23] = (byte)((short)Acceleration.Z >> 8);

                        buffer[29] = (byte)0xff; // battery

                        // buffer[33] = (byte)touch1.bPacketCounter;

                        vcontroller.SubmitRawReport(buffer);
                    }
                    previousState = state;
                }

                Thread.Sleep(10);
            }
        }
    }
}
