namespace HandheldCompanion.Controllers.SDL
{
    public class NintendoSwitchProController : SDLController
    {
        public NintendoSwitchProController()
        { }

        public NintendoSwitchProController(nint gamepad, uint deviceIndex, PnPDetails details) : base(gamepad, deviceIndex, details)
        { }
    }
}
