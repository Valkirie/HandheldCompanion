namespace HandheldCompanion.Devices;

public class DefaultDevice : IDevice
{
    public DefaultDevice()
    {
        // We assume all the devices have those keys
        // Disabled until we implement "turbo" type hotkeys

        /*
        OEMChords.Add(new DeviceChord("Volume Up",
            new List<KeyCode> { KeyCode.VolumeUp },
            new List<KeyCode> { KeyCode.VolumeUp },
            false, ButtonFlags.VolumeUp
        ));
        OEMChords.Add(new DeviceChord("Volume Down",
            new List<KeyCode> { KeyCode.VolumeDown },
            new List<KeyCode> { KeyCode.VolumeDown },
            false, ButtonFlags.VolumeDown
        ));
        */
    }
}