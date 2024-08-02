using System;

namespace HandheldCompanion.Devices;

public class AOKZOEA1Pro : AOKZOEA1
{
    public AOKZOEA1Pro()
    {
        // device specific settings
        ProductIllustration = "device_aokzoe_a1";
        ProductModel = "AOKZOEA1Pro";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E, // 78
            AddressDataPort = 0x4F,     // 79
            FanValueMin = 0,
            FanValueMax = 184
        };
    }

    public override void SetFanDuty(double percent)
    {
        if (ECDetails.AddressFanDuty == 0)
            return;

        if (!IsOpen)
            return;

        var duty = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin;
        var data = Convert.ToByte(duty);

        ECRamDirectWrite(ECDetails.AddressFanDuty, ECDetails, data);
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        if (ECDetails.AddressFanControl == 0)
            return;

        if (!IsOpen)
            return;

        var data = Convert.ToByte(enable);
        ECRamDirectWrite(ECDetails.AddressFanControl, ECDetails, data);
    }
}