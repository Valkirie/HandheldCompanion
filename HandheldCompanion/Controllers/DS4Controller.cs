using System;
using System.Windows.Media;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SharpDX.DirectInput;
using SharpDX.XInput;
using static JSL;

namespace HandheldCompanion.Controllers;

public class DS4Controller : DInputController
{
    private int joyShockId;

    public DS4Controller()
    {
    }

    public DS4Controller(Joystick joystick, PnPDetails details) : base(joystick, details)
    {
        this.UserIndex = joystick.Properties.JoystickId;

        Capabilities |= ControllerCapabilities.MotionSensor;

        // JSL
        int connectedJoys = JslConnectDevices();
        int[] joysHandle = new int[connectedJoys];
        JslGetConnectedDeviceHandles(joysHandle, connectedJoys);

        // scroll handles until we find matching device path
        foreach(int i in joysHandle)
        {
            JOY_SETTINGS settings = JslGetControllerInfoAndSettings(i);

            // fixme
            string joyShockpath = $"V{settings.path}".ToUpper();
            string detailsPath = details.Path.Replace(@"\", "").Replace("?HID#","");

            if (joyShockpath.Equals(detailsPath, StringComparison.InvariantCultureIgnoreCase))
            {
                joyShockId = i;
                break;
            }
        }

        int joyShockType = JslGetControllerType(joyShockId);
        string joyShockName = joyShockNames[joyShockType];

        // had the feeling this was causing some drifting
        JslPauseContinuousCalibration(joyShockId);

        // UI
        ColoredButtons.Add(ButtonFlags.B1, new SolidColorBrush(Color.FromArgb(255, 116, 139, 255)));
        ColoredButtons.Add(ButtonFlags.B2, new SolidColorBrush(Color.FromArgb(255, 255, 73, 75)));
        ColoredButtons.Add(ButtonFlags.B3, new SolidColorBrush(Color.FromArgb(255, 244, 149, 193)));
        ColoredButtons.Add(ButtonFlags.B4, new SolidColorBrush(Color.FromArgb(255, 73, 191, 115)));

        // Additional controller specific source buttons
        SourceButtons.Add(ButtonFlags.LeftPadClick);
        SourceButtons.Add(ButtonFlags.LeftPadTouch);
        SourceButtons.Add(ButtonFlags.RightPadTouch);

        SourceAxis.Add(AxisLayoutFlags.LeftPad);
        SourceAxis.Add(AxisLayoutFlags.RightPad);
        SourceAxis.Add(AxisLayoutFlags.Gyroscope);
    }

    public override void UpdateInputs(long ticks)
    {
        // skip if controller isn't connected
        if (!IsConnected())
            return;

        try
        {
            // Poll events from joystick
            joystick.Poll();

            // update gamepad state
            State = joystick.GetCurrentState();
        }
        catch
        {
        }

        Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

        Inputs.ButtonState[ButtonFlags.B1] = State.Buttons[1];
        Inputs.ButtonState[ButtonFlags.B2] = State.Buttons[2];
        Inputs.ButtonState[ButtonFlags.B3] = State.Buttons[0];
        Inputs.ButtonState[ButtonFlags.B4] = State.Buttons[3];

        Inputs.ButtonState[ButtonFlags.Back] = State.Buttons[8];
        Inputs.ButtonState[ButtonFlags.Start] = State.Buttons[9];

        Inputs.ButtonState[ButtonFlags.L2Soft] = State.Buttons[6];
        Inputs.ButtonState[ButtonFlags.R2Soft] = State.Buttons[7];

        Inputs.ButtonState[ButtonFlags.LeftStickClick] = State.Buttons[10];
        Inputs.ButtonState[ButtonFlags.RightStickClick] = State.Buttons[11];

        Inputs.ButtonState[ButtonFlags.L1] = State.Buttons[4];
        Inputs.ButtonState[ButtonFlags.R1] = State.Buttons[5];

        Inputs.ButtonState[ButtonFlags.Special] = State.Buttons[12];

        // Left Pad
        Inputs.ButtonState[ButtonFlags.LeftPadClick] = State.Buttons[13];
        Inputs.ButtonState[ButtonFlags.LeftPadTouch] = JslGetTouchDown(joyShockId);

        if (Inputs.ButtonState[ButtonFlags.LeftPadTouch])
        {
            float joyShockX0 = JslGetTouchX(joyShockId);
            float joyShockY0 = JslGetTouchY(joyShockId);

            Inputs.AxisState[AxisFlags.LeftPadX] = (short)InputUtils.MapRange(joyShockX0, 0.0f, 1.0f, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.LeftPadY] = (short)InputUtils.MapRange(joyShockY0, 0.0f, 1.0f, short.MaxValue, short.MinValue);
        }
        else
        {
            Inputs.AxisState[AxisFlags.LeftPadX] = 0;
            Inputs.AxisState[AxisFlags.LeftPadY] = 0;
        }

        // Right Pad
        Inputs.ButtonState[ButtonFlags.RightPadTouch] = JslGetTouchDown(joyShockId, true);

        if (Inputs.ButtonState[ButtonFlags.RightPadTouch])
        {
            float joyShockX1 = JslGetTouchX(joyShockId, true);
            float joyShockY1 = JslGetTouchY(joyShockId, true);

            Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange(joyShockX1, 0.0f, 1.0f, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange(joyShockY1, 0.0f, 1.0f, short.MaxValue, short.MinValue);
        }
        else
        {
            Inputs.AxisState[AxisFlags.RightPadX] = 0;
            Inputs.AxisState[AxisFlags.RightPadY] = 0;
        }

        switch (State.PointOfViewControllers[0])
        {
            case 0:
                Inputs.ButtonState[ButtonFlags.DPadUp] = true;
                break;
            case 4500:
                Inputs.ButtonState[ButtonFlags.DPadUp] = true;
                Inputs.ButtonState[ButtonFlags.DPadRight] = true;
                break;
            case 9000:
                Inputs.ButtonState[ButtonFlags.DPadRight] = true;
                break;
            case 13500:
                Inputs.ButtonState[ButtonFlags.DPadDown] = true;
                Inputs.ButtonState[ButtonFlags.DPadRight] = true;
                break;
            case 18000:
                Inputs.ButtonState[ButtonFlags.DPadDown] = true;
                break;
            case 22500:
                Inputs.ButtonState[ButtonFlags.DPadLeft] = true;
                Inputs.ButtonState[ButtonFlags.DPadDown] = true;
                break;
            case 27000:
                Inputs.ButtonState[ButtonFlags.DPadLeft] = true;
                break;
            case 31500:
                Inputs.ButtonState[ButtonFlags.DPadLeft] = true;
                Inputs.ButtonState[ButtonFlags.DPadUp] = true;
                break;
        }

        Inputs.AxisState[AxisFlags.L2] = (short)(State.RotationX * byte.MaxValue / ushort.MaxValue);
        Inputs.AxisState[AxisFlags.R2] = (short)(State.RotationY * byte.MaxValue / ushort.MaxValue);

        Inputs.ButtonState[ButtonFlags.L2Full] = Inputs.AxisState[AxisFlags.L2] > Gamepad.TriggerThreshold * 8;
        Inputs.ButtonState[ButtonFlags.R2Full] = Inputs.AxisState[AxisFlags.R2] > Gamepad.TriggerThreshold * 8;

        Inputs.AxisState[AxisFlags.LeftStickX] =
            (short)Math.Clamp(State.X - short.MaxValue, short.MinValue, short.MaxValue);
        Inputs.AxisState[AxisFlags.LeftStickY] =
            (short)Math.Clamp(-State.Y + short.MaxValue, short.MinValue, short.MaxValue);

        Inputs.AxisState[AxisFlags.RightStickX] =
            (short)Math.Clamp(State.Z - short.MaxValue, short.MinValue, short.MaxValue);
        Inputs.AxisState[AxisFlags.RightStickY] =
            (short)Math.Clamp(-State.RotationZ + short.MaxValue, short.MinValue, short.MaxValue);

        IMU_STATE joyShockState = JslGetIMUState(joyShockId);
        Inputs.GyroState.Accelerometer.X = -joyShockState.accelX;
        Inputs.GyroState.Accelerometer.Y = -joyShockState.accelY;
        Inputs.GyroState.Accelerometer.Z = joyShockState.accelZ;

        Inputs.GyroState.Gyroscope.X = joyShockState.gyroX;
        Inputs.GyroState.Gyroscope.Y = -joyShockState.gyroY;
        Inputs.GyroState.Gyroscope.Z = joyShockState.gyroZ;

        base.UpdateInputs(ticks);
    }

    public override bool IsConnected()
    {
        return joystick is null ? false : !joystick.IsDisposed;
    }

    public override void Plug()
    {
        TimerManager.Tick += UpdateInputs;
        base.Plug();
    }

    public override void Unplug()
    {
        TimerManager.Tick -= UpdateInputs;
        base.Unplug();
    }

    public override void Cleanup()
    {
        TimerManager.Tick -= UpdateInputs;
    }

    public override void SetVibration(byte LargeMotor, byte SmallMotor)
    {
        JslSetRumble(joyShockId, (byte)(SmallMotor * VibrationStrength), (byte)(LargeMotor * VibrationStrength));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.B1:
                return "\u21E3"; // Cross
            case ButtonFlags.B2:
                return "\u21E2"; // Circle
            case ButtonFlags.B3:
                return "\u21E0"; // Square
            case ButtonFlags.B4:
                return "\u21E1"; // Triangle
            case ButtonFlags.L1:
                return "\u21B0";
            case ButtonFlags.R1:
                return "\u21B1";
            case ButtonFlags.Back:
                return "\u21E6";
            case ButtonFlags.Start:
                return "\u21E8";
            case ButtonFlags.L2Soft:
                return "\u21DC";
            case ButtonFlags.L2Full:
                return "\u21B2";
            case ButtonFlags.R2Soft:
                return "\u21DD";
            case ButtonFlags.R2Full:
                return "\u21B3";
            case ButtonFlags.Special:
                return "\uE000";
            case ButtonFlags.LeftPadClick:
            case ButtonFlags.RightPadClick:
                return "\u21E7";
        }

        return base.GetGlyph(button);
    }

    public override string GetGlyph(AxisFlags axis)
    {
        switch (axis)
        {
            case AxisFlags.L2:
                return "\u21B2";
            case AxisFlags.R2:
                return "\u21B3";
        }

        return base.GetGlyph(axis);
    }

    public override string GetGlyph(AxisLayoutFlags axis)
    {
        switch (axis)
        {
            case AxisLayoutFlags.L2:
                return "\u21B2";
            case AxisLayoutFlags.R2:
                return "\u21B3";
        }

        return base.GetGlyph(axis);
    }
}