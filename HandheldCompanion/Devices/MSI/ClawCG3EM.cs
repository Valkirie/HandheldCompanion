using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HandheldCompanion.Devices.MSI
{
    public class ClawCG3EM : ClawA1M
    {
        public ClawCG3EM()
        {
            // device specific settings
            ProductIllustration = "device_msi_claw8ex";

            // https://www.intel.fr/content/www/us/en/products/sku/245626/intel-arc-g3-processor-12m-cache-up-to-4-60-ghz/specifications.html
            nTDP = new double[] { 15, 25, 30 };
            cTDP = new double[] { 20, 37 };
            GfxClock = new double[] { 100, 2200 };
            CpuClock = 4600;

            // todo: figure me
            GyroMatrix = new() { Axis = new Vector3(1.0f, 1.0f, -1.0f) };

            // overwrite ClawA1M default power profiles
            Dictionary<Guid, double[]> tdpOverrides = new Dictionary<Guid, double[]>
            {
                { BetterBatteryGuid,      new double[] { 15, 15, 20 } },
                { BetterPerformanceGuid,  new double[] { 25, 25, 37 } },
                { BestPerformanceGuid,    new double[] { 30, 30, 37 } }
            };

            foreach (KeyValuePair<Guid, double[]> kvp in tdpOverrides)
            {
                PowerProfile? profile = DevicePowerProfiles.FirstOrDefault(p => p.Guid == kvp.Key);
                profile?.TDPOverrideValues = kvp.Value;
            }
        }

        protected override int GetShiftModeValue(ShiftType shiftType)
        {
            return shiftType == ShiftType.User ? 6 : base.GetShiftModeValue(shiftType);
        }

        public override bool Open()
        {
            bool success = base.Open();
            if (!success)
                return false;

            // unlock TDP
            set_long_limit(30);
            set_short_limit(37);

            return true;
        }
    }
}
