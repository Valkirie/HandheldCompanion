using ControllerCommon.Managers;
using System.Collections.Generic;
using System.Management;
using System.Timers;
using Timer = System.Timers.Timer;

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

        public bool CanChangeTDP, CanChangeGPU;
        protected object IsBusy = new();
        public bool IsInitialized;

        protected readonly Timer updateTimer = new Timer() { Interval = 3000, AutoReset = true };

        protected Dictionary<PowerType, int> m_Limits = new();
        protected Dictionary<PowerType, int> m_PrevLimits = new();

        protected Dictionary<PowerType, float> m_Values = new();
        protected Dictionary<PowerType, float> m_PrevValues = new();

        protected Dictionary<string, float> m_Misc = new();
        protected Dictionary<string, float> m_PrevMisc = new();

        #region events
        public event LimitChangedHandler LimitChanged;
        public delegate void LimitChangedHandler(PowerType type, int limit);

        public event ValueChangedHandler ValueChanged;
        public delegate void ValueChangedHandler(PowerType type, float value);

        public event GfxChangedHandler MiscChanged;
        public delegate void GfxChangedHandler(string misc, float value);

        public event StatusChangedHandler StatusChanged;
        public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

        public event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        public static Processor GetCurrent()
        {
            if (processor is not null)
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

            return string.Empty;
        }

        public Processor()
        {
            Name = GetProcessorDetails("Name");
            ProcessorID = GetProcessorDetails("processorID");

            // write default miscs
            m_Misc["gfx_clk"] = m_PrevMisc["gfx_clk"] = 0;
        }

        public virtual void Initialize()
        {
            StatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);
            Initialized?.Invoke();

            // deprecated, we're using HWiNFO to provide values and limits
            /*
            if (CanChangeTDP)
                updateTimer.Start();
            */
        }

        public virtual void Stop()
        {
            // deprecated, we're using HWiNFO to provide values and limits
            /*
            if (CanChangeTDP)
                updateTimer.Stop();
            */
        }

        public virtual void SetTDPLimit(PowerType type, double limit, int result = 0)
        {
            LogManager.LogDebug("User requested {0} TDP limit: {1}, error code: {2}", type, limit, result);
        }

        public virtual void SetGPUClock(double clock, int result = 0)
        {
            /*
             * #define ADJ_ERR_FAM_UNSUPPORTED      -1
             * #define ADJ_ERR_SMU_TIMEOUT          -2
             * #define ADJ_ERR_SMU_UNSUPPORTED      -3
             * #define ADJ_ERR_SMU_REJECTED         -4
             * #define ADJ_ERR_MEMORY_ACCESS        -5
             */

            LogManager.LogInformation("User requested GPU clock: {0}, error code: {1}", clock, result);
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

            // search for misc changes
            foreach (KeyValuePair<string, float> pair in m_Misc)
            {
                if (m_PrevMisc[pair.Key] == pair.Value)
                    continue;

                MiscChanged?.Invoke(pair.Key, pair.Value);

                m_PrevMisc[pair.Key] = pair.Value;
            }
        }
    }
}
