using System.ComponentModel;

namespace ControllerCommon
{
    public enum HIDmode
    {
        [Description("Emulated DualShock 4 Controller")]
        DualShock4Controller = 0,
        [Description("Emulated XBOX 360 Controller")]
        Xbox360Controller = 1,
        [Description("No Controller")]
        None = 2,
    }

    public enum HIDstatus
    {
        [Description("Disconnected")]
        Disconnected = 0,
        [Description("Connected")]
        Connected = 1
    }
}
