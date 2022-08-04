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
        Long = 0,
        Stapm = 0,

        // short
        Fast = 1,
        Short = 1
    }

    public class Processor
    {
        private static ManagementClass managClass = new ManagementClass("win32_processor");

        private static Processor processor;
        private static string Manufacturer;

        protected string Name, ProcessorID;

        protected bool CanChangeTDP, CanChangeGPU;

        protected Timer updateTimer = new Timer() { Interval = 2000, AutoReset = true };

        protected Dictionary<PowerType, int> m_Limits = new();
        protected Dictionary<PowerType, int> m_PrevLimits = new();

        protected Dictionary<PowerType, float> m_Values = new();
        protected Dictionary<PowerType, float> m_PrevValues = new();

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

        public virtual void SetTDPLimit(string type, double limit)
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

            // search for value changes
            foreach (KeyValuePair<PowerType, float> pair in m_Values)
            {
                if (m_PrevValues[pair.Key] == pair.Value)
                    continue;

                ValueChanged?.Invoke(pair.Key, pair.Value);

                m_PrevValues[pair.Key] = pair.Value;
            }
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

                // write default limit(s)
                m_Limits[PowerType.Short] = m_Limits[PowerType.Long] = m_Limits[PowerType.Stapm] = 0;
                m_PrevLimits[PowerType.Short] = m_PrevLimits[PowerType.Long] = m_PrevLimits[PowerType.Stapm] = 0;

                // write default value(s)
                m_Values[PowerType.Short] = m_Values[PowerType.Long] = m_Values[PowerType.Stapm] = 0;
                m_PrevValues[PowerType.Short] = m_PrevValues[PowerType.Long] = m_PrevValues[PowerType.Stapm] = 0;
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
            // read limit(s)
            base.m_Limits[PowerType.Short] = (int)platform.get_short_limit();
            base.m_Limits[PowerType.Long] = (int)platform.get_long_limit();
            base.m_Limits[PowerType.Stapm] = base.m_Limits[PowerType.Long];

            // read value(s)
            base.m_Values[PowerType.Short] = (int)platform.get_short_value();
            base.m_Values[PowerType.Long] = (int)platform.get_long_value();
            base.m_Values[PowerType.Stapm] = base.m_Values[PowerType.Long];

            base.UpdateTimer_Elapsed(sender, e);
        }

        public override void SetTDPLimit(string type, double limit)
        {
            switch (type)
            {
                case "fast":
                    platform.set_short_limit((int)limit);
                    break;
                case "slow":
                    platform.set_long_limit((int)limit);
                    break;
                case "stapm":
                    platform.set_long_limit((int)limit);
                    break;
                case "all":
                    platform.set_long_limit((int)limit);
                    platform.set_short_limit((int)limit);
                    break;
            }
            base.SetTDPLimit(type, limit);
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

                CanChangeTDP = true;
            }

            // write default limit(s)
            m_Limits[PowerType.Fast] = m_Limits[PowerType.Slow] = m_Limits[PowerType.Stapm] = 0;
            m_PrevLimits[PowerType.Fast] = m_PrevLimits[PowerType.Slow] = m_PrevLimits[PowerType.Stapm] = 0;

            // write default value(s)
            m_Values[PowerType.Fast] = m_Values[PowerType.Slow] = m_Values[PowerType.Stapm] = 0;
            m_PrevValues[PowerType.Fast] = m_PrevValues[PowerType.Slow] = m_PrevValues[PowerType.Stapm] = 0;
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
            RyzenAdj.get_table_values(ry);
            RyzenAdj.refresh_table(ry);

            // read limit(s)
            m_Limits[PowerType.Fast] = (int)RyzenAdj.get_fast_limit(ry);
            m_Limits[PowerType.Slow] = (int)RyzenAdj.get_slow_limit(ry);
            m_Limits[PowerType.Stapm] = (int)RyzenAdj.get_stapm_limit(ry);

            // read value(s)
            m_Values[PowerType.Fast] = RyzenAdj.get_fast_value(ry);
            m_Values[PowerType.Slow] = RyzenAdj.get_slow_value(ry);
            m_Values[PowerType.Stapm] = RyzenAdj.get_stapm_value(ry);

            base.UpdateTimer_Elapsed(sender, e);
        }

        public override void SetTDPLimit(string type, double limit)
        {
            if (ry == IntPtr.Zero)
                return;

            // 15W : 15000
            limit *= 1000;

            switch (type)
            {
                case "fast":
                    RyzenAdj.set_fast_limit(ry, (uint)limit);
                    break;
                case "slow":
                    RyzenAdj.set_slow_limit(ry, (uint)limit);
                    break;
                case "stapm":
                    RyzenAdj.set_stapm_limit(ry, (uint)limit);
                    break;
                case "all":
                    RyzenAdj.set_stapm_limit(ry, (uint)limit);
                    RyzenAdj.set_slow_limit(ry, (uint)limit);
                    RyzenAdj.set_fast_limit(ry, (uint)limit);
                    break;
            }
            base.SetTDPLimit(type, limit);
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
