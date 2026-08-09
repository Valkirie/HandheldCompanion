using HandheldCompanion.Devices;

namespace HandheldCompanion.Devices.ASUS;

public class ASUS : IDevice
{
    public override bool IsOpen => DeviceOpen && AsusACPI.IsOpen;

    public override bool Open()
    {
        if (!base.Open() || !AsusACPI.Open())
            return false;

        if (AsusACPI.IsSupported(AsusACPI.GPUXG))
            Capabilities |= DeviceCapabilities.XGMobile;

        return true;
    }
}
