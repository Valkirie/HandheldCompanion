namespace HandheldCompanion.Utils;

public enum HIDmode
{
    NotSelected = -1,
    Xbox360Controller = 0,
    DualShock4Controller = 1,
    DualSenseController = 2,
    SteamDeckController = 3,
    SwitchProController = 4,
    Free = 5,
    SteamController = 6,
    NoController = 999,
}

public enum HIDstatus
{
    Disconnected = 0,
    Connected = 1
}

public enum VirtualManagerStatus
{
    Retrying = 0,
    Connected = 1,
    Failed = 2,
    Processing = 3,
    Ready = 4,
}