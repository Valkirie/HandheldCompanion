using HandheldCompanion.Devices;
using HandheldCompanion.Processors.AMD;
using System;

namespace HandheldCompanion.Processors;

public class AMDProcessor : Processor
{
    public readonly RyzenFamily family;
    public readonly IntPtr ry;

    public AMDProcessor()
    {
        ry = RyzenAdj.init_ryzenadj();
        if (ry != IntPtr.Zero)
        {
            family = RyzenAdj.get_cpu_family(ry);
            switch (family)
            {
                case RyzenFamily.FAM_RENOIR:
                case RyzenFamily.FAM_LUCIENNE:
                case RyzenFamily.FAM_CEZANNE:
                case RyzenFamily.FAM_VANGOGH:
                case RyzenFamily.FAM_REMBRANDT:
                case RyzenFamily.FAM_MENDOCINO:
                case RyzenFamily.FAM_PHOENIX:
                case RyzenFamily.FAM_HAWKPOINT:
                case RyzenFamily.FAM_KRACKANPOINT: /* Added to debug on KRK, STX, & STXH */
                case RyzenFamily.FAM_STRIXPOINT:
                case RyzenFamily.FAM_STRIXHALO:
                    CanChangeGPU = true;
                    break;
            }

            switch (family)
            {
                default:
                    CanChangeTDP = false;
                    break;

                case RyzenFamily.FAM_RAVEN:
                case RyzenFamily.FAM_PICASSO:
                case RyzenFamily.FAM_DALI:
                case RyzenFamily.FAM_RENOIR:
                case RyzenFamily.FAM_LUCIENNE:
                case RyzenFamily.FAM_CEZANNE:
                case RyzenFamily.FAM_VANGOGH:
                case RyzenFamily.FAM_REMBRANDT:
                case RyzenFamily.FAM_MENDOCINO:
                case RyzenFamily.FAM_PHOENIX:
                case RyzenFamily.FAM_HAWKPOINT:
                case RyzenFamily.FAM_KRACKANPOINT:
                case RyzenFamily.FAM_STRIXPOINT:
                case RyzenFamily.FAM_STRIXHALO:
                    CanChangeTDP = true;
                    break;
            }
        }

        // check capabilities
        CanChangeTDP |= HasOEMCPU;
        CanChangeGPU |= HasOEMGPU;

        IsInitialized = CanChangeTDP || CanChangeGPU;
    }

    public override void SetTDPLimit(PowerType type, double limit, bool immediate, int result)
    {
        lock (updateLock)
        {
            if (!CanChangeTDP)
                return;

            if (HasOEMCPU && UseOEM)
            {
                // get device
                IDevice device = IDevice.GetCurrent();

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
                // RyzenAdj use mW
                limit *= 1000.0d;

                if (ry != IntPtr.Zero)
                {
                    switch (type)
                    {
                        case PowerType.Fast:
                            result = RyzenAdj.set_fast_limit(ry, (uint)limit);
                            break;
                        case PowerType.Slow:
                            result = RyzenAdj.set_slow_limit(ry, (uint)limit);
                            break;
                        case PowerType.Stapm:
                            result = RyzenAdj.set_stapm_limit(ry, (uint)limit);
                            break;
                    }
                }
            }

            base.SetTDPLimit(type, limit, immediate, result);
        }
    }

    public override void SetGPUClock(double clock, int result)
    {
        lock (updateLock)
        {
            if (!CanChangeGPU)
                return;

            // get device
            IDevice device = IDevice.GetCurrent();

            bool restore = (clock == 12750);

            if (HasOEMGPU)
            {
                device.set_min_gfxclk_freq((uint)(restore ? IDevice.GetCurrent().GfxClock[0] : clock));
                device.set_max_gfxclk_freq((uint)(restore ? IDevice.GetCurrent().GfxClock[1] : clock));
            }
            else
            {
                switch (family)
                {
                    case RyzenFamily.FAM_RAVEN:
                    case RyzenFamily.FAM_PICASSO:
                    case RyzenFamily.FAM_DALI:
                    case RyzenFamily.FAM_LUCIENNE:
                        {
                            result = RyzenAdj.set_min_gfxclk_freq(ry, (uint)(restore ? IDevice.GetCurrent().GfxClock[0] : clock));
                            result = RyzenAdj.set_max_gfxclk_freq(ry, (uint)(restore ? IDevice.GetCurrent().GfxClock[1] : clock));
                        }
                        break;

                    default:
                        {
                            // you can't restore default frequency on AMD GPUs
                            if (restore)
                                return;

                            result = RyzenAdj.set_gfx_clk(ry, (uint)clock);
                        }
                        break;
                }
            }

            base.SetGPUClock(clock, result);
        }
    }
}