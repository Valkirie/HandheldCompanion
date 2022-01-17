using System.ComponentModel;

namespace ControllerCommon
{
    public enum HIDmode
    {
        [Description("No emulation")]
        None = 0,
        [Description("DualShock 4 emulation")]
        DualShock4Controller = 1,
        [Description("Xbox 360 emulation")]
        Xbox360Controller = 2,
    }
}
