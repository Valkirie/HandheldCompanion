using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using SharpDX.XInput;
using System.Collections.Generic;

namespace ControllerService.Targets
{
    internal class DualShock4Target : ViGEmTarget
    {
        private static readonly List<DualShock4Button> ButtonMap = new List<DualShock4Button>
        {
            DualShock4Button.ThumbRight,
            DualShock4Button.ThumbLeft,
            DualShock4Button.Options,
            DualShock4Button.Share,
            DualShock4Button.TriggerRight,
            DualShock4Button.TriggerLeft,
            DualShock4Button.ShoulderRight,
            DualShock4Button.ShoulderLeft,
            DualShock4Button.Triangle,
            DualShock4Button.Circle,
            DualShock4Button.Cross,
            DualShock4Button.Square,
            DualShock4SpecialButton.Ps,
            DualShock4SpecialButton.Touchpad
        };

        private static readonly List<DualShock4Axis> AxisMap = new List<DualShock4Axis>
        {
            DualShock4Axis.LeftThumbX,
            DualShock4Axis.LeftThumbY,
            DualShock4Axis.RightThumbX,
            DualShock4Axis.RightThumbY
        };

        private static readonly List<DualShock4Slider> SliderMap = new List<DualShock4Slider>
        {
            DualShock4Slider.LeftTrigger,
            DualShock4Slider.RightTrigger
        };

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

        public DualShock4Target(XInputController xinput, ViGEmClient client) : base(xinput, client)
        {
            // initialize controller
            HID = HIDmode.DualShock4Controller;

            virtualController = client.CreateDualShock4Controller();
            virtualController.AutoSubmitReport = false;
            virtualController.FeedbackReceived += FeedbackReceived;

            LogManager.LogInformation("{0} initialized, {1}", ToString(), virtualController);
        }

        public override void Connect()
        {
            if (IsConnected)
                return;

            virtualController.Connect();
            base.Connect();
        }

        public override void Disconnect()
        {
            if (!IsConnected)
                return;

            virtualController.Disconnect();
            base.Disconnect();
        }

        public void FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
        {
            if (!physicalController.IsConnected)
                return;

            Vibration inputMotor = new()
            {
                LeftMotorSpeed = (ushort)((e.LargeMotor * ushort.MaxValue / byte.MaxValue) * vibrationStrength),
                RightMotorSpeed = (ushort)((e.SmallMotor * ushort.MaxValue / byte.MaxValue) * vibrationStrength),
            };
            physicalController.SetVibration(inputMotor);
        }

        public override unsafe void UpdateReport(Gamepad Gamepad)
        {
            if (!IsConnected)
                return;

            base.UpdateReport(Gamepad);

            var Touch = xinputController.Touch;

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
                if (Buttons.HasFlag(GamepadButtonFlagsExt.A))
                    tempButtons |= DualShock4Button.Cross.Value;
                if (Buttons.HasFlag(GamepadButtonFlagsExt.B))
                    tempButtons |= DualShock4Button.Circle.Value;
                if (Buttons.HasFlag(GamepadButtonFlagsExt.X))
                    tempButtons |= DualShock4Button.Square.Value;
                if (Buttons.HasFlag(GamepadButtonFlagsExt.Y))
                    tempButtons |= DualShock4Button.Triangle.Value;

                if (Buttons.HasFlag(GamepadButtonFlagsExt.Start))
                    tempButtons |= DualShock4Button.Options.Value;
                if (Buttons.HasFlag(GamepadButtonFlagsExt.Back))
                    tempButtons |= DualShock4Button.Share.Value;

                if (Buttons.HasFlag(GamepadButtonFlagsExt.RightThumb))
                    tempButtons |= DualShock4Button.ThumbRight.Value;
                if (Buttons.HasFlag(GamepadButtonFlagsExt.LeftThumb))
                    tempButtons |= DualShock4Button.ThumbLeft.Value;

                if (Buttons.HasFlag(GamepadButtonFlagsExt.RightShoulder))
                    tempButtons |= DualShock4Button.ShoulderRight.Value;
                if (Buttons.HasFlag(GamepadButtonFlagsExt.LeftShoulder))
                    tempButtons |= DualShock4Button.ShoulderLeft.Value;

                if (Gamepad.LeftTrigger > 0)
                    tempButtons |= DualShock4Button.TriggerLeft.Value;
                if (Gamepad.RightTrigger > 0)
                    tempButtons |= DualShock4Button.TriggerRight.Value;

                if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadUp) &&
                    Buttons.HasFlag(GamepadButtonFlagsExt.DPadRight))
                    tempDPad = DualShock4DPadDirection.Northeast;
                else if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadUp) &&
                         Buttons.HasFlag(GamepadButtonFlagsExt.DPadLeft))
                    tempDPad = DualShock4DPadDirection.Northwest;
                else if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadUp))
                    tempDPad = DualShock4DPadDirection.North;
                else if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadRight) &&
                         Buttons.HasFlag(GamepadButtonFlagsExt.DPadDown))
                    tempDPad = DualShock4DPadDirection.Southeast;
                else if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadRight))
                    tempDPad = DualShock4DPadDirection.East;
                else if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadDown) &&
                         Buttons.HasFlag(GamepadButtonFlagsExt.DPadLeft))
                    tempDPad = DualShock4DPadDirection.Southwest;
                else if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadDown))
                    tempDPad = DualShock4DPadDirection.South;
                else if (Buttons.HasFlag(GamepadButtonFlagsExt.DPadLeft))
                    tempDPad = DualShock4DPadDirection.West;

                if (sState.wButtons.HasFlag(XInputStateButtons.Xbox))
                    tempSpecial |= DualShock4SpecialButton.Ps.Value;
                if (Touch.OutputClickButton)
                    tempSpecial |= DualShock4SpecialButton.Touchpad.Value;

                outDS4Report.bSpecial = (byte)(tempSpecial | (0 << 2));
            }

            if (!ControllerService.currentProfile.whitelisted)
            {
                outDS4Report.wButtons = tempButtons;
                outDS4Report.wButtons |= tempDPad.Value;

                outDS4Report.bTriggerL = Gamepad.LeftTrigger;
                outDS4Report.bTriggerR = Gamepad.RightTrigger;

                outDS4Report.bThumbLX = InputUtils.NormalizeXboxInput(LeftThumb.X);
                outDS4Report.bThumbLY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(LeftThumb.Y));
                outDS4Report.bThumbRX = InputUtils.NormalizeXboxInput(RightThumb.X);
                outDS4Report.bThumbRY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(RightThumb.Y));
            }

            unchecked
            {
                outDS4Report.bTouchPacketsN = 0x01;
                outDS4Report.sCurrentTouch.bPacketCounter = Touch.TouchPacketCounter;
                outDS4Report.sCurrentTouch.bIsUpTrackingNum1 = (byte)Touch.TrackPadTouch1.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData1[0] = (byte)(Touch.TrackPadTouch1.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData1[1] =
                    (byte)(((Touch.TrackPadTouch1.X >> 8) & 0x0F) | ((Touch.TrackPadTouch1.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData1[2] = (byte)(Touch.TrackPadTouch1.Y >> 4);

                outDS4Report.sCurrentTouch.bIsUpTrackingNum2 = (byte)Touch.TrackPadTouch2.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData2[0] = (byte)(Touch.TrackPadTouch2.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData2[1] =
                    (byte)(((Touch.TrackPadTouch2.X >> 8) & 0x0F) | ((Touch.TrackPadTouch2.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData2[2] = (byte)(Touch.TrackPadTouch2.Y >> 4);
            }

            // Use IMU sensor data, map to proper range, invert where needed
            outDS4Report.wGyroX = (short)InputUtils.rangeMap(xinputController.AngularVelocities[XInputSensorFlags.Default].X, DS4GyroscopeSensorSpec);    // gyroPitchFull
            outDS4Report.wGyroY = (short)InputUtils.rangeMap(-xinputController.AngularVelocities[XInputSensorFlags.Default].Y, DS4GyroscopeSensorSpec);   // gyroYawFull
            outDS4Report.wGyroZ = (short)InputUtils.rangeMap(xinputController.AngularVelocities[XInputSensorFlags.Default].Z, DS4GyroscopeSensorSpec);    // gyroRollFull

            outDS4Report.wAccelX = (short)InputUtils.rangeMap(-xinputController.Accelerations[XInputSensorFlags.Default].X, DS4AccelerometerSensorSpec); // accelXFull
            outDS4Report.wAccelY = (short)InputUtils.rangeMap(-xinputController.Accelerations[XInputSensorFlags.Default].Y, DS4AccelerometerSensorSpec); // accelYFull
            outDS4Report.wAccelZ = (short)InputUtils.rangeMap(xinputController.Accelerations[XInputSensorFlags.Default].Z, DS4AccelerometerSensorSpec);  // accelZFull

            outDS4Report.bBatteryLvlSpecial = 11;

            outDS4Report.wTimestamp = (ushort)(xinputController.CurrentMicroseconds);

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
            Disconnect();
            base.Dispose();
        }
    }
}
