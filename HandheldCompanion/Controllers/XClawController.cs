using HandheldCompanion.Devices;

namespace HandheldCompanion.Controllers
{
    public class XClawController : SDLController
    {
        public override bool IsReady
        {
            get
            {
                if (IDevice.GetCurrent() is ClawA1M clawA1M)
                    return clawA1M.IsOpen;

                return false;
            }
        }

        public XClawController()
        { }

        public XClawController(nint gamepad, uint deviceIndex, PnPDetails details) : base(gamepad, deviceIndex, details)
        { }
    }
}
