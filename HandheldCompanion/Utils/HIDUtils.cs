namespace HandheldCompanion.Utils;

public enum HIDmode
{
    NotSelected = -1,
    Xbox360Controller = 0,
    DualShock4Controller = 1,
    DInputController = 2,
    NoController = 3,
}

public enum HIDstatus
{
    Disconnected = 0,
    Connected = 1
}