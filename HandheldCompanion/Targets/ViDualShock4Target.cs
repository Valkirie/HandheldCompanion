using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using System;

namespace HandheldCompanion.Targets
{
    /// <summary>
    /// ViGEM DualShock4 Controller target.
    /// </summary>
    internal class ViDualShock4Target : ViGEmTarget
    {
        private DS4_REPORT_EX outDS4Report;
        private IDualShock4Controller? _dualShockController;

        protected override string DeviceType => "dualshock4";
        protected override int InputLength => 64;

        public ViDualShock4Target(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
            HID = HIDmode.DualShock4Controller;

            LogManager.LogInformation("{0} initialized for ViGEM ({1:X4}:{2:X4})", ToString(), vendorId, productId);
        }

        protected override IVirtualGamepad CreateVirtualController()
        {
            try
            {
                if (VirtualManager.vClient == null)
                {
                    LogManager.LogError("VirtualManager.vClient is not initialized");
                    return null;
                }

                var controller = VirtualManager.vClient.CreateDualShock4Controller(vendorId, productId);
                _dualShockController = (IDualShock4Controller)controller;
                _dualShockController.AutoSubmitReport = false;
                _dualShockController.FeedbackReceived += FeedbackReceived;
                return controller;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create ViGEM DualShock4 controller: {0}", ex.Message);
                return null;
            }
        }

        private void FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
        {
            SendVibrate(e.LargeMotor, e.SmallMotor);
        }

        public override void UpdateInputs(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            if (!IsConnected || _dualShockController == null)
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

            unsafe
            {
                if (inputs.ButtonState[ButtonFlags.B1])
                    tempButtons |= DualShock4Button.Cross.Value;
                if (inputs.ButtonState[ButtonFlags.B2])
                    tempButtons |= DualShock4Button.Circle.Value;
                if (inputs.ButtonState[ButtonFlags.B3])
                    tempButtons |= DualShock4Button.Square.Value;
                if (inputs.ButtonState[ButtonFlags.B4])
                    tempButtons |= DualShock4Button.Triangle.Value;

                if (inputs.ButtonState[ButtonFlags.Start])
                    tempButtons |= DualShock4Button.Options.Value;
                if (inputs.ButtonState[ButtonFlags.Back])
                    tempButtons |= DualShock4Button.Share.Value;

                if (inputs.ButtonState[ButtonFlags.RightStickClick])
                    tempButtons |= DualShock4Button.ThumbRight.Value;
                if (inputs.ButtonState[ButtonFlags.LeftStickClick])
                    tempButtons |= DualShock4Button.ThumbLeft.Value;

                if (inputs.ButtonState[ButtonFlags.L1])
                    tempButtons |= DualShock4Button.ShoulderLeft.Value;
                if (inputs.ButtonState[ButtonFlags.R1])
                    tempButtons |= DualShock4Button.ShoulderRight.Value;

                if (inputs.AxisState[AxisFlags.L2] > 0)
                    tempButtons |= DualShock4Button.TriggerLeft.Value;
                if (inputs.AxisState[AxisFlags.R2] > 0)
                    tempButtons |= DualShock4Button.TriggerRight.Value;

                if (inputs.ButtonState[ButtonFlags.DPadUp] && inputs.ButtonState[ButtonFlags.DPadLeft])
                    tempDPad = DualShock4DPadDirection.Northwest;
                else if (inputs.ButtonState[ButtonFlags.DPadUp] && inputs.ButtonState[ButtonFlags.DPadRight])
                    tempDPad = DualShock4DPadDirection.Northeast;
                else if (inputs.ButtonState[ButtonFlags.DPadDown] && inputs.ButtonState[ButtonFlags.DPadLeft])
                    tempDPad = DualShock4DPadDirection.Southwest;
                else if (inputs.ButtonState[ButtonFlags.DPadDown] && inputs.ButtonState[ButtonFlags.DPadRight])
                    tempDPad = DualShock4DPadDirection.Southeast;
                else if (inputs.ButtonState[ButtonFlags.DPadUp])
                    tempDPad = DualShock4DPadDirection.North;
                else if (inputs.ButtonState[ButtonFlags.DPadDown])
                    tempDPad = DualShock4DPadDirection.South;
                else if (inputs.ButtonState[ButtonFlags.DPadLeft])
                    tempDPad = DualShock4DPadDirection.West;
                else if (inputs.ButtonState[ButtonFlags.DPadRight])
                    tempDPad = DualShock4DPadDirection.East;

                if (inputs.ButtonState[ButtonFlags.Special])
                    tempSpecial |= DualShock4SpecialButton.Ps.Value;
                if (inputs.ButtonState[ButtonFlags.LeftPadClick] || inputs.ButtonState[ButtonFlags.RightPadClick] || DS4Touch.OutputClickButton)
                    tempSpecial |= DualShock4SpecialButton.Touchpad.Value;

                outDS4Report.bSpecial = (byte)(tempSpecial | (0 << 2));

                outDS4Report.wButtons = tempButtons;
                outDS4Report.wButtons |= tempDPad.Value;

                outDS4Report.bTriggerL = (byte)inputs.AxisState[AxisFlags.L2];
                outDS4Report.bTriggerR = (byte)inputs.AxisState[AxisFlags.R2];

                outDS4Report.bThumbLX = InputUtils.NormalizeXboxInput(inputs.AxisState[AxisFlags.LeftStickX]);
                outDS4Report.bThumbLY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(inputs.AxisState[AxisFlags.LeftStickY]));
                outDS4Report.bThumbRX = InputUtils.NormalizeXboxInput(inputs.AxisState[AxisFlags.RightStickX]);
                outDS4Report.bThumbRY = (byte)(byte.MaxValue - InputUtils.NormalizeXboxInput(inputs.AxisState[AxisFlags.RightStickY]));

                outDS4Report.bTouchPacketsN = 0x01;
                outDS4Report.sCurrentTouch.bPacketCounter = DS4Touch.TouchPacketCounter;
                outDS4Report.sCurrentTouch.bIsUpTrackingNum1 = (byte)DS4Touch.LeftPadTouch.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData1[0] = (byte)(DS4Touch.LeftPadTouch.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData1[1] = (byte)(((DS4Touch.LeftPadTouch.X >> 8) & 0x0F) | ((DS4Touch.LeftPadTouch.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData1[2] = (byte)(DS4Touch.LeftPadTouch.Y >> 4);

                outDS4Report.sCurrentTouch.bIsUpTrackingNum2 = (byte)DS4Touch.RightPadTouch.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData2[0] = (byte)(DS4Touch.RightPadTouch.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData2[1] = (byte)(((DS4Touch.RightPadTouch.X >> 8) & 0x0F) | ((DS4Touch.RightPadTouch.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData2[2] = (byte)(DS4Touch.RightPadTouch.Y >> 4);
            }

            if (gamepadMotion is not null)
            {
                gamepadMotion.GetRawGyro(out float gx, out float gy, out float gz);
                gamepadMotion.GetRawAcceleration(out float ax, out float ay, out float az);
                outDS4Report.wGyroX = (short)InputUtils.rangeMap(gx, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
                outDS4Report.wGyroY = (short)InputUtils.rangeMap(gy, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
                outDS4Report.wGyroZ = (short)InputUtils.rangeMap(gz, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
                outDS4Report.wAccelX = (short)InputUtils.rangeMap(ax, -4.0f, 4.0f, short.MinValue, short.MaxValue);
                outDS4Report.wAccelY = (short)InputUtils.rangeMap(ay, -4.0f, 4.0f, short.MinValue, short.MaxValue);
                outDS4Report.wAccelZ = (short)InputUtils.rangeMap(az, -4.0f, 4.0f, short.MinValue, short.MaxValue);
                outDS4Report.wTimestamp += (ushort)(gamepadMotion.deltaTime * 100000.0f);
            }
            else
            {
                var gyro = inputs.GyroState.GetGyroscope(GyroState.SensorState.DSU);
                var accel = inputs.GyroState.GetAccelerometer(GyroState.SensorState.DSU);
                outDS4Report.wGyroX = (short)InputUtils.rangeMap(gyro.X, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
                outDS4Report.wGyroY = (short)InputUtils.rangeMap(gyro.Y, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
                outDS4Report.wGyroZ = (short)InputUtils.rangeMap(gyro.Z, -2000.0f, 2000.0f, short.MinValue, short.MaxValue);
                outDS4Report.wAccelX = (short)InputUtils.rangeMap(accel.X, -4.0f, 4.0f, short.MinValue, short.MaxValue);
                outDS4Report.wAccelY = (short)InputUtils.rangeMap(accel.Y, -4.0f, 4.0f, short.MinValue, short.MaxValue);
                outDS4Report.wAccelZ = (short)InputUtils.rangeMap(accel.Z, -4.0f, 4.0f, short.MinValue, short.MaxValue);
                outDS4Report.wTimestamp += (ushort)(TimerManager.GetDelta() * 100000.0f);
            }

            // todo: implement battery value based on device
            outDS4Report.bBatteryLvlSpecial = 11;

            DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);

            try
            {
                _dualShockController.SubmitRawReport(rawOutReportEx);
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

        protected override void UpdateVirtualController(IVirtualGamepad controller, byte[] reportData)
        {
            // Not used; UpdateInputs handles direct ViGEM API calls
        }

        public override void Dispose()
        {
            _dualShockController?.FeedbackReceived -= FeedbackReceived;

            try { _dualShockController?.Disconnect(); } catch { }
            _dualShockController = null;

            base.Dispose();
        }
    }
}
