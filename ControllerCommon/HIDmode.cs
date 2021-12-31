using System.ComponentModel;

namespace ControllerCommon
{
    public enum HIDmode
    {
        [Description("DualShock 4")]
        DualShock4Controller = 0,
        [Description("Xbox 360")]
        Xbox360Controller = 1,
        [Description("None")]
        None = 2,
    }
}
