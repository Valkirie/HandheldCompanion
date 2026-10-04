using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using SharpDX.DirectInput;

namespace HandheldCompanion.Controllers.AYANEO;

public class AYANEODController : DInputController
{
    private readonly Notification dinputNotification = new(
        Properties.Resources.Hint_AYANEONext2DInput,
        Properties.Resources.Hint_AYANEONext2DInputDesc,
        severity: InfoBarSeverity.Warning);

    public AYANEODController() : base()
    { }

    public AYANEODController(PnPDetails details) : base(details)
    {
        ManagerFactory.notificationManager.Add(dinputNotification);
    }

    public override void Gone()
    {
        ManagerFactory.notificationManager.Discard(dinputNotification);
        base.Gone();
    }

    public override void Unplug()
    {
        ManagerFactory.notificationManager.Discard(dinputNotification);
        base.Unplug();
    }

    public override string ToString()
    {
        return "AYANEO DInput Controller";
    }

    public override void Tick(long ticks, float delta, bool commit)
    {
        if (!IsConnected() || IsBusy || !IsPlugged || _disposing || _disposed)
            return;

        ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

        try
        {
            if (joystick is null)
                return;

            JoystickState state = joystick.GetCurrentState();

            if (state.RotationX == 32767 && state.RotationY == 32767 && state.RotationZ == 32767)
                return;

            Inputs.ButtonState[ButtonFlags.B1] |= state.Buttons[1]; // A
            Inputs.ButtonState[ButtonFlags.B2] |= state.Buttons[2]; // B
            Inputs.ButtonState[ButtonFlags.B3] |= state.Buttons[0]; // X
            Inputs.ButtonState[ButtonFlags.B4] |= state.Buttons[3]; // Y

            int pov = state.PointOfViewControllers[0];
            Inputs.ButtonState[ButtonFlags.DPadUp] |= pov == 0 || pov == 4500 || pov == 31500;
            Inputs.ButtonState[ButtonFlags.DPadRight] |= pov == 9000 || pov == 4500 || pov == 13500;
            Inputs.ButtonState[ButtonFlags.DPadDown] |= pov == 18000 || pov == 13500 || pov == 22500;
            Inputs.ButtonState[ButtonFlags.DPadLeft] |= pov == 27000 || pov == 31500 || pov == 22500;

            Inputs.ButtonState[ButtonFlags.L1] |= state.Buttons[4];
            Inputs.ButtonState[ButtonFlags.R1] |= state.Buttons[5];
            Inputs.ButtonState[ButtonFlags.L2Full] |= state.Buttons[6];
            Inputs.ButtonState[ButtonFlags.R2Full] |= state.Buttons[7];
            Inputs.ButtonState[ButtonFlags.Back] |= state.Buttons[8];
            Inputs.ButtonState[ButtonFlags.Start] |= state.Buttons[9];
            Inputs.ButtonState[ButtonFlags.LeftStickClick] |= state.Buttons[10];
            Inputs.ButtonState[ButtonFlags.RightStickClick] |= state.Buttons[11];

            Inputs.AxisState[AxisFlags.LeftStickX] = (short)InputUtils.MapRange(state.X, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.LeftStickY] = (short)InputUtils.MapRange(state.Y, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.RightStickX] = (short)InputUtils.MapRange(state.Z, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.RightStickY] = (short)InputUtils.MapRange(state.RotationZ, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.L2] = (byte)InputUtils.MapRange(state.RotationX, ushort.MinValue, ushort.MaxValue, byte.MinValue, byte.MaxValue);
            Inputs.AxisState[AxisFlags.R2] = (byte)InputUtils.MapRange(state.RotationY, ushort.MinValue, ushort.MaxValue, byte.MinValue, byte.MaxValue);
        }
        catch (SharpDX.SharpDXException ex)
        {
            if (ex.ResultCode == ResultCode.NotAcquired && IsPlugged)
                Plug();
            else if (ex.ResultCode == ResultCode.InputLost && Details is not null)
                AttachDetails(Details);
        }

        base.Tick(ticks, delta, commit);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        return button switch
        {
            ButtonFlags.B1 => "\u21D3",
            ButtonFlags.B2 => "\u21D2",
            ButtonFlags.B3 => "\u21D0",
            ButtonFlags.B4 => "\u21D1",
            ButtonFlags.L1 => "\u2198",
            ButtonFlags.R1 => "\u2199",
            ButtonFlags.Back => "\u21FA",
            ButtonFlags.Start => "\u21FB",
            ButtonFlags.L2Soft => "\u21DC",
            ButtonFlags.L2Full => "\u2196",
            ButtonFlags.R2Soft => "\u21DD",
            ButtonFlags.R2Full => "\u2197",
            ButtonFlags.Special => "\uE001",
            _ => base.GetGlyph(button)
        };
    }

    public override string GetGlyph(AxisFlags axis)
    {
        return axis switch
        {
            AxisFlags.L2 => "\u2196",
            AxisFlags.R2 => "\u2197",
            _ => base.GetGlyph(axis)
        };
    }

    public override string GetGlyph(AxisLayoutFlags axis)
    {
        return axis switch
        {
            AxisLayoutFlags.L2 => "\u2196",
            AxisLayoutFlags.R2 => "\u2197",
            _ => base.GetGlyph(axis)
        };
    }
}
