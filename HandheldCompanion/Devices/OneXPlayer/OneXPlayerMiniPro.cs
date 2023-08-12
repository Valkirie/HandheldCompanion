using System.Collections.Generic;
using System.Numerics;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class OneXPlayerMiniPro : OneXPlayerMini
{
    public OneXPlayerMiniPro()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2200 };

        AccelerationAxis = new Vector3(-1.0f, 1.0f, 1.0f);
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // allow OneX button to pass key inputs
        LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);

        ECRamDirectWrite(0x4F1, ECDetails, 0x40);
        ECRamDirectWrite(0x4F2, ECDetails, 0x02);

        return (ECRamReadByte(0x4F1, ECDetails) == 0x40 && ECRamReadByte(0x4F2, ECDetails) == 0x02);
    }

    public override void Close()
    {
        LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);
        ECRamDirectWrite(0x4F1, ECDetails, 0x00);
        ECRamDirectWrite(0x4F2, ECDetails, 0x00);
        base.Close();
    }
}