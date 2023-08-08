using ControllerCommon;
using ControllerCommon.Controllers;
using SharpDX.DirectInput;

namespace HandheldCompanion.Controllers;

public class DInputController : IController
{
    public Joystick joystick;
    protected JoystickState State = new();

    public DInputController()
    {
    }

    public DInputController(Joystick joystick, PnPDetails details)
    {
        if (joystick is null)
            return;

        this.joystick = joystick;
        UserIndex = joystick.Properties.JoystickId;

        if (details is null)
            return;

        Details = details;
        Details.isHooked = true;

        // Set BufferSize in order to use buffered data.
        joystick.Properties.BufferSize = 128;

        // ui
        DrawControls();
        RefreshControls();
    }

    public override string ToString()
    {
        var baseName = base.ToString();
        if (!string.IsNullOrEmpty(baseName))
            return baseName;
        if (joystick is null)
            return string.Empty;
        return joystick.Information.ProductName;
    }

    public override void UpdateInputs(long ticks)
    {
        base.UpdateInputs(ticks);
    }

    public override bool IsConnected()
    {
        return (bool)!joystick?.IsDisposed;
    }

    public override void Plug()
    {
        if (joystick is not null)
            joystick.Acquire();

        base.Plug();
    }

    public override void Unplug()
    {
        // Unacquire the joystick
        if (joystick is not null)
            joystick.Unacquire();

        base.Unplug();
    }
}