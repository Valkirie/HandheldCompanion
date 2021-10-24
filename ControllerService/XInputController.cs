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
using System.Windows.Forms;

namespace ControllerService
{
    public class XInputController
    {
        public Controller controller;
        public Gamepad gamepad;

        private DSUServer dsu;
        private IDualShock4Controller vcontroller;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;

        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        public UserIndex index;
        public bool muted;

        public long microseconds;
        private Stopwatch stopwatch;

        public struct TrackPadTouch
        {
            public bool IsActive;
            public byte Id;
            public ushort X;
            public ushort Y;
        }

        public TrackPadTouch TrackPadTouch0;
        public TrackPadTouch TrackPadTouch1;

        public DsBattery BatteryStatus;

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
            vcontroller.FeedbackReceived += Vcontroller_FeedbackReceived;

            Thread UpdateThread = new Thread(UpdateDS4);
            UpdateThread.Start();

            Thread MonitorBattery = new Thread(MonitorBatteryLife);
            MonitorBattery.Start();
        }

        private void Vcontroller_FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
        {
            if (controller.IsConnected)
            {
                Vibration inputMotor = new Vibration()
                {
                    LeftMotorSpeed = (ushort)(e.LargeMotor * ushort.MaxValue / byte.MaxValue),
                    RightMotorSpeed = (ushort)(e.SmallMotor * ushort.MaxValue / byte.MaxValue),
                };
                controller.SetVibration(inputMotor);
            }
        }

        private void MonitorBatteryLife()
        {
            while (true)
            {
                if (controller.IsConnected)
                {
                    BatteryChargeStatus ChargeStatus = SystemInformation.PowerStatus.BatteryChargeStatus;

                    if (ChargeStatus.HasFlag(BatteryChargeStatus.Charging))
                        BatteryStatus = DsBattery.Charging;
                    else if (ChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery))
                        BatteryStatus = DsBattery.None;
                    else if (ChargeStatus.HasFlag(BatteryChargeStatus.High))
                        BatteryStatus = DsBattery.High;
                    else if (ChargeStatus.HasFlag(BatteryChargeStatus.Low))
                        BatteryStatus = DsBattery.Low;
                    else if (ChargeStatus.HasFlag(BatteryChargeStatus.Critical))
                        BatteryStatus = DsBattery.Dying;
                    else
                        BatteryStatus = DsBattery.Medium;
                }

                Thread.Sleep(1000);
            }
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

        public void SetDSUServer(DSUServer _server)
        {
            dsu = _server;
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

        private State previousState;
        private ushort tempButtons;
        private DualShock4DPadDirection tempDPad;
        private byte[] buffer;

        private void UpdateDS4()
        {
            while (true)
            {
                // update timestamp
                microseconds = stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));

                if (controller.IsConnected)
                {
                    // reset vars
                    buffer = new byte[63];
                    tempButtons = 0;
                    tempDPad = DualShock4DPadDirection.None;

                    State state = controller.GetState();
                    if (previousState.PacketNumber != state.PacketNumber)
                    {
                        gamepad = controller.GetState().Gamepad;

                        buffer[0] = Utils.NormalizeInput(gamepad.LeftThumbX); // Left Stick X
                        buffer[1] = (byte)(byte.MaxValue - Utils.NormalizeInput(gamepad.LeftThumbY)); // Left Stick Y

                        buffer[2] = Utils.NormalizeInput(gamepad.RightThumbX); ; // Right Stick X
                        buffer[3] = (byte)(byte.MaxValue - Utils.NormalizeInput(gamepad.RightThumbY)); // Right Stick Y

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

                        buffer[7] = gamepad.LeftTrigger; // Left Trigger
                        buffer[8] = gamepad.RightTrigger; // Right Trigger
                    }

                    // update state
                    previousState = state;

                    // buttons and dpad
                    tempButtons |= tempDPad.Value;
                    buffer[4] = (byte)tempButtons; // dpad
                    buffer[5] = (byte)((short)tempButtons >> 8); // dpad

                    // timestamp
                    buffer[9] = (byte)microseconds;
                    buffer[10] = (byte)((ushort)microseconds >> 8);

                    // battery
                    buffer[11] = (byte)BatteryStatus; // bBatteryLvl
                    buffer[29] = (byte)BatteryStatus; // bBatteryLvlSpecial

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

                    // send report to server
                    dsu.NewReportIncoming(this, microseconds);

                    // send report to controller
                    if (!muted)
                        vcontroller.SubmitRawReport(buffer);
                }

                Thread.Sleep(10);
            }
        }
    }
}
