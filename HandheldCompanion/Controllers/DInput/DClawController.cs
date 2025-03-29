using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SharpDX.DirectInput;

namespace HandheldCompanion.Controllers;

public class DClawController : DInputController
{
    public DClawController() : base()
    { }

    public DClawController(PnPDetails details) : base(details)
    {
        // Capabilities
        Capabilities |= ControllerCapabilities.Rumble;
    }

    protected override void InitializeInputOutput()
    {
        // Additional controller specific source buttons
        SourceButtons.Add(ButtonFlags.OEM3);
        SourceButtons.Add(ButtonFlags.OEM4);
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

    public override void UpdateInputs(long ticks, float delta)
    {
        // skip if controller isn't connected
        if (!IsConnected() || IsDisposing || IsDisposed || joystick.IsDisposed)
            return;

        ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

        try
        {
            // get state
            JoystickState state = joystick.GetCurrentState();

            Inputs.ButtonState[ButtonFlags.B1] = state.Buttons[1]; // A
            Inputs.ButtonState[ButtonFlags.B2] = state.Buttons[2]; // B
            Inputs.ButtonState[ButtonFlags.B3] = state.Buttons[0]; // X
            Inputs.ButtonState[ButtonFlags.B4] = state.Buttons[3]; // Y

            int pov = state.PointOfViewControllers[0];
            Inputs.ButtonState[ButtonFlags.DPadUp] = (pov == 0 || pov == 4500 || pov == 31500);
            Inputs.ButtonState[ButtonFlags.DPadRight] = (pov == 9000 || pov == 4500 || pov == 13500);
            Inputs.ButtonState[ButtonFlags.DPadDown] = (pov == 18000 || pov == 13500 || pov == 22500);
            Inputs.ButtonState[ButtonFlags.DPadLeft] = (pov == 27000 || pov == 31500 || pov == 22500);

            Inputs.ButtonState[ButtonFlags.L1] = state.Buttons[4];
            Inputs.ButtonState[ButtonFlags.R1] = state.Buttons[5];
            Inputs.ButtonState[ButtonFlags.L2Full] = state.Buttons[6];
            Inputs.ButtonState[ButtonFlags.R2Full] = state.Buttons[7];

            Inputs.ButtonState[ButtonFlags.Back] = state.Buttons[8];
            Inputs.ButtonState[ButtonFlags.Start] = state.Buttons[9];

            Inputs.ButtonState[ButtonFlags.LeftStickClick] = state.Buttons[10];
            Inputs.ButtonState[ButtonFlags.RightStickClick] = state.Buttons[11];

            Inputs.ButtonState[ButtonFlags.OEM3] = state.Buttons[15]; // M1
            Inputs.ButtonState[ButtonFlags.OEM4] = state.Buttons[16]; // M2

            Inputs.AxisState[AxisFlags.LeftStickX] = (short)InputUtils.MapRange(state.X, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.LeftStickY] = (short)InputUtils.MapRange(state.Y, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);

            Inputs.AxisState[AxisFlags.RightStickX] = (short)InputUtils.MapRange(state.Z, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.RightStickY] = (short)InputUtils.MapRange(state.RotationZ, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);

            Inputs.AxisState[AxisFlags.L2] = (byte)InputUtils.MapRange(state.RotationX, ushort.MinValue, ushort.MaxValue, byte.MinValue, byte.MaxValue);
            Inputs.AxisState[AxisFlags.R2] = (byte)InputUtils.MapRange(state.RotationY, ushort.MinValue, ushort.MaxValue, byte.MinValue, byte.MaxValue);
        }
        catch (SharpDX.SharpDXException ex)
        {
            if (ex.ResultCode == ResultCode.NotAcquired)
                joystick.Acquire();
            else if (ex.ResultCode == ResultCode.InputLost)
                AttachDetails(Details);
        }

        base.UpdateInputs(ticks, delta);
    }

    public override void SetVibration(byte LargeMotor, byte SmallMotor)
    {
        if (!IsConnected())
            return;

        joystickHid?.Write(new byte[] { 05, 01, 00, 00, (byte)(SmallMotor * VibrationStrength), (byte)(LargeMotor * VibrationStrength) });
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.B1:
                return "\u21D3"; // Button A
            case ButtonFlags.B2:
                return "\u21D2"; // Button B
            case ButtonFlags.B3:
                return "\u21D0"; // Button X
            case ButtonFlags.B4:
                return "\u21D1"; // Button Y
            case ButtonFlags.L1:
                return "\u2198";
            case ButtonFlags.R1:
                return "\u2199";
            case ButtonFlags.Back:
                return "\u21FA";
            case ButtonFlags.Start:
                return "\u21FB";
            case ButtonFlags.L2Soft:
                return "\u21DC";
            case ButtonFlags.L2Full:
                return "\u2196";
            case ButtonFlags.R2Soft:
                return "\u21DD";
            case ButtonFlags.R2Full:
                return "\u2197";
            case ButtonFlags.Special:
                return "\uE001";
        }

        return base.GetGlyph(button);
    }

    public override string GetGlyph(AxisFlags axis)
    {
        switch (axis)
        {
            case AxisFlags.L2:
                return "\u2196";
            case AxisFlags.R2:
                return "\u2197";
        }

        return base.GetGlyph(axis);
    }

    public override string GetGlyph(AxisLayoutFlags axis)
    {
        switch (axis)
        {
            case AxisLayoutFlags.L2:
                return "\u2196";
            case AxisLayoutFlags.R2:
                return "\u2197";
        }

        return base.GetGlyph(axis);
    }
}