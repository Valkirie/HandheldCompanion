using HandheldCompanion.Devices;
using HandheldCompanion.Processors.AMD;

namespace HandheldCompanion.Processors;

public class AMDProcessor : Processor
{
    public RyzenSmuService ryzenSmuService = new();

    public bool HasAllCoreCurve = false;
    public bool HasPerCoreCurve = false;
    public bool HasGpuCurve = false;

    public AMDProcessor()
    {
        if (!ryzenSmuService.Initialize())
            return;

        CanChangeTDP = ryzenSmuService.CanSetTDP();
        CanChangeGPU = ryzenSmuService.CanSetGfxClk();

        HasAllCoreCurve = ryzenSmuService.CanSetCoAll() && ryzenSmuService.SetCoAll(0);
        HasPerCoreCurve = ryzenSmuService.CanSetCoPer() && ryzenSmuService.SetCoPer(0);
        HasGpuCurve = ryzenSmuService.CanSetCoGfx() && ryzenSmuService.SetCoGfx(0);

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
                switch (type)
                {
                    case PowerType.Fast:
                        result = ryzenSmuService.SetFastLimit((uint)limit) ? 0 : -1;
                        break;
                    case PowerType.Slow:
                        result = ryzenSmuService.SetSlowLimit((uint)limit) ? 0 : -1;
                        break;
                    case PowerType.Stapm:
                        result = ryzenSmuService.SetStapmLimit((uint)limit) ? 0 : -1;
                        break;
                }
            }

            base.SetTDPLimit(type, limit, immediate, result);
        }
    }

    public override uint GetTDPLimit(PowerType type)
    {
        float value = 0.0f;
        switch (type)
        {
            case PowerType.Slow:
                ryzenSmuService.TryGetSlowLimit(out value);
                break;
            case PowerType.Stapm:
                ryzenSmuService.TryGetStapmLimit(out value);
                break;
            case PowerType.Fast:
                ryzenSmuService.TryGetFastLimit(out value);
                break;
        }
        return (uint)value;
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
                switch (ryzenSmuService.CpuCodeName)
                {
                    case CpuCodeName.RavenRidge:
                    case CpuCodeName.Picasso:
                    //case CpuCodeName.Dali:
                    case CpuCodeName.Lucienne:
                        {
                            result = ryzenSmuService.SetMinGfxClkFreq((uint)(restore ? IDevice.GetCurrent().GfxClock[0] : clock)) ? 0 : -1;
                            result = ryzenSmuService.SetMaxGfxClkFreq((uint)(restore ? IDevice.GetCurrent().GfxClock[1] : clock)) ? 0 : -1;
                        }
                        break;

                        default:
                            {
                                // you can't restore default frequency on AMD GPUs
                                if (restore)
                                    return;

                            result = ryzenSmuService.SetGfxClk((uint)clock) ? 0 : -1;
                        }
                        break;
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

            return ryzenSmuService.SetCoAll(steps);
        }
    }

    public bool SetCoPer(int steps)
    {
        lock (updateLock)
        {
            if (!HasPerCoreCurve)
                return false;

            return ryzenSmuService.SetCoPer(steps);
        }
    }

    public bool SetCoGfx(int steps)
    {
        lock (updateLock)
        {
            if (!HasGpuCurve)
                return false;

            return ryzenSmuService.SetCoGfx(steps);
        }
    }
}