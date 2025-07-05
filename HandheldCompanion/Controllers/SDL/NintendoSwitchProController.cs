using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
