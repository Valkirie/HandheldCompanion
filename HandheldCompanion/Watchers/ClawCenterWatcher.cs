using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace HandheldCompanion.Watchers
{
    public class ClawCenterWatcher : ISpaceWatcher
    {
        protected new List<string> taskNames = new() { "MSI_Center_M_Server" };
        protected new List<string> executableNames = new() { "MSI_Center_M_Server", "MSI Center M" };

        public ClawCenterWatcher()
        {
            watchdogTimer = new Timer(4000);
            watchdogTimer.Elapsed += WatchdogTimer_Elapsed;
        }

        public override void Start()
        {
            base.Start();
        }

        public override void Stop()
        {
            base.Stop();
        }

        private void WatchdogTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            bool status = HasProcesses() || HasEnabledTasks();
            if (status != prevStatus)
            {
                prevStatus = status;
                UpdateStatus(status);
            }
        }
    }
}
