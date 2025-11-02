using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;

namespace HandheldCompanion.Devices
{
    public class OneXAOKZOE : IDevice
    {
        public override bool Open()
        {
            bool success = base.Open();
            if (!success)
                return false;

            // allow OneX turbo button to pass key inputs
            ECRamDirectWriteByte(0xF1, ECDetails, 0x40);
            ECRamDirectWriteByte(0xF2, ECDetails, 0x02);

            if (ECRamDirectReadByte(0xF1, ECDetails) == 0x40 && ECRamDirectReadByte(0xF2, ECDetails) == 0x02)
                LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);

            return success;
        }

        public override void Close()
        {
            ECRamDirectWriteByte(0xF1, ECDetails, 0x00);
            ECRamDirectWriteByte(0xF2, ECDetails, 0x00);

            if (ECRamDirectReadByte(0xF1, ECDetails) == 0x00 && ECRamDirectReadByte(0xF2, ECDetails) == 0x00)
                LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);

            base.Close();
        }
    }
}
