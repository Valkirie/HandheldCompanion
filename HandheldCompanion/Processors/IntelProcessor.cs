using HandheldCompanion.Processors.Intel;
using System.Threading;

namespace HandheldCompanion.Processors;

public class IntelProcessor : Processor
{
    public string family;
    public KX platform = new();

    public IntelProcessor()
    {
        IsInitialized = platform.init();
        if (IsInitialized)
        {
            family = ProcessorID.Substring(ProcessorID.Length - 5);

            switch (family)
            {
                default:
                case "206A7": // SandyBridge
                case "306A9": // IvyBridge
                case "40651": // Haswell
                case "306D4": // Broadwell
                case "406E3": // Skylake
                case "906ED": // CoffeeLake
                case "806E9": // AmberLake
                case "706E5": // IceLake
                case "806C1": // TigerLake U
                case "806C2": // TigerLake U Refresh
                case "806D1": // TigerLake H
                case "906A2": // AlderLake-P
                case "906A3": // AlderLake-P
                case "906A4": // AlderLake-P
                case "90672": // AlderLake-S
                case "90675": // AlderLake-S
                    CanChangeTDP = true;
                    CanChangeGPU = true;
                    break;
            }
        }
    }

    public override void SetTDPLimit(PowerType type, double limit, bool immediate, int result)
    {
        lock (updateLock)
        {
            var error = 0;

            switch (type)
            {
                case PowerType.Slow:
                    error = platform.set_long_limit((int)limit);
                    break;
                case PowerType.Fast:
                    error = platform.set_short_limit((int)limit);
                    break;
            }

            base.SetTDPLimit(type, limit, immediate, error);
        }
    }

    public void SetMSRLimit(double PL1, double PL2)
    {
        platform.set_msr_limits((int)PL1, (int)PL2);
    }

    public override void SetGPUClock(double clock, int result)
    {
        lock (updateLock)
        {
            var error = platform.set_gfx_clk((int)clock);

            base.SetGPUClock(clock, error);
        }
    }
}