using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Processors.AMD;
using System;
using System.Threading;

namespace HandheldCompanion.Processors;

public class AMDProcessor : Processor
{
    public RyzenFamily family;
    public IntPtr ry;

    public AMDProcessor()
    {
        ry = RyzenAdj.init_ryzenadj();

        if (ry == IntPtr.Zero)
        {
            IsInitialized = false;
        }
        else
        {
            family = RyzenAdj.get_cpu_family(ry);

            switch (family)
            {
                default:
                    CanChangeGPU = false;
                    break;

                case RyzenFamily.FAM_RENOIR:
                case RyzenFamily.FAM_LUCIENNE:
                case RyzenFamily.FAM_CEZANNE:
                case RyzenFamily.FAM_REMBRANDT:
                case RyzenFamily.FAM_MENDOCINO:
                case RyzenFamily.FAM_PHEONIX:
                    CanChangeGPU = true;
                    break;
                case RyzenFamily.FAM_VANGOGH:
                    CanChangeGPU = VangoghGPU.Detect() == VangoghGPU.DetectionStatus.Detected;
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
                case RyzenFamily.FAM_PHEONIX:
                    CanChangeTDP = true;
                    break;
            }

            IsInitialized = true;
        }
    }

    public override void SetTDPLimit(PowerType type, double limit, bool immediate, int result)
    {
        if (ry == IntPtr.Zero)
            return;

        if (Monitor.TryEnter(IsBusy))
        {
            // 15W : 15000
            limit *= 1000;

            var error = 0;

            switch (type)
            {
                case PowerType.Fast:
                    error = RyzenAdj.set_fast_limit(ry, (uint)limit);
                    break;
                case PowerType.Slow:
                    error = RyzenAdj.set_slow_limit(ry, (uint)limit);
                    break;
                case PowerType.Stapm:
                    error = RyzenAdj.set_stapm_limit(ry, (uint)limit);
                    break;
            }

            base.SetTDPLimit(type, limit, immediate, error);

            Monitor.Exit(IsBusy);
        }
    }

    public override void SetGPUClock(double clock, int result)
    {
        if (Monitor.TryEnter(IsBusy))
        {
            switch (family)
            {
                case RyzenFamily.FAM_VANGOGH:
                    {
                        using (var sd = VangoghGPU.Open())
                        {
                            if (sd is null)
                            {
                                base.SetGPUClock(clock, 1);
                                return;
                            }

                            if (clock == 12750)
                            {
                                sd.HardMinGfxClock = (uint)IDevice.GetCurrent().GfxClock[0]; //hardMin
                                sd.SoftMaxGfxClock = (uint)IDevice.GetCurrent().GfxClock[1]; //softMax
                            }
                            else
                            {
                                sd.HardMinGfxClock = (uint)clock; //hardMin
                                sd.SoftMaxGfxClock = (uint)clock; //softMax
                            }

                            base.SetGPUClock(clock, 0);
                        }
                    }
                    break;

                default:
                    {
                        // you can't restore default frequency on AMD GPUs
                        if (clock == 12750)
                            return;

                        int error = RyzenAdj.set_gfx_clk(ry, (uint)clock);

                        /*
                        if (clock == 12750)
                        {
                            error2 = RyzenAdj.set_min_gfxclk_freq(ry, (uint)IDevice.GetCurrent().GfxClock[0]);
                            error3 = RyzenAdj.set_max_gfxclk_freq(ry, (uint)IDevice.GetCurrent().GfxClock[1]);
                        }
                        else
                        {
                            error2 = RyzenAdj.set_min_gfxclk_freq(ry, (uint)clock);
                            error3 = RyzenAdj.set_max_gfxclk_freq(ry, (uint)clock);
                        }
                        */

                        base.SetGPUClock(clock, error);
                    }
                    break;
            }

            Monitor.Exit(IsBusy);
        }
    }

    public void SetCoall(uint value)
    {
        RyzenAdj.set_coall(ry, value);
    }
}