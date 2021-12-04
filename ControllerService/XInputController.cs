using ControllerCommon;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Timers;
using static ControllerCommon.Utils;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    public class XInputController
    {
        #region imports
        [StructLayout(LayoutKind.Sequential)]
        public struct XInputStateSecret
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
        private static extern int XInputGetStateSecret13(int playerIndex, out XInputStateSecret struc);
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern int XInputGetStateSecret14(int playerIndex, out XInputStateSecret struc);
        #endregion

        private const float F_ACC_RES_PER_G = 8192.0f;
        private const float F_GYRO_RES_IN_DEG_SEC = 16.0f;

        private OneEuroFilter3D accelFilter = new OneEuroFilter3D();
        private OneEuroFilter3D gyroFilter = new OneEuroFilter3D();

        public Controller controller;
        public Gamepad gamepad;
        public XInputStateSecret state_s;
        public DeviceInstance instance;

        private DSUServer server;
        private IVirtualGamepad vcontroller;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;
        public DS4Touch touch;

        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        private Timer UpdateTimer;
        private float strength;

        public UserIndex index;
        public Profile profile;

        public long microseconds;
        private Stopwatch stopwatch;

        private object updateLock = new();

        private DS4_REPORT_EX outDS4Report;

        private readonly ILogger logger;

        public XInputController(UserIndex _idx, int HIDrate, ILogger logger)
        {
            this.logger = logger;

            // initilize controller
            controller = new Controller(_idx);
            index = _idx;

            if (!controller.IsConnected)
                return;

            // initialize profile
            profile = new Profile();

            // initialize vectors
            AngularVelocity = new Vector3();
            Acceleration = new Vector3();

            // initialize secret state
            state_s = new XInputStateSecret();

            // initialize stopwatch
            stopwatch = new Stopwatch();
            stopwatch.Start();

            // initialize timers
            UpdateTimer = new Timer(HIDrate) { Enabled = false, AutoReset = true };

            // initialize touch
            touch = new DS4Touch();
        }

        public void SetPollRate(int HIDrate)
        {
            UpdateTimer.Interval = HIDrate;
            logger.LogInformation("Virtual {0} report interval set to {1}ms", vcontroller.GetType().Name, UpdateTimer.Interval);
        }

        public void SetVibrationStrength(float strength)
        {
            this.strength = strength / 100.0f;
            logger.LogInformation("Virtual {0} vibration strength set to {1}%", vcontroller.GetType().Name, strength);
        }

        public Dictionary<string, string> ToArgs()
        {
            return new Dictionary<string, string>() {
                { "ProductName", instance.ProductName },
                { "InstanceGuid", $"{instance.InstanceGuid}" },
                { "ProductGuid", $"{instance.ProductGuid}" },
                { "ProductIndex", $"{(int)index}" }
            };
        }

        public void SetVirtualController(IVirtualGamepad _controller)
        {
            vcontroller = _controller;
            vcontroller.AutoSubmitReport = false;

            switch (vcontroller.GetType().FullName)
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

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", vcontroller.GetType().Name, instance.InstanceName, index);
            logger.LogInformation("Virtual {0} report interval set to {1}ms", vcontroller.GetType().Name, UpdateTimer.Interval);
        }

        private void DS4_FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
        {
            if (controller.IsConnected)
            {
                Vibration inputMotor = new Vibration()
                {
                    LeftMotorSpeed = (ushort)((e.LargeMotor * ushort.MaxValue / byte.MaxValue) * strength),
                    RightMotorSpeed = (ushort)((e.SmallMotor * ushort.MaxValue / byte.MaxValue) * strength),
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
                microseconds = (long)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)));

                // get current gamepad state
                State state = controller.GetState();
                gamepad = state.Gamepad;

                XInputGetStateSecret13((int)index, out state_s);

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

                    if (gamepad.LeftTrigger > 0)
                        tempButtons |= DualShock4Button.TriggerLeft.Value;
                    if (gamepad.RightTrigger > 0)
                        tempButtons |= DualShock4Button.TriggerRight.Value;

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

                    if ((state_s.wButtons & 0x0400) == 0x0400)
                        tempSpecial |= DualShock4SpecialButton.Ps.Value;
                    if (touch.OutputClickButton)
                        tempSpecial |= DualShock4SpecialButton.Touchpad.Value;

                    outDS4Report.bSpecial = (byte)(tempSpecial | (0 << 2));
                }

                if (!profile.whitelisted)
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

                var rate = 1.0 / stopwatch.ElapsedMilliseconds;
                outDS4Report.wGyroX = (short)gyroFilter.axis1Filter.Filter(-AngularVelocity.X * F_GYRO_RES_IN_DEG_SEC, rate); // gyroPitchFull
                outDS4Report.wGyroY = (short)gyroFilter.axis1Filter.Filter(-AngularVelocity.Y * F_GYRO_RES_IN_DEG_SEC, rate); // gyroYawFull
                outDS4Report.wGyroZ = (short)gyroFilter.axis1Filter.Filter(AngularVelocity.Z * F_GYRO_RES_IN_DEG_SEC, rate); // gyroRollFull
                
                outDS4Report.wAccelX = (short)accelFilter.axis1Filter.Filter(Acceleration.X * F_ACC_RES_PER_G, rate); // accelXFull
                outDS4Report.wAccelY = (short)accelFilter.axis1Filter.Filter(-Acceleration.Y * F_ACC_RES_PER_G, rate); // accelYFull
                outDS4Report.wAccelZ = (short)accelFilter.axis1Filter.Filter(Acceleration.Z * F_ACC_RES_PER_G, rate); // accelZFull

                outDS4Report.bBatteryLvlSpecial = 11;

                outDS4Report.wTimestamp = (ushort)(microseconds / 5.33f);

                DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);
                ((IDualShock4Controller)vcontroller).SubmitRawReport(rawOutReportEx);
            }
        }
    }
}
