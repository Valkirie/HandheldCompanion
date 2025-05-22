using HandheldCompanion.Devices;
using HandheldCompanion.Processors.Intel;

namespace HandheldCompanion.Processors;

public class IntelProcessor : Processor
{
    public readonly string family;
    public KX platform = new();

    public IntelProcessor()
    {
        bool PlatformInit = platform.init();

        // get family
        family = ProcessorID.Substring(ProcessorID.Length - 5);

        // check capabilities
        CanChangeTDP = PlatformInit || HasOEMCPU;
        CanChangeGPU = PlatformInit || HasOEMGPU;

        IsInitialized = CanChangeTDP || CanChangeGPU;
    }

    public override void SetTDPLimit(PowerType type, double limit, bool immediate, int result)
    {
        lock (updateLock)
        {
            if (!CanChangeTDP)
                return;

            IDevice device = IDevice.GetCurrent();

            // MSI OverBoost will disable VT-d in BIOS and can cause BSOD
            bool ForceOEM = device is ClawA1M claw && claw.GetOverBoost();

            if (HasOEMCPU && (UseOEM || ForceOEM))
            {
                switch (type)
                {
                    case PowerType.Slow:
                        device.set_long_limit((int)limit);
                        break;
                    case PowerType.Fast:
                        device.set_short_limit((int)limit);
                        break;
                }
            }
            else
            {
                switch (type)
                {
                    case PowerType.Slow:
                        result = platform.set_long_limit((int)limit);
                        break;
                    case PowerType.Fast:
                        result = platform.set_short_limit((int)limit);
                        break;
                }
            }

            base.SetTDPLimit(type, limit, immediate, result);
        }
    }

    public void SetMSRLimit(double PL1, double PL2)
    {
        lock (updateLock)
        {
            if (!CanChangeTDP)
                return;

            if (HasOEMCPU && UseOEM)
            {
                // do something
            }
            else
            {
                platform.set_msr_limits((int)PL1, (int)PL2);
            }
        }
    }

    public override void SetGPUClock(double clock, int result)
    {
        lock (updateLock)
        {
            if (!CanChangeGPU)
                return;

            if (HasOEMGPU)
            {
                // do something
            }
            else
            {
                result = platform.set_gfx_clk((int)clock);
            }

            base.SetGPUClock(clock, result);
        }
    }
}