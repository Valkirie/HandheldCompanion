using System;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace ControllerService.Targets;

internal class DualShock4Target : ViGEmTarget
{
    // DS4 Accelerometer g-force measurement range G SI unit to short
    // Various sources state either +/- 2 or 4 ranges are in use 
    private static readonly SensorSpec DS4AccelerometerSensorSpec = new()
    {
        minIn = -4.0f,
        maxIn = 4.0f,
        minOut = short.MinValue,
        maxOut = short.MaxValue
    };

    // DS4 Gyroscope angular rate measurement range deg/sec SI unit to short
    // Note, at +/- 2000 the value is still off by a factor 5
    private static readonly SensorSpec DS4GyroscopeSensorSpec = new()
    {
        minIn = -2000.0f,
        maxIn = 2000.0f,
        minOut = short.MinValue,
        maxOut = short.MaxValue
    };

    private DS4_REPORT_EX outDS4Report;

    private new readonly IDualShock4Controller virtualController;

    public DualShock4Target()
    {
        // initialize controller
        HID = HIDmode.DualShock4Controller;

        virtualController = ControllerService.vClient.CreateDualShock4Controller(0x054C, 0x09CC);
        virtualController.AutoSubmitReport = false;
        virtualController.FeedbackReceived += FeedbackReceived;

        LogManager.LogInformation("{0} initialized, {1}", ToString(), virtualController);
    }

    public override void Connect()
    {
        if (IsConnected)
            return;

        try
        {
            virtualController.Connect();
            TimerManager.Tick += UpdateReport;

            base.Connect();
        }
        catch (Exception ex)
        {
            virtualController.Disconnect();
            LogManager.LogWarning("Failed to connect {0}. {1}", ToString(), ex.Message);
        }
    }

    public override void Disconnect()
    {
        if (!IsConnected)
            return;

        try
        {
            virtualController.Disconnect();
            TimerManager.Tick -= UpdateReport;

            base.Disconnect();
        }
        catch
        {
        }
    }

    public void FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
    {
        // pass raw vibration to client
        PipeServer.SendMessage(new PipeClientVibration { LargeMotor = e.LargeMotor, SmallMotor = e.SmallMotor });
    }

    public override unsafe void UpdateReport(long ticks)
    {
        if (!IsConnected)
            return;

        base.UpdateReport(ticks);

        // reset vars
        var rawOutReportEx = new byte[63];
        ushort tempButtons = 0;
        ushort tempSpecial = 0;
        var tempDPad = DualShock4DPadDirection.None;

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
        }

        if (!IsSilenced)
        {
            outDS4Report.wButtons = tempButtons;
            outDS4Report.wButtons |= tempDPad.Value;

            outDS4Report.bTriggerL = (byte)Inputs.AxisState[AxisFlags.L2];
            outDS4Report.bTriggerR = (byte)Inputs.AxisState[AxisFlags.R2];

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
        if (IMU.AngularVelocity.TryGetValue(XInputSensorFlags.Default, out var velocity))
        {
            outDS4Report.wGyroX = (short)InputUtils.rangeMap(velocity.X, DS4GyroscopeSensorSpec); // gyroPitchFull
            outDS4Report.wGyroY = (short)InputUtils.rangeMap(-velocity.Y, DS4GyroscopeSensorSpec); // gyroYawFull
            outDS4Report.wGyroZ = (short)InputUtils.rangeMap(velocity.Z, DS4GyroscopeSensorSpec); // gyroRollFull
        }

        if (IMU.Acceleration.TryGetValue(XInputSensorFlags.Default, out var acceleration))
        {
            outDS4Report.wAccelX =
                (short)InputUtils.rangeMap(-acceleration.X, DS4AccelerometerSensorSpec); // accelXFull
            outDS4Report.wAccelY =
                (short)InputUtils.rangeMap(-acceleration.Y, DS4AccelerometerSensorSpec); // accelYFull
            outDS4Report.wAccelZ = (short)InputUtils.rangeMap(acceleration.Z, DS4AccelerometerSensorSpec); // accelZFull
        }

        outDS4Report.bBatteryLvlSpecial = 11;

        outDS4Report.wTimestamp = (ushort)TimerManager.GetElapsedSeconds();

        DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);

        try
        {
            virtualController.SubmitRawReport(rawOutReportEx);
        }
        catch (VigemBusNotFoundException ex)
        {
            LogManager.LogCritical(ex.Message);
        }
        catch (VigemInvalidTargetException ex)
        {
            LogManager.LogCritical(ex.Message);
        }

        SubmitReport();
    }

    public override void Dispose()
    {
        if (virtualController is not null)
            virtualController.Disconnect();

        base.Dispose();
    }
}