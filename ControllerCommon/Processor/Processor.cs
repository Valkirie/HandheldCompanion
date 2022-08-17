using ControllerCommon.Managers;
using ControllerCommon.Processor.AMD;
using ControllerCommon.Processor.Intel;
using System;
using System.Collections.Generic;
using System.Management;
using System.Timers;

namespace ControllerCommon.Processor
{
    public enum PowerType
    {
        // long
        Slow = 0,
        Stapm = 1,
        Fast = 2,
        MsrSlow = 3,
        MsrFast = 4,
    }

    public class Processor
    {
        private static ManagementClass managClass = new ManagementClass("win32_processor");

        private static Processor processor;
        private static string Manufacturer;

        protected string Name, ProcessorID;

        protected bool CanChangeTDP, CanChangeGPU;
        protected object IsBusy = new();

        protected Timer updateTimer = new Timer() { Interval = 3000, AutoReset = true };

        protected Dictionary<PowerType, int> m_Limits = new();
        protected Dictionary<PowerType, int> m_PrevLimits = new();

        /* 
         * protected Dictionary<PowerType, float> m_Values = new();
         * protected Dictionary<PowerType, float> m_PrevValues = new();
         */

        #region events
        public event LimitChangedHandler LimitChanged;
        public delegate void LimitChangedHandler(PowerType type, int limit);

        public event ValueChangedHandler ValueChanged;
        public delegate void ValueChangedHandler(PowerType type, float value);

        public event StatusChangedHandler StatusChanged;
        public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);
        #endregion

        public static Processor GetCurrent()
        {
            if (processor != null)
                return processor;

            Manufacturer = GetProcessorDetails("Manufacturer");

            switch (Manufacturer)
            {
                case "GenuineIntel":
                    processor = new IntelProcessor();
                    break;
                case "AuthenticAMD":
                    processor = new AMDProcessor();
                    break;
            }

            return processor;
        }

        private static string GetProcessorDetails(string value)
        {
            var managCollec = managClass.GetInstances();
            foreach (ManagementObject managObj in managCollec)
                return managObj.Properties[value].Value.ToString();

            return "";
        }

        public Processor()
        {
            Name = GetProcessorDetails("Name");
            ProcessorID = GetProcessorDetails("processorID");
        }

        public virtual void Initialize()
        {
            StatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);

            if (CanChangeTDP)
                updateTimer.Start();
        }

        public virtual void Stop()
        {
            if (CanChangeTDP)
                updateTimer.Stop();
        }

        public virtual void SetTDPLimit(PowerType type, double limit)
        {
            LogManager.LogInformation("User requested {0} TDP limit: {1}", type, limit);
        }

        public virtual void SetGPUClock(double clock)
        {
            LogManager.LogInformation("User requested GPU clock: {0}", clock);
        }

        protected virtual void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // search for limit changes
            foreach (KeyValuePair<PowerType, int> pair in m_Limits)
            {
                if (m_PrevLimits[pair.Key] == pair.Value)
                    continue;

                LimitChanged?.Invoke(pair.Key, pair.Value);

                m_PrevLimits[pair.Key] = pair.Value;
            }

            /*
            // search for value changes
            foreach (KeyValuePair<PowerType, float> pair in m_Values)
            {
                if (m_PrevValues[pair.Key] == pair.Value)
                    continue;

                ValueChanged?.Invoke(pair.Key, pair.Value);

                m_PrevValues[pair.Key] = pair.Value;
            }
            */
        }
    }

    public class IntelProcessor : Processor
    {
        public KX platform = new KX();

        public string family;

        public IntelProcessor() : base()
        {
            if (platform.init())
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
            lock (base.IsBusy)
            {
                // read limit(s)
                int limit_short = -1;
                int limit_long = -1;

                while (limit_short == -1)
                    limit_short = (int)platform.get_short_limit(false);
                while (limit_long == -1)
                    limit_long = (int)platform.get_long_limit(false);

                base.m_Limits[PowerType.Fast] = limit_short;
                base.m_Limits[PowerType.Slow] = limit_long;

                // read msr limit(s)
                int msr_short = -1;
                int msr_long = -1;

                while (msr_short == -1)
                    msr_short = (int)platform.get_short_limit(true);
                while (msr_long == -1)
                    msr_long = (int)platform.get_long_limit(true);

                base.m_Limits[PowerType.MsrFast] = msr_short;
                base.m_Limits[PowerType.MsrSlow] = msr_long;

                /*
                // read value(s)
                int value_short = 0;
                int value_long = 0;

                while (value_short == 0)
                    value_short = (int)platform.get_short_value();

                while (value_long == 0)
                    value_long = (int)platform.get_long_value();

                base.m_Values[PowerType.Fast] = value_short;
                base.m_Values[PowerType.Slow] = value_long;
                */
            }

            base.UpdateTimer_Elapsed(sender, e);
        }

        public override void SetTDPLimit(PowerType type, double limit)
        {
            lock (base.IsBusy)
            {
                switch (type)
                {
                    case PowerType.Slow:
                        platform.set_long_limit((int)limit);
                        break;
                    case PowerType.Fast:
                        platform.set_short_limit((int)limit);
                        break;
                }

                base.SetTDPLimit(type, limit);
            }
        }

        public void SetMSRLimit(double PL1, double PL2)
        {
            platform.set_msr_limits((int)PL1, (int)PL2);
        }

        public override void SetGPUClock(double clock)
        {
            platform.set_gfx_clk((int)clock);
            base.SetGPUClock(clock);
        }
    }

    public class AMDProcessor : Processor
    {
        public IntPtr ry;
        public RyzenFamily family;

        public AMDProcessor() : base()
        {
            ry = RyzenAdj.init_ryzenadj();

            if (ry != IntPtr.Zero)
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
                    case RyzenFamily.FAM_VANGOGH:
                    case RyzenFamily.FAM_REMBRANDT:
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
            lock (base.IsBusy)
            {
                RyzenAdj.get_table_values(ry);
                RyzenAdj.refresh_table(ry);

                // read limit(s)
                int limit_fast = (int)RyzenAdj.get_fast_limit(ry);
                int limit_slow = (int)RyzenAdj.get_slow_limit(ry);
                int limit_stapm = (int)RyzenAdj.get_stapm_limit(ry);

                while (limit_fast == 0)
                    limit_fast = (int)RyzenAdj.get_fast_limit(ry);
                while (limit_slow == 0)
                    limit_slow = (int)RyzenAdj.get_slow_limit(ry);
                while (limit_stapm == 0)
                    limit_stapm = (int)RyzenAdj.get_stapm_limit(ry);

                base.m_Limits[PowerType.Fast] = limit_fast;
                base.m_Limits[PowerType.Slow] = limit_slow;
                base.m_Limits[PowerType.Stapm] = limit_stapm;

                /*
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
                */
            }

            base.UpdateTimer_Elapsed(sender, e);
        }

        public override void SetTDPLimit(PowerType type, double limit)
        {
            if (ry == IntPtr.Zero)
                return;

            lock (base.IsBusy)
            {
                // 15W : 15000
                limit *= 1000;

                switch (type)
                {
                    case PowerType.Fast:
                        RyzenAdj.set_fast_limit(ry, (uint)limit);
                        break;
                    case PowerType.Slow:
                        RyzenAdj.set_slow_limit(ry, (uint)limit);
                        break;
                    case PowerType.Stapm:
                        RyzenAdj.set_stapm_limit(ry, (uint)limit);
                        break;
                }

                base.SetTDPLimit(type, limit);
            }
        }

        public override void SetGPUClock(double clock)
        {
            // reset default var
            if (clock == 12750)
                return;

            RyzenAdj.set_gfx_clk(ry, (uint)clock);
            base.SetGPUClock(clock);
        }
    }
}
