using ControllerCommon.Processor.AMD;
using System;
using System.Threading;
using System.Timers;

namespace ControllerCommon.Processor
{
    public class AMDProcessor : Processor
    {
        public IntPtr ry;
        public RyzenFamily family;

        public AMDProcessor() : base()
        {
            ry = RyzenAdj.init_ryzenadj();

            if (ry == IntPtr.Zero)
                IsInitialized = false;
            else
            {
                family = RyzenAdj.get_cpu_family(ry);
                IsInitialized = true;

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
            }

            foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
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
            if (Monitor.TryEnter(base.IsBusy))
            {
                RyzenAdj.get_table_values(ry);
                RyzenAdj.refresh_table(ry);

                // read limit(s)
                int limit_fast = (int)RyzenAdj.get_fast_limit(ry);
                int limit_slow = (int)RyzenAdj.get_slow_limit(ry);
                int limit_stapm = (int)RyzenAdj.get_stapm_limit(ry);

                if (limit_fast != 0)
                    base.m_Limits[PowerType.Fast] = limit_fast;
                if (limit_slow != 0)
                    base.m_Limits[PowerType.Slow] = limit_slow;
                if (limit_stapm != 0)
                    base.m_Limits[PowerType.Stapm] = limit_stapm;

                // read value(s)
                int value_fast = (int)RyzenAdj.get_fast_value(ry);
                int value_slow = (int)RyzenAdj.get_slow_value(ry);
                int value_stapm = (int)RyzenAdj.get_stapm_value(ry);

                while (value_fast == 0)
                    value_fast = (int)RyzenAdj.get_fast_value(ry);
                while (value_slow == 0)
                    value_slow = (int)RyzenAdj.get_slow_value(ry);
                while (value_stapm == 0)
                    value_stapm = (int)RyzenAdj.get_stapm_value(ry);

                base.m_Values[PowerType.Fast] = value_fast;
                base.m_Values[PowerType.Slow] = value_slow;
                base.m_Values[PowerType.Stapm] = value_stapm;

                // read gfx_clk
                int gfx_clk = (int)RyzenAdj.get_gfx_clk(ry);
                if (gfx_clk != 0)
                    base.m_Misc["gfx_clk"] = gfx_clk;

                base.UpdateTimer_Elapsed(sender, e);

                Monitor.Exit(base.IsBusy);
            }
        }

        public override void SetTDPLimit(PowerType type, double limit, int result)
        {
            if (ry == IntPtr.Zero)
                return;

            if (Monitor.TryEnter(base.IsBusy))
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

                Monitor.Exit(base.IsBusy);
            }
        }

        public override void SetGPUClock(double clock, int result)
        {
            if (Monitor.TryEnter(base.IsBusy))
            {
                // reset default var
                if (clock == 12750)
                {
                    Monitor.Exit(base.IsBusy);
                    return;
                }

                var error1 = RyzenAdj.set_gfx_clk(ry, (uint)clock);
                var error2 = RyzenAdj.set_min_gfxclk_freq(ry, (uint)clock);
                var error3 = RyzenAdj.set_max_gfxclk_freq(ry, (uint)clock);

                base.SetGPUClock(clock, error1 + error2 + error3);

                Monitor.Exit(base.IsBusy);
            }
        }
    }
}
