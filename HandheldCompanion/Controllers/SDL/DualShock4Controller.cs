using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers.SDL
{
    public class DualShock4Controller : SDLController
    {
        public DualShock4Controller()
        { }

        public DualShock4Controller(nint gamepad, uint deviceIndex, PnPDetails details) : base(gamepad, deviceIndex, details)
        { }
    }
}
