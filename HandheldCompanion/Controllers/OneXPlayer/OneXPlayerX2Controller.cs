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

        // The M1/M2 back paddles (L4/R4) are contributed by the device itself via
        // IDevice.InjectedControllerButtons (the single source of truth for injected buttons);
        // this controller only supplies their names/glyphs. The Home button is exposed as OEM3 via
        // the device vendor HID, so drop the duplicate Guide/Special entry (same physical button).
        SourceButtons.Remove(ButtonFlags.Special);
    }
}
