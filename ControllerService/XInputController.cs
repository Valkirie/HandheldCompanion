using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Numerics;
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

        private DS4_REPORT_EX outDS4Report;

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

        private void UpdateDS4()
        {
            while (true)
            {
                if (controller.IsConnected)
                {
                    // update timestamp
                    microseconds = stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));

                    // get current gamepad state
                    State state = controller.GetState();
                    gamepad = state.Gamepad;

                    // send report to server
                    dsu.NewReportIncoming(this, microseconds);

                    if (muted)
                    {
                        vcontroller.ResetReport();
                        vcontroller.SubmitReport();
                        continue;
                    }

                    // reset vars
                    byte[] rawOutReportEx = new byte[63];
                    ushort tempButtons = 0;
                    ushort tempSpecial = 0;
                    DualShock4DPadDirection tempDPad = DualShock4DPadDirection.None;

                    unchecked
                    {
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.A)) tempButtons |= DualShock4Button.Cross.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.B)) tempButtons |= DualShock4Button.Circle.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.X)) tempButtons |= DualShock4Button.Square.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Y)) tempButtons |= DualShock4Button.Triangle.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Start)) tempButtons |= DualShock4Button.Options.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Back)) tempButtons |= DualShock4Button.Share.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb)) tempButtons |= DualShock4Button.ThumbRight.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb)) tempButtons |= DualShock4Button.ThumbLeft.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder)) tempButtons |= DualShock4Button.ShoulderRight.Value;
                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder)) tempButtons |= DualShock4Button.ShoulderLeft.Value;

                        if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight)) tempDPad = DualShock4DPadDirection.Northeast;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.Northwest;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)) tempDPad = DualShock4DPadDirection.North;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)) tempDPad = DualShock4DPadDirection.Southeast;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight)) tempDPad = DualShock4DPadDirection.East;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.Southwest;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)) tempDPad = DualShock4DPadDirection.South;
                        else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.West;
                        
                        // if (state.PS) tempSpecial |= DualShock4SpecialButton.Ps.Value;
                        // if (state.OutputTouchButton) tempSpecial |= DualShock4SpecialButton.Touchpad.Value;

                        outDS4Report.wButtons = tempButtons;
                        outDS4Report.bSpecial = (byte)tempSpecial;
                        outDS4Report.wButtons |= tempDPad.Value;
                    }

                    outDS4Report.bTriggerL = gamepad.LeftTrigger;
                    outDS4Report.bTriggerR = gamepad.RightTrigger;

                    outDS4Report.bThumbLX = Utils.NormalizeInput(gamepad.LeftThumbX);
                    outDS4Report.bThumbLY = (byte)(byte.MaxValue - Utils.NormalizeInput(gamepad.LeftThumbY));

                    outDS4Report.bThumbRX = Utils.NormalizeInput(gamepad.RightThumbX);
                    outDS4Report.bThumbRY = (byte)(byte.MaxValue - Utils.NormalizeInput(gamepad.RightThumbY));

                    outDS4Report.bTouchPacketsN = 0; // todo

                    outDS4Report.wGyroX = (short)AngularVelocity.X;
                    outDS4Report.wGyroY = (short)AngularVelocity.Y;
                    outDS4Report.wGyroZ = (short)AngularVelocity.Z;

                    outDS4Report.wAccelX = (short)Acceleration.X;
                    outDS4Report.wAccelY = (short)Acceleration.Y;
                    outDS4Report.wAccelZ = (short)Acceleration.Z;

                    outDS4Report.bBatteryLvl = (byte)BatteryStatus;
                    
                    // USB DS4 v.1 battery level range is [0-11]
                    outDS4Report.bBatteryLvlSpecial = (byte)((byte)BatteryStatus / 11);
                    outDS4Report.wTimestamp = (ushort)microseconds;

                    DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);

                    // send report to controller
                    vcontroller.SubmitRawReport(rawOutReportEx);
                }

                Thread.Sleep(10);
            }
        }
    }
}
