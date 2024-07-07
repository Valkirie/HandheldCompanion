using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Threading.Tasks;
using static JSL;

namespace HandheldCompanion.Controllers;

public class JSController : IController
{
    protected JOY_SETTINGS sSETTINGS;
    protected JOY_SHOCK_STATE sTATE;
    protected IMU_STATE iMU_STATE;

    protected float TriggerThreshold = 0.12f;
    protected float LeftThumbDeadZone = 0.24f;
    protected float RightThumbDeadZone = 0.265f;

    public JSController()
    { }

    public JSController(JOY_SETTINGS settings, PnPDetails details)
    {
        AttachJoySettings(settings);
        AttachDetails(details);

        // Capabilities
        Capabilities |= ControllerCapabilities.MotionSensor;

        // UI
        DrawUI();
        UpdateUI();
    }

    public override string ToString()
    {
        var baseName = base.ToString();
        if (!string.IsNullOrEmpty(baseName))
            return baseName;

        switch ((JOY_TYPE)sSETTINGS.controllerType)
        {
            case JOY_TYPE.DualShock4:
                return "DualShock 4";
        }

        return $"JoyShock Controller {UserIndex}";
    }

    public override void UpdateInputs(long ticks, float delta)
    {
        base.UpdateInputs(ticks, delta);
    }

    public virtual void UpdateState(float delta)
    {
        // skip if controller isn't connected
        if (!IsConnected())
            return;

        Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

        // pull state
        sTATE = JslGetSimpleState(UserIndex);

        Inputs.ButtonState[ButtonFlags.B1] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskS);
        Inputs.ButtonState[ButtonFlags.B2] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskE);
        Inputs.ButtonState[ButtonFlags.B3] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskW);
        Inputs.ButtonState[ButtonFlags.B4] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskN);

        Inputs.ButtonState[ButtonFlags.Back] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskMinus);
        Inputs.ButtonState[ButtonFlags.Start] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskPlus);

        Inputs.ButtonState[ButtonFlags.DPadUp] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskUp);
        Inputs.ButtonState[ButtonFlags.DPadDown] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskDown);
        Inputs.ButtonState[ButtonFlags.DPadLeft] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskLeft);
        Inputs.ButtonState[ButtonFlags.DPadRight] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskRight);

        Inputs.ButtonState[ButtonFlags.Special] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskHome);

        // Triggers
        Inputs.ButtonState[ButtonFlags.L1] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskL);
        Inputs.ButtonState[ButtonFlags.R1] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskR);

        Inputs.ButtonState[ButtonFlags.L2Soft] = sTATE.lTrigger > TriggerThreshold;
        Inputs.ButtonState[ButtonFlags.R2Soft] = sTATE.rTrigger > TriggerThreshold;

        Inputs.ButtonState[ButtonFlags.L2Full] = sTATE.lTrigger > TriggerThreshold * 8;
        Inputs.ButtonState[ButtonFlags.R2Full] = sTATE.rTrigger > TriggerThreshold * 8;

        Inputs.AxisState[AxisFlags.L2] = (short)InputUtils.MapRange(sTATE.lTrigger, 0.0f, 1.0f, byte.MinValue, byte.MaxValue);
        Inputs.AxisState[AxisFlags.R2] = (short)InputUtils.MapRange(sTATE.rTrigger, 0.0f, 1.0f, byte.MinValue, byte.MaxValue);

        // Left Stick
        Inputs.ButtonState[ButtonFlags.LeftStickLeft] = sTATE.stickLX < -LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftStickRight] = sTATE.stickLX > LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftStickDown] = sTATE.stickLY < -LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftStickUp] = sTATE.stickLY > LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftStickClick] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskLClick);

        Inputs.AxisState[AxisFlags.LeftStickX] = (short)InputUtils.MapRange(sTATE.stickLX, -1.0f, 1.0f, short.MinValue, short.MaxValue);
        Inputs.AxisState[AxisFlags.LeftStickY] = (short)InputUtils.MapRange(sTATE.stickLY, -1.0f, 1.0f, short.MinValue, short.MaxValue);

        // Right Stick
        Inputs.ButtonState[ButtonFlags.RightStickLeft] = sTATE.stickRX < -LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightStickRight] = sTATE.stickRX > LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightStickDown] = sTATE.stickRY < -LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightStickUp] = sTATE.stickRY > LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightStickClick] = BitwiseUtils.HasByteSet(sTATE.buttons, ButtonMaskRClick);

        Inputs.AxisState[AxisFlags.RightStickX] = (short)InputUtils.MapRange(sTATE.stickRX, -1.0f, 1.0f, short.MinValue, short.MaxValue);
        Inputs.AxisState[AxisFlags.RightStickY] = (short)InputUtils.MapRange(sTATE.stickRY, -1.0f, 1.0f, short.MinValue, short.MaxValue);

        // IMU
        iMU_STATE = JslGetIMUState(UserIndex);

        // store motion
        Inputs.GyroState.SetGyroscope(iMU_STATE.gyroX, iMU_STATE.gyroY, iMU_STATE.gyroZ);
        Inputs.GyroState.SetAccelerometer(iMU_STATE.accelX, iMU_STATE.accelY, iMU_STATE.accelZ);

        // process motion
        gamepadMotion.ProcessMotion(iMU_STATE.gyroX, iMU_STATE.gyroY, iMU_STATE.gyroZ, iMU_STATE.accelX, iMU_STATE.accelY, iMU_STATE.accelZ, delta);
    }

    public override bool IsConnected()
    {
        return JslStillConnected(UserIndex);
    }

    public override void Plug()
    {
        base.Plug();
    }

    public override void Unplug()
    {
        base.Unplug();
    }

    public override void SetVibration(byte LargeMotor, byte SmallMotor)
    {
        JslSetRumble(UserIndex, (byte)(SmallMotor * VibrationStrength), (byte)(LargeMotor * VibrationStrength));
    }

    public override void CyclePort()
    {
        string enumerator = Details.GetEnumerator();
        switch (enumerator)
        {
            default:
            case "BTHENUM":
                Task.Run(async () =>
                {
                    // Details.InstallNullDrivers();
                    // await Task.Delay(1000);
                    // Details.InstallCustomDriver("hidbth.inf");

                    Details.Uninstall(false);
                    await Task.Delay(3000);
                    Devcon.Refresh();
                });
                break;
            case "USB":
                base.CyclePort();
                break;
        }
    }

    public void AttachJoySettings(JOY_SETTINGS settings)
    {
        this.sSETTINGS = settings;
        this.UserIndex = (byte)settings.playerNumber;

        // manage elsewhere
        JslResetContinuousCalibration(UserIndex);
        JslPauseContinuousCalibration(UserIndex);
    }
}
