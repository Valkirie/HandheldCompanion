using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    public class XInputController
    {
        public Controller controller;
        public Gamepad gamepad;
        public DeviceInstance instance;

        private DSUServer server;
        private IVirtualGamepad vcontroller;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;
        public DS4Touch touch;

        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        private Timer UpdateTimer;

        public UserIndex index;
        public bool muted;

        public long microseconds;
        private Stopwatch stopwatch;

        private byte FrameCounter = 0; // always 0 on USB

        private const int ACC_RES_PER_G = 8192;
        private const float F_ACC_RES_PER_G = ACC_RES_PER_G;
        private const int GYRO_RES_IN_DEG_SEC = 16;
        private const float F_GYRO_RES_IN_DEG_SEC = GYRO_RES_IN_DEG_SEC;

        private object updateLock = new();

        private DS4_REPORT_EX outDS4Report;

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

            // initialize timers
            UpdateTimer = new Timer(10) { Enabled = false, AutoReset = true };

            // initialize touch
            touch = new DS4Touch();
        }

        public Dictionary<string, string> ToArgs()
        {
            return new Dictionary<string, string>() {
                { "name", instance.ProductName },
                { "guid", $"{instance.InstanceGuid}" },
                { "index", $"{(int)index}" },
                { "gyrometer", $"{gyrometer != null}" },
                { "accelerometer", $"{accelerometer != null}" }
            };
        }

        public void SetVirtualController(IVirtualGamepad _controller)
        {
            vcontroller = _controller;
            vcontroller.AutoSubmitReport = false;

            switch(vcontroller.GetType().FullName)
            {
                case "Nefarius.ViGEm.Client.Targets.DualShock4Controller":
                    UpdateTimer.Elapsed += DS4_UpdateReport;
                    ((IDualShock4Controller)vcontroller).FeedbackReceived += DS4_FeedbackReceived;
                    break;
                case "Nefarius.ViGEm.Client.Targets.Xbox360Controller":
                    throw new NotImplementedException();
                    break;
            }

            UpdateTimer.Enabled = true;
            UpdateTimer.Start();
        }

        private void DS4_FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
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
            server = _server;
        }

        private void Accelerometer_ReadingChanged(object sender, XInputAccelerometerReadingChangedEventArgs e)
        {
            Acceleration.X = e.AccelerationY;
            Acceleration.Y = e.AccelerationZ;
            Acceleration.Z = e.AccelerationX;
        }

        private void Girometer_ReadingChanged(object sender, XInputGirometerReadingChangedEventArgs e)
        {
            AngularVelocity.X = e.AngularVelocityY;
            AngularVelocity.Y = e.AngularVelocityZ;
            AngularVelocity.Z = e.AngularVelocityX;
        }

        private unsafe void DS4_UpdateReport(object sender, ElapsedEventArgs e)
        {
            if (!controller.IsConnected)
                return;

            lock (updateLock)
            {
                // update timestamp
                microseconds = (long)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)) /* / 5.33f */);

                // get current gamepad state
                State state = controller.GetState();
                gamepad = state.Gamepad;

                // send report to server
                server?.NewReportIncoming(this, microseconds);

                // reset vars
                byte[] rawOutReportEx = new byte[63];
                ushort tempButtons = 0;
                ushort tempSpecial = 0;
                DualShock4DPadDirection tempDPad = DualShock4DPadDirection.None;

                outDS4Report.bThumbLX = 128;
                outDS4Report.bThumbLY = 128;
                outDS4Report.bThumbRX = 128;
                outDS4Report.bThumbRY = 128;

                unchecked
                {
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.A)) tempButtons |= DualShock4Button.Cross.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.B)) tempButtons |= DualShock4Button.Circle.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.X)) tempButtons |= DualShock4Button.Square.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Y)) tempButtons |= DualShock4Button.Triangle.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
                        tempButtons |= DualShock4Button.Options.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Back)) tempButtons |= DualShock4Button.Share.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb))
                        tempButtons |= DualShock4Button.ThumbRight.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb))
                        tempButtons |= DualShock4Button.ThumbLeft.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                        tempButtons |= DualShock4Button.ShoulderRight.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
                        tempButtons |= DualShock4Button.ShoulderLeft.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) &&
                        gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                        tempDPad = DualShock4DPadDirection.Northeast;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) &&
                             gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                        tempDPad = DualShock4DPadDirection.Northwest;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp))
                        tempDPad = DualShock4DPadDirection.North;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) &&
                             gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                        tempDPad = DualShock4DPadDirection.Southeast;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                        tempDPad = DualShock4DPadDirection.East;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown) &&
                             gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                        tempDPad = DualShock4DPadDirection.Southwest;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                        tempDPad = DualShock4DPadDirection.South;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                        tempDPad = DualShock4DPadDirection.West;

                    // if (state.PS) tempSpecial |= DualShock4SpecialButton.Ps.Value;
                    // if (state.OutputTouchButton) tempSpecial |= DualShock4SpecialButton.Touchpad.Value;
                    // outDS4Report.bSpecial = (byte)tempSpecial;
                    outDS4Report.bSpecial = (byte)(tempSpecial | (FrameCounter << 2));
                }

                if (!muted)
                {
                    outDS4Report.wButtons = tempButtons;
                    outDS4Report.wButtons |= tempDPad.Value;

                    outDS4Report.bTriggerL = gamepad.LeftTrigger;
                    outDS4Report.bTriggerR = gamepad.RightTrigger;

                    outDS4Report.bThumbLX = Utils.NormalizeInput(gamepad.LeftThumbX);
                    outDS4Report.bThumbLY = (byte)(byte.MaxValue - Utils.NormalizeInput(gamepad.LeftThumbY));
                    outDS4Report.bThumbRX = Utils.NormalizeInput(gamepad.RightThumbX);
                    outDS4Report.bThumbRY = (byte)(byte.MaxValue - Utils.NormalizeInput(gamepad.RightThumbY));
                }

                unchecked
                {
                    outDS4Report.bTouchPacketsN = 0x01;
                    outDS4Report.sCurrentTouch.bPacketCounter = touch.TouchPacketCounter;
                    outDS4Report.sCurrentTouch.bIsUpTrackingNum1 = touch.TrackPadTouch0.RawTrackingNum;
                    outDS4Report.sCurrentTouch.bTouchData1[0] = (byte)(touch.TrackPadTouch0.X & 0xFF);
                    outDS4Report.sCurrentTouch.bTouchData1[1] =
                        (byte)(((touch.TrackPadTouch0.X >> 8) & 0x0F) | ((touch.TrackPadTouch0.Y << 4) & 0xF0));
                    outDS4Report.sCurrentTouch.bTouchData1[2] = (byte)(touch.TrackPadTouch0.Y >> 4);

                    outDS4Report.sCurrentTouch.bIsUpTrackingNum2 = touch.TrackPadTouch1.RawTrackingNum;
                    outDS4Report.sCurrentTouch.bTouchData2[0] = (byte)(touch.TrackPadTouch1.X & 0xFF);
                    outDS4Report.sCurrentTouch.bTouchData2[1] =
                        (byte)(((touch.TrackPadTouch1.X >> 8) & 0x0F) | ((touch.TrackPadTouch1.Y << 4) & 0xF0));
                    outDS4Report.sCurrentTouch.bTouchData2[2] = (byte)(touch.TrackPadTouch1.Y >> 4);
                }

                outDS4Report.wGyroX = (short)(-AngularVelocity.X * F_GYRO_RES_IN_DEG_SEC); // gyroPitchFull
                outDS4Report.wGyroY = (short)(-AngularVelocity.Y * F_GYRO_RES_IN_DEG_SEC); // gyroYawFull
                outDS4Report.wGyroZ = (short)(AngularVelocity.Z * F_GYRO_RES_IN_DEG_SEC); // gyroRollFull
                outDS4Report.wAccelX = (short)(Acceleration.X * F_ACC_RES_PER_G); // accelXFull
                outDS4Report.wAccelY = (short)(-Acceleration.Y * F_ACC_RES_PER_G); // accelYFull
                outDS4Report.wAccelZ = (short)(Acceleration.Z * F_ACC_RES_PER_G); // accelZFull

                outDS4Report.bBatteryLvlSpecial = 11;

                outDS4Report.wTimestamp = (ushort)(microseconds / 5.33f);

                DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);

                // send report to controller
                ((IDualShock4Controller)vcontroller).SubmitRawReport(rawOutReportEx);
            }
        }
    }
}
