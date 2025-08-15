using HandheldCompanion.Devices;

namespace HandheldCompanion.Controllers.MSI
{
    public class XClawController : XInputController
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

        public XClawController(PnPDetails details) : base(details)
        { }
    }
}
