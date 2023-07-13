using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using System.Numerics;

namespace ControllerCommon.Devices;

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
        return ECRamDirectWrite(0xF1, ECDetails, 0x40);
    }

    public override void Close()
    {
        LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);
        ECRamDirectWrite(0xF1, ECDetails, 0x00);
        base.Close();
    }
}