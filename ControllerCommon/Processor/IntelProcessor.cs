using ControllerCommon.Processor.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace ControllerCommon.Processor
{
    public class IntelProcessor : Processor
    {
        public KX platform = new KX();

        public string family;

        public IntelProcessor() : base()
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

                foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
                {
                    // write default limits
                    m_Limits[type] = 0;
                    m_PrevLimits[type] = 0;

                    /*
                    // write default values : not supported
                    m_Values[type] = -1;
                    m_PrevValues[type] = -1;
                    */
                }
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
                // read limit(s)
                int limit_short = (int)platform.get_short_limit(false);
                int limit_long = (int)platform.get_long_limit(false);

                if (limit_short != -1)
                    base.m_Limits[PowerType.Fast] = limit_short;
                if (limit_long != -1)
                    base.m_Limits[PowerType.Slow] = limit_long;

                // read msr limit(s)
                int msr_short = (int)platform.get_short_limit(true);
                int msr_long = (int)platform.get_long_limit(true);

                if (msr_short != -1)
                    base.m_Limits[PowerType.MsrFast] = msr_short;
                if (msr_long != -1)
                    base.m_Limits[PowerType.MsrSlow] = msr_long;

                // read value(s)
                int value_short = 0;
                int value_long = 0;

                while (value_short == 0)
                    value_short = (int)platform.get_short_value();

                while (value_long == 0)
                    value_long = (int)platform.get_long_value();

                base.m_Values[PowerType.Fast] = value_short;
                base.m_Values[PowerType.Slow] = value_long;

                // read gfx_clk
                int gfx_clk = (int)platform.get_gfx_clk();

                if (gfx_clk != -1)
                    base.m_Misc["gfx_clk"] = gfx_clk;

                base.UpdateTimer_Elapsed(sender, e);

                Monitor.Exit(base.IsBusy);
            }
        }

        public override void SetTDPLimit(PowerType type, double limit, int result)
        {
            if (Monitor.TryEnter(base.IsBusy))
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

                base.SetTDPLimit(type, limit, error);

                Monitor.Exit(base.IsBusy);
            }
        }

        public void SetMSRLimit(double PL1, double PL2)
        {
            platform.set_msr_limits((int)PL1, (int)PL2);
        }

        public override void SetGPUClock(double clock, int result)
        {
            if (Monitor.TryEnter(base.IsBusy))
            {
                var error = platform.set_gfx_clk((int)clock);

                base.SetGPUClock(clock, error);

                Monitor.Exit(base.IsBusy);
            }
        }
    }
}
