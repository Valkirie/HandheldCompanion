using System;

namespace HandheldCompanion.Controllers
{
    [Flags]
    public enum ControllerCapabilities : ushort
    {
        None = 0,
        MotionSensor = 1,
    }
}
