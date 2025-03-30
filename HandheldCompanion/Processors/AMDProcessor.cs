using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Processors.AMD;
using System;

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
                case RyzenFamily.FAM_PHOENIX:
                case RyzenFamily.FAM_HAWKPOINT:
                    // case RyzenFamily.FAM_STRIXPOINT:
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
                case RyzenFamily.FAM_PHOENIX:
                case RyzenFamily.FAM_HAWKPOINT:
                case RyzenFamily.FAM_STRIXPOINT:
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

        lock (updateLock)
        {
            bool UseOEM = (TDPMethod)ManagerFactory.settingsManager.GetInt("ConfigurableTDPMethod") == TDPMethod.OEM;
            IDevice device = IDevice.GetCurrent();

            if (device.Capabilities.HasFlag(DeviceCapabilities.OEMPower) && UseOEM)
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
                // 15W : 15000
                limit *= 1000;

                int error = 0;

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
            }
        }
    }

    public override void SetGPUClock(double clock, ref int result)
    {
        lock (updateLock)
        {
            bool restore = false;
            if (clock == 12750)
                restore = true;

            switch (family)
            {
                case RyzenFamily.FAM_VANGOGH:
                    {
                        using (VangoghGPU? sd = VangoghGPU.Open())
                        {
                            if (sd is null)
                                return;

                            sd.HardMinGfxClock = (uint)(restore ? IDevice.GetCurrent().GfxClock[0] : clock);
                            sd.SoftMaxGfxClock = (uint)(restore ? IDevice.GetCurrent().GfxClock[1] : clock);
                        }
                    }
                    break;

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

            base.SetGPUClock(clock, ref result);
        }
    }
}