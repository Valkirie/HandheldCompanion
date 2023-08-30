using HandheldCompanion.Helpers;
using HandheldCompanion.Processors.AMD;
using HandheldCompanion.Views;
using System;
using System.Threading;
using System.Timers;

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

        foreach (var type in (PowerType[])Enum.GetValues(typeof(PowerType)))
        {
            // write default limits
            m_Limits[type] = 0;
            m_PrevLimits[type] = 0;

            /*
            // write default values
            m_Values[type] = 0;
            m_PrevValues[type] = 0;
            */
        }
    }

    public override void Initialize()
    {
        updateTimer.Elapsed += UpdateTimer_Elapsed;
        base.Initialize();
    }

    public override void Stop()
    {
        updateTimer.Elapsed -= UpdateTimer_Elapsed;
        base.Stop();
    }

    protected override void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (Monitor.TryEnter(IsBusy))
        {
            RyzenAdj.get_table_values(ry);
            RyzenAdj.refresh_table(ry);

            // read limit(s)
            var limit_fast = (int)RyzenAdj.get_fast_limit(ry);
            var limit_slow = (int)RyzenAdj.get_slow_limit(ry);
            var limit_stapm = (int)RyzenAdj.get_stapm_limit(ry);

            if (limit_fast != 0)
                m_Limits[PowerType.Fast] = limit_fast;
            if (limit_slow != 0)
                m_Limits[PowerType.Slow] = limit_slow;
            if (limit_stapm != 0)
                m_Limits[PowerType.Stapm] = limit_stapm;

            // read value(s)
            var value_fast = (int)RyzenAdj.get_fast_value(ry);
            var value_slow = (int)RyzenAdj.get_slow_value(ry);
            var value_stapm = (int)RyzenAdj.get_stapm_value(ry);

            while (value_fast == 0)
                value_fast = (int)RyzenAdj.get_fast_value(ry);
            while (value_slow == 0)
                value_slow = (int)RyzenAdj.get_slow_value(ry);
            while (value_stapm == 0)
                value_stapm = (int)RyzenAdj.get_stapm_value(ry);

            m_Values[PowerType.Fast] = value_fast;
            m_Values[PowerType.Slow] = value_slow;
            m_Values[PowerType.Stapm] = value_stapm;

            // read gfx_clk
            var gfx_clk = (int)RyzenAdj.get_gfx_clk(ry);
            if (gfx_clk != 0)
                m_Misc["gfx_clk"] = gfx_clk;

            base.UpdateTimer_Elapsed(sender, e);

            Monitor.Exit(IsBusy);
        }
    }

    public override void SetTDPLimit(PowerType type, double limit, int result)
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

            base.SetTDPLimit(type, limit, error);

            Monitor.Exit(IsBusy);
        }
    }

    public override void SetGPUClock(double clock, int result)
    {
        if (Monitor.TryEnter(IsBusy))
        {
            switch(family)
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
                                sd.HardMinGfxClock = (uint)MainWindow.CurrentDevice.GfxClock[0]; //hardMin
                                sd.SoftMaxGfxClock = (uint)MainWindow.CurrentDevice.GfxClock[1]; //softMax
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
                        int error1, error2, error3;

                        if (clock == 12750)
                        {
                            error1 = RyzenAdj.set_gfx_clk(ry, (uint)clock);
                            error2 = RyzenAdj.set_min_gfxclk_freq(ry, (uint)MainWindow.CurrentDevice.GfxClock[0]);
                            error3 = RyzenAdj.set_max_gfxclk_freq(ry, (uint)MainWindow.CurrentDevice.GfxClock[1]);
                        }
                        else
                        {
                            error1 = RyzenAdj.set_gfx_clk(ry, (uint)clock);
                            error2 = RyzenAdj.set_min_gfxclk_freq(ry, (uint)clock);
                            error3 = RyzenAdj.set_max_gfxclk_freq(ry, (uint)clock);
                        }

                        base.SetGPUClock(clock, error1 + error2 + error3);
                    }
                    break;
            }

            Monitor.Exit(IsBusy);
        }
    }
}