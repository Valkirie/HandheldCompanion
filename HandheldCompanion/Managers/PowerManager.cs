using ControllerCommon.Managers;
using ControllerCommon.Processor;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace HandheldCompanion.Managers
{
    public class PowerManager
    {
        private Processor processor;

        public event LimitChangedHandler LimitChanged;
        public delegate void LimitChangedHandler(string type, int limit);

        public event ValueChangedHandler ValueChanged;
        public delegate void ValueChangedHandler(string type, float value);

        public event StatusChangedHandler StatusChanged;
        public delegate void StatusChangedHandler(bool success);

        // user requested limits
        private double RequestedStapm = 0;

        public PowerManager()
        {
            // initialize processor
            processor = Processor.GetCurrent();
            processor.ValueChanged += Processor_ValueChanged;
            processor.StatusChanged += Processor_StatusChanged;
            processor.LimitChanged += Processor_LimitChanged;
        }

        public void RequestTDP(double value)
        {
            LogManager.LogDebug("User requested stapm: {0}", value);
            RequestedStapm = value;

            processor.SetLimit("stapm", value);
            processor.SetLimit("slow", value + 2);
            processor.SetLimit("fast", value + 5);
        }

        #region events
        private void Processor_StatusChanged(bool success)
        {
            StatusChanged?.Invoke(success);
        }

        private void Processor_ValueChanged(string type, float value)
        {
            ValueChanged?.Invoke(type, value);
        }

        private void Processor_LimitChanged(string type, int limit)
        {
            switch(type)
            {
                default:
                case "slow":
                case "fast":
                    break;
                case "stapm":
                    if (limit != RequestedStapm)
                    {
                        if (RequestedStapm != 0)
                            RequestTDP(RequestedStapm);
                    }
                    break;
            }

            LimitChanged?.Invoke(type, limit);
        }
        #endregion

        internal void Start()
        {
            processor.Initialize();
        }

        internal void Stop()
        {
            processor.Stop();
        }
    }
}
