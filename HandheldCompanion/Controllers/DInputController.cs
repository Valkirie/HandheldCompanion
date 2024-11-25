using SharpDX.DirectInput;

namespace HandheldCompanion.Controllers;

public class DInputController : IController
{
    public Joystick joystick;
    protected JoystickState State = new();

    public DInputController()
    { }

    public DInputController(Joystick joystick, PnPDetails details)
    {
        if (joystick is null)
            return;

        this.joystick = joystick;
        UserIndex = (byte)joystick.Properties.JoystickId;

        if (details is null)
            return;

        Details = details;
        Details.isHooked = true;

        // Set BufferSize in order to use buffered data.
        joystick.Properties.BufferSize = 128;
    }

    public override string ToString()
    {
        string baseName = base.ToString();
        if (!string.IsNullOrEmpty(baseName))
            return baseName;
        if (!string.IsNullOrEmpty(joystick?.Information.ProductName))
            return joystick.Information.ProductName;
        return $"DInput Controller {UserIndex}";
    }

    public override void UpdateInputs(long ticks, float delta)
    {
        base.UpdateInputs(ticks, delta);
    }

    public override bool IsConnected()
    {
        return (bool)!joystick?.IsDisposed;
    }

    public override void Plug()
    {
        joystick?.Acquire();

        base.Plug();
    }

    public override void Unplug()
    {
        // Unacquire the joystick
        joystick?.Unacquire();

        base.Unplug();
    }
}