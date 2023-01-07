using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace ControllerService.Targets
{
    internal class DualShock4Target : ViGEmTarget
    {
        // DS4 Accelerometer g-force measurement range G SI unit to short
        // Various sources state either +/- 2 or 4 ranges are in use 
        private static readonly SensorSpec DS4AccelerometerSensorSpec = new SensorSpec()
        {
            minIn = -4.0f,
            maxIn = 4.0f,
            minOut = short.MinValue,
            maxOut = short.MaxValue,
        };

        // DS4 Gyroscope angular rate measurement range deg/sec SI unit to short
        // Note, at +/- 2000 the value is still off by a factor 5
        private static readonly SensorSpec DS4GyroscopeSensorSpec = new SensorSpec()
        {
            minIn = -10000.0f,
            maxIn = 10000.0f,
            minOut = short.MinValue,
            maxOut = short.MaxValue,
        };

        private DS4_REPORT_EX outDS4Report;

        private new IDualShock4Controller virtualController;

        public DualShock4Target() : base()
        {
            // initialize controller
            HID = HIDmode.DualShock4Controller;

            virtualController = ControllerService.vClient.CreateDualShock4Controller();
            virtualController.AutoSubmitReport = false;
            virtualController.FeedbackReceived += FeedbackReceived;

            UpdateTimer.Tick += (sender, e) => UpdateReport();

            LogManager.LogInformation("{0} initialized, {1}", ToString(), virtualController);
        }

        public override void Connect()
        {
            if (IsConnected)
                return;

            try
            {
                virtualController.Connect();
                UpdateTimer.Start();

                base.Connect();
            }
            catch { }
        }

        public override void Disconnect()
        {
            if (!IsConnected)
                return;

            try
            {
                virtualController.Disconnect();
                UpdateTimer.Stop();

                base.Disconnect();
            }
            catch { }
        }

        public void FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
        {
            // pass raw vibration to client
            PipeServer.SendMessage(new PipeClientVibration() { LargeMotor = e.LargeMotor, SmallMotor = e.SmallMotor });
        }

        public override unsafe void UpdateReport()
        {
            if (!IsConnected)
                return;

            base.UpdateReport();

            // reset vars
            byte[] rawOutReportEx = new byte[63];
            ushort tempButtons = 0;
            ushort tempSpecial = 0;
            DualShock4DPadDirection tempDPad = DualShock4DPadDirection.None;

            outDS4Report.bThumbLX = 127;
            outDS4Report.bThumbLY = 127;
            outDS4Report.bThumbRX = 127;
            outDS4Report.bThumbRY = 127;

            unchecked
            {
                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.B1))
                    tempButtons |= DualShock4Button.Cross.Value;
                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.B2))
                    tempButtons |= DualShock4Button.Circle.Value;
                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.B3))
                    tempButtons |= DualShock4Button.Square.Value;
                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.B4))
                    tempButtons |= DualShock4Button.Triangle.Value;

                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.Start))
                    tempButtons |= DualShock4Button.Options.Value;
                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.Back))
                    tempButtons |= DualShock4Button.Share.Value;

                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.RightThumb))
                    tempButtons |= DualShock4Button.ThumbRight.Value;
                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.LeftThumb))
                    tempButtons |= DualShock4Button.ThumbLeft.Value;

                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.RightShoulder))
                    tempButtons |= DualShock4Button.ShoulderRight.Value;
                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.LeftShoulder))
                    tempButtons |= DualShock4Button.ShoulderLeft.Value;

                if (Inputs.LeftTrigger > 0)
                    tempButtons |= DualShock4Button.TriggerLeft.Value;
                if (Inputs.RightTrigger > 0)
                    tempButtons |= DualShock4Button.TriggerRight.Value;

                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadUp) &&
                    Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadRight))
                    tempDPad = DualShock4DPadDirection.Northeast;
                else if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadUp) &&
                         Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadLeft))
                    tempDPad = DualShock4DPadDirection.Northwest;
                else if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadUp))
                    tempDPad = DualShock4DPadDirection.North;
                else if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadRight) &&
                         Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadDown))
                    tempDPad = DualShock4DPadDirection.Southeast;
                else if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadRight))
                    tempDPad = DualShock4DPadDirection.East;
                else if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadDown) &&
                         Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadLeft))
                    tempDPad = DualShock4DPadDirection.Southwest;
                else if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadDown))
                    tempDPad = DualShock4DPadDirection.South;
                else if (Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadLeft))
                    tempDPad = DualShock4DPadDirection.West;

                if (Inputs.Buttons.HasFlag(ControllerButtonFlags.Special))
                    tempSpecial |= DualShock4SpecialButton.Ps.Value;
                if (DS4Touch.OutputClickButton)
                    tempSpecial |= DualShock4SpecialButton.Touchpad.Value;

                outDS4Report.bSpecial = (byte)(tempSpecial | (0 << 2));
            }

            if (!IsSilenced)
            {
                outDS4Report.wButtons = tempButtons;
                outDS4Report.wButtons |= tempDPad.Value;

                outDS4Report.bTriggerL = (byte)Inputs.LeftTrigger;
                outDS4Report.bTriggerR = (byte)Inputs.RightTrigger;

                outDS4Report.bThumbLX = InputUtils.NormalizeXboxInput(LeftThumb.X);
                outDS4Report.bThumbLY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(LeftThumb.Y));
                outDS4Report.bThumbRX = InputUtils.NormalizeXboxInput(RightThumb.X);
                outDS4Report.bThumbRY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(RightThumb.Y));
            }

            unchecked
            {
                outDS4Report.bTouchPacketsN = 0x01;
                outDS4Report.sCurrentTouch.bPacketCounter = DS4Touch.TouchPacketCounter;
                outDS4Report.sCurrentTouch.bIsUpTrackingNum1 = (byte)DS4Touch.LeftPadTouch.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData1[0] = (byte)(DS4Touch.LeftPadTouch.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData1[1] =
                    (byte)(((DS4Touch.LeftPadTouch.X >> 8) & 0x0F) | ((DS4Touch.LeftPadTouch.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData1[2] = (byte)(DS4Touch.LeftPadTouch.Y >> 4);

                outDS4Report.sCurrentTouch.bIsUpTrackingNum2 = (byte)DS4Touch.RightPadTouch.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData2[0] = (byte)(DS4Touch.RightPadTouch.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData2[1] =
                    (byte)(((DS4Touch.RightPadTouch.X >> 8) & 0x0F) | ((DS4Touch.RightPadTouch.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData2[2] = (byte)(DS4Touch.RightPadTouch.Y >> 4);
            }

            // Use IMU sensor data, map to proper range, invert where needed
            if (IMU.AngularVelocity.ContainsKey(XInputSensorFlags.Default))
            {
                outDS4Report.wGyroX = (short)InputUtils.rangeMap(IMU.AngularVelocity[XInputSensorFlags.Default].X, DS4GyroscopeSensorSpec);    // gyroPitchFull
                outDS4Report.wGyroY = (short)InputUtils.rangeMap(-IMU.AngularVelocity[XInputSensorFlags.Default].Y, DS4GyroscopeSensorSpec);   // gyroYawFull
                outDS4Report.wGyroZ = (short)InputUtils.rangeMap(IMU.AngularVelocity[XInputSensorFlags.Default].Z, DS4GyroscopeSensorSpec);    // gyroRollFull
            }

            if (IMU.Acceleration.ContainsKey(XInputSensorFlags.Default))
            {
                outDS4Report.wAccelX = (short)InputUtils.rangeMap(-IMU.Acceleration[XInputSensorFlags.Default].X, DS4AccelerometerSensorSpec); // accelXFull
                outDS4Report.wAccelY = (short)InputUtils.rangeMap(-IMU.Acceleration[XInputSensorFlags.Default].Y, DS4AccelerometerSensorSpec); // accelYFull
                outDS4Report.wAccelZ = (short)InputUtils.rangeMap(IMU.Acceleration[XInputSensorFlags.Default].Z, DS4AccelerometerSensorSpec);  // accelZFull
            }

            outDS4Report.bBatteryLvlSpecial = 11;

            outDS4Report.wTimestamp = (ushort)(IMU.CurrentMicroseconds);

            DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);

            try
            {
                virtualController.SubmitRawReport(rawOutReportEx);
            }
            catch (VigemBusNotFoundException ex)
            {
                LogManager.LogCritical(ex.Message);
            }

            base.SubmitReport();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
