using HandheldCompanion.Devices.AYANEO;

namespace HandheldCompanion.Devices;

public class AYANEOAIR2 : AYANEODeviceCEc
{
    public AYANEOAIR2()
    {
        ProductIllustration = "device_aya_air";
        ProductModel = "AYANEO AIR 2";

        rgbConfirmation = false;

        // AYASpace CEcControl::FanSetManual AB10: duty 0x1809 (percent * 0xB8 / 100), control 0x2F1
        ECDetails.AddressFanControl = 0x02F1;
        ECDetails.AddressFanDuty = 0x1809;
        ECDetails.FanValueMin = 0;
        ECDetails.FanValueMax = 0xB8;

        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2500 };
        CpuClock = 5000;
    }
}
