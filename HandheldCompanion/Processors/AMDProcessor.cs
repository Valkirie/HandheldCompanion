using HandheldCompanion.Devices;
using HandheldCompanion.Processors.AMD;
using System;

namespace HandheldCompanion.Processors;

public class AMDProcessor : Processor
{
    public readonly RyzenFamily family;
    public readonly IntPtr ry = IntPtr.Zero;

    public bool HasAllCoreCurve = false;
    public bool HasPerCoreCurve = false;
    public bool HasGpuCurve = false;

    public AMDProcessor()
    {
        try
        {
            ry = RyzenAdj.init_ryzenadj();
        }
        catch { /* ignore */ }

        if (ry == IntPtr.Zero)
            return;

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
            case RyzenFamily.FAM_KRACKANPOINT:
            case RyzenFamily.FAM_STRIXPOINT:
            case RyzenFamily.FAM_STRIXHALO:
            case RyzenFamily.FAM_DRAGONRANGE:
            case RyzenFamily.FAM_FIRERANGE:
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
            case RyzenFamily.FAM_KRACKANPOINT: /* Added to debug on KRK, STX, & STXH */
            case RyzenFamily.FAM_STRIXPOINT:
            case RyzenFamily.FAM_STRIXHALO:
                CanChangeTDP = true;
                break;
        }

        switch (family)
        {
            case RyzenFamily.FAM_CEZANNE:
            case RyzenFamily.FAM_RENOIR:
            case RyzenFamily.FAM_LUCIENNE:
            case RyzenFamily.FAM_REMBRANDT:
            case RyzenFamily.FAM_VANGOGH:
            case RyzenFamily.FAM_PHOENIX:
            case RyzenFamily.FAM_HAWKPOINT:
            case RyzenFamily.FAM_KRACKANPOINT:
            case RyzenFamily.FAM_STRIXPOINT:
            case RyzenFamily.FAM_STRIXHALO:
            case RyzenFamily.FAM_DRAGONRANGE:
            case RyzenFamily.FAM_FIRERANGE:
                HasAllCoreCurve = true;
                break;
        }

        switch (family)
        {
            case RyzenFamily.FAM_CEZANNE:
            case RyzenFamily.FAM_RENOIR:
            case RyzenFamily.FAM_LUCIENNE:
            case RyzenFamily.FAM_REMBRANDT:
            case RyzenFamily.FAM_VANGOGH:
            case RyzenFamily.FAM_PHOENIX:
            case RyzenFamily.FAM_HAWKPOINT:
            case RyzenFamily.FAM_KRACKANPOINT:
            case RyzenFamily.FAM_STRIXPOINT:
            case RyzenFamily.FAM_STRIXHALO:
            case RyzenFamily.FAM_DRAGONRANGE:
            case RyzenFamily.FAM_FIRERANGE:
                HasPerCoreCurve = true;
                break;
        }

        switch (family)
        {
            case RyzenFamily.FAM_CEZANNE:
            case RyzenFamily.FAM_RENOIR:
            case RyzenFamily.FAM_LUCIENNE:
            case RyzenFamily.FAM_REMBRANDT:
            case RyzenFamily.FAM_VANGOGH:
            case RyzenFamily.FAM_PHOENIX:
            case RyzenFamily.FAM_HAWKPOINT:
                HasGpuCurve = true;
                break;
        }

        HasAllCoreCurve = SetCoAll(0);
        HasPerCoreCurve = SetCoPer(0);
        HasGpuCurve = SetCoGfx(0);

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
                if (ry != IntPtr.Zero)
                {
                    // RyzenAdj use mW
                    switch (type)
                    {
                        case PowerType.Fast:
                            result = RyzenAdj.set_fast_limit(ry, (uint)(limit * 1000.0d));
                            break;
                        case PowerType.Slow:
                            result = RyzenAdj.set_slow_limit(ry, (uint)(limit * 1000.0d));
                            break;
                        case PowerType.Stapm:
                            result = RyzenAdj.set_stapm_limit(ry, (uint)(limit * 1000.0d));
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
                if (ry != IntPtr.Zero)
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
            }

            base.SetGPUClock(clock, result);
        }
    }

    public bool SetCoAll(int steps)
    {
        lock (updateLock)
        {
            if (!HasAllCoreCurve)
                return false;

            RyzenError error = RyzenError.ADJ_ERR_FAM_UNSUPPORTED;
            if (ry != IntPtr.Zero)
                error = RyzenAdj.set_coall(ry, RyzenAdj.EncodeCurveOffset(steps));

            return error == RyzenError.ADJ_ERR_SUCCESS;
        }
    }

    public bool SetCoPer(int steps)
    {
        lock (updateLock)
        {
            if (!HasPerCoreCurve)
                return false;

            RyzenError error = RyzenError.ADJ_ERR_FAM_UNSUPPORTED;
            if (ry != IntPtr.Zero)
                error = RyzenAdj.set_coper(ry, RyzenAdj.EncodeCurveOffset(steps));

            return error == RyzenError.ADJ_ERR_SUCCESS;
        }
    }

    public bool SetCoGfx(int steps)
    {
        lock (updateLock)
        {
            if (!HasGpuCurve)
                return false;

            RyzenError error = RyzenError.ADJ_ERR_FAM_UNSUPPORTED;
            if (ry != IntPtr.Zero)
                error = RyzenAdj.set_cogfx(ry, RyzenAdj.EncodeCurveOffset(steps));

            return error == RyzenError.ADJ_ERR_SUCCESS;
        }
    }
}