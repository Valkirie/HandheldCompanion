using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Sensors;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using System;

namespace HandheldCompanion.Targets
{
    internal class DualShock4Target : ViGEmTarget
    {
        private DS4_REPORT_EX outDS4Report;

        private new IDualShock4Controller virtualController;

        public DualShock4Target() : base()
        {
            // initialize controller
            HID = HIDmode.DualShock4Controller;

            virtualController = VirtualManager.vClient.CreateDualShock4Controller(0x054C, 0x09CC);
            virtualController.AutoSubmitReport = false;
            virtualController.FeedbackReceived += FeedbackReceived;

            LogManager.LogInformation("{0} initialized, {1}", ToString(), virtualController);
        }

        public override bool Connect()
        {
            if (IsConnected)
                return true;

            try
            {
                virtualController.Connect();
                return base.Connect();
            }
            catch(Exception ex)
            {
                virtualController?.Disconnect();
                LogManager.LogWarning("Failed to connect {0}. {1}", this.ToString(), ex.Message);
                return false;
            }
        }

        public override bool Disconnect()
        {
            if (!IsConnected)
                return true;

            try
            {
                virtualController?.Disconnect();
                return base.Disconnect();
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to disconnect {0}. {1}", this.ToString(), ex.Message);
                return false;
            }
        }

        public void FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
        {
            SendVibrate(e.LargeMotor, e.SmallMotor);
        }

        public override unsafe void UpdateInputs(ControllerState Inputs, GamepadMotion gamepadMotion)
        {
            if (!IsConnected)
                return;

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
                if (Inputs.ButtonState[ButtonFlags.B1])
                    tempButtons |= DualShock4Button.Cross.Value;
                if (Inputs.ButtonState[ButtonFlags.B2])
                    tempButtons |= DualShock4Button.Circle.Value;
                if (Inputs.ButtonState[ButtonFlags.B3])
                    tempButtons |= DualShock4Button.Square.Value;
                if (Inputs.ButtonState[ButtonFlags.B4])
                    tempButtons |= DualShock4Button.Triangle.Value;

                if (Inputs.ButtonState[ButtonFlags.Start])
                    tempButtons |= DualShock4Button.Options.Value;
                if (Inputs.ButtonState[ButtonFlags.Back])
                    tempButtons |= DualShock4Button.Share.Value;

                if (Inputs.ButtonState[ButtonFlags.RightStickClick])
                    tempButtons |= DualShock4Button.ThumbRight.Value;
                if (Inputs.ButtonState[ButtonFlags.LeftStickClick])
                    tempButtons |= DualShock4Button.ThumbLeft.Value;

                if (Inputs.ButtonState[ButtonFlags.L1])
                    tempButtons |= DualShock4Button.ShoulderLeft.Value;
                if (Inputs.ButtonState[ButtonFlags.R1])
                    tempButtons |= DualShock4Button.ShoulderRight.Value;

                if (Inputs.AxisState[AxisFlags.L2] > 0)
                    tempButtons |= DualShock4Button.TriggerLeft.Value;
                if (Inputs.AxisState[AxisFlags.R2] > 0)
                    tempButtons |= DualShock4Button.TriggerRight.Value;

                if (Inputs.ButtonState[ButtonFlags.DPadUp] && Inputs.ButtonState[ButtonFlags.DPadLeft])
                    tempDPad = DualShock4DPadDirection.Northwest;
                else if (Inputs.ButtonState[ButtonFlags.DPadUp] && Inputs.ButtonState[ButtonFlags.DPadRight])
                    tempDPad = DualShock4DPadDirection.Northeast;
                else if (Inputs.ButtonState[ButtonFlags.DPadDown] && Inputs.ButtonState[ButtonFlags.DPadLeft])
                    tempDPad = DualShock4DPadDirection.Southwest;
                else if (Inputs.ButtonState[ButtonFlags.DPadDown] && Inputs.ButtonState[ButtonFlags.DPadRight])
                    tempDPad = DualShock4DPadDirection.Southeast;
                else if (Inputs.ButtonState[ButtonFlags.DPadUp])
                    tempDPad = DualShock4DPadDirection.North;
                else if (Inputs.ButtonState[ButtonFlags.DPadDown])
                    tempDPad = DualShock4DPadDirection.South;
                else if (Inputs.ButtonState[ButtonFlags.DPadLeft])
                    tempDPad = DualShock4DPadDirection.West;
                else if (Inputs.ButtonState[ButtonFlags.DPadRight])
                    tempDPad = DualShock4DPadDirection.East;

                if (Inputs.ButtonState[ButtonFlags.Special])
                    tempSpecial |= DualShock4SpecialButton.Ps.Value;
                if (Inputs.ButtonState[ButtonFlags.LeftPadClick] || Inputs.ButtonState[ButtonFlags.RightPadClick] || DS4Touch.OutputClickButton)
                    tempSpecial |= DualShock4SpecialButton.Touchpad.Value;

                outDS4Report.bSpecial = (byte)(tempSpecial | (0 << 2));

                outDS4Report.wButtons = tempButtons;
                outDS4Report.wButtons |= tempDPad.Value;

                outDS4Report.bTriggerL = (byte)Inputs.AxisState[AxisFlags.L2];
                outDS4Report.bTriggerR = (byte)Inputs.AxisState[AxisFlags.R2];

                outDS4Report.bThumbLX = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.LeftStickX]);
                outDS4Report.bThumbLY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.LeftStickY]));
                outDS4Report.bThumbRX = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.RightStickX]);
                outDS4Report.bThumbRY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.RightStickY]));

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

            // pull calibration data
            IMUCalibration calibration;
            if (gamepadMotion is not null)
                calibration = gamepadMotion.GetCalibration();

            // Use gyro sensor data, map to proper range, invert where needed
            outDS4Report.wGyroX = (short)InputUtils.rangeMap(Inputs.GyroState.Gyroscope[GyroState.SensorState.Raw].X, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
            outDS4Report.wGyroY = (short)InputUtils.rangeMap(Inputs.GyroState.Gyroscope[GyroState.SensorState.Raw].Y, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
            outDS4Report.wGyroZ = (short)InputUtils.rangeMap(Inputs.GyroState.Gyroscope[GyroState.SensorState.Raw].Z, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);

            outDS4Report.wAccelX = (short)InputUtils.rangeMap(Inputs.GyroState.Accelerometer[GyroState.SensorState.Raw].X, -4.0f, 4.0f, short.MinValue, short.MaxValue);
            outDS4Report.wAccelY = (short)InputUtils.rangeMap(Inputs.GyroState.Accelerometer[GyroState.SensorState.Raw].Y, -4.0f, 4.0f, short.MinValue, short.MaxValue);
            outDS4Report.wAccelZ = (short)InputUtils.rangeMap(Inputs.GyroState.Accelerometer[GyroState.SensorState.Raw].Z, -4.0f, 4.0f, short.MinValue, short.MaxValue);

            // todo: implement battery value based on device
            outDS4Report.bBatteryLvlSpecial = 11;

            // A common increment value between two reports is 188 (at full rate the report period is 1.25ms)
            if (gamepadMotion is not null)
                outDS4Report.wTimestamp += (ushort)(gamepadMotion.deltaTime * 100000.0f);

            DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);

            try
            {
                virtualController.SubmitRawReport(rawOutReportEx);
            }
            catch (VigemBusNotFoundException ex)
            {
                LogManager.LogError(ex.Message);
            }
            catch (VigemInvalidTargetException ex)
            {
                LogManager.LogError(ex.Message);
            }
        }

        public override void Dispose()
        {
            if (virtualController is not null)
                virtualController.Disconnect();

            base.Dispose();
        }
    }
}