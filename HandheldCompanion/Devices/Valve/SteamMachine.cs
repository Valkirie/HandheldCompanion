namespace HandheldCompanion.Devices;

public class SteamMachine : IDevice
{
    public SteamMachine()
    {
        ProductIllustration = "device_valve_fremont";
        ProductModel = "SteamMachine";
        DeviceType = "Desktop";

        // Valve Fremont uses a six-core Zen 4 / RDNA 3 semi-custom APU.
        nTDP = new double[] { 35, 35, 45 };
        cTDP = new double[] { 35, 45 };
        GfxClock = new double[] { 800, 2800 };
        CpuClock = 4800;
    }
}
