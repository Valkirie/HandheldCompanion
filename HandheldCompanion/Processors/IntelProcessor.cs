using HandheldCompanion.Processors.Intel;
using System;
using System.Threading;
using System.Timers;

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

            foreach (var type in (PowerType[])Enum.GetValues(typeof(PowerType)))
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
        if (Monitor.TryEnter(IsBusy))
        {
            // read limit(s)
            var limit_short = platform.get_short_limit(false);
            var limit_long = platform.get_long_limit(false);

            if (limit_short != -1)
                m_Limits[PowerType.Fast] = limit_short;
            if (limit_long != -1)
                m_Limits[PowerType.Slow] = limit_long;

            // read msr limit(s)
            var msr_short = platform.get_short_limit(true);
            var msr_long = platform.get_long_limit(true);

            if (msr_short != -1)
                m_Limits[PowerType.MsrFast] = msr_short;
            if (msr_long != -1)
                m_Limits[PowerType.MsrSlow] = msr_long;

            // read value(s)
            var value_short = 0;
            var value_long = 0;

            while (value_short == 0)
                value_short = platform.get_short_value();

            while (value_long == 0)
                value_long = platform.get_long_value();

            m_Values[PowerType.Fast] = value_short;
            m_Values[PowerType.Slow] = value_long;

            // read gfx_clk
            var gfx_clk = platform.get_gfx_clk();

            if (gfx_clk != -1)
                m_Misc["gfx_clk"] = gfx_clk;

            base.UpdateTimer_Elapsed(sender, e);

            Monitor.Exit(IsBusy);
        }
    }

    public override void SetTDPLimit(PowerType type, double limit, bool immediate, int result)
    {
        if (Monitor.TryEnter(IsBusy))
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

            Monitor.Exit(IsBusy);
        }
    }

    public void SetMSRLimit(double PL1, double PL2)
    {
        platform.set_msr_limits((int)PL1, (int)PL2);
    }

    public override void SetGPUClock(double clock, int result)
    {
        if (Monitor.TryEnter(IsBusy))
        {
            var error = platform.set_gfx_clk((int)clock);

            base.SetGPUClock(clock, error);

            Monitor.Exit(IsBusy);
        }
    }
}