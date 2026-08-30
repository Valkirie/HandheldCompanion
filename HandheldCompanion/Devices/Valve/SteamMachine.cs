using HandheldCompanion.Processors.Valve;
using HandheldCompanion.Shared;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class SteamMachine : IDevice
{
    private readonly ValveLedService valveLedService = new();

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

        if (valveLedService.Initialize())
        {
            Capabilities |= DeviceCapabilities.DynamicLighting;
            Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
            DynamicLightingCapabilities |= LEDLevel.SolidColor;
            DynamicLightingCapabilities |= LEDLevel.Ambilight;
        }
        else
        {
            LogManager.LogWarning("Valve LED support is unavailable");
        }
    }

    public override bool SetLedStatus(bool status) => valveLedService.SetEnabled(status);

    public override bool SetLedBrightness(int brightness) => valveLedService.SetBrightness(brightness);

    public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
    {
        return level switch
        {
            LEDLevel.SolidColor => valveLedService.SetSolidColor(mainColor),
            LEDLevel.Ambilight => valveLedService.SetAmbilightColor(mainColor, secondaryColor),
            _ => false
        };
    }

    public override void Close()
    {
        valveLedService.Dispose();
        base.Close();
    }
}
