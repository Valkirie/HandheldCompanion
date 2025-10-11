using HandheldCompanion.Devices;

namespace HandheldCompanion.Controllers.MSI
{
    public class XboxAdaptiveController : XInputController
    {
        public XboxAdaptiveController()
        { }

        public XboxAdaptiveController(PnPDetails details) : base(details)
        { }

        public bool Enable()
        {
            if (IDevice.GetCurrent() is ROGAlly rogAlly)
                return rogAlly.XBoxController(false);
            return false;
        }

        public bool Disable()
        {
            if (IDevice.GetCurrent() is ROGAlly rogAlly)
                rogAlly.XBoxController(true);
            return false;
        }
    }
}
