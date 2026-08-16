using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers;

// Custom controller for the ONEXPLAYER X2. Its gamepad presents as a generic Xbox 360 controller,
// so it reads exactly like an XInputController; this subclass only adds the M1/M2 back paddles
// (injected by the device over the vendor HID monitor) to the mappable button set, with a proper
// controller name and button labels (see Enum_OneXPlayerX2Controller_ButtonFlags_* resources).
public class OneXPlayerX2Controller : XInputController
{
    public OneXPlayerX2Controller() : base()
    { }

    public OneXPlayerX2Controller(PnPDetails details) : base(details)
    { }

    public override string ToString() => "ONEXPLAYER X2 Controller";

    protected override void InitializeInputOutput()
    {
        base.InitializeInputOutput();

        SourceButtons.Add(ButtonFlags.L4);
        SourceButtons.Add(ButtonFlags.R4);

        // Home is exposed as OEM3 by the vendor interface.
        SourceButtons.Remove(ButtonFlags.Special);
    }
}
