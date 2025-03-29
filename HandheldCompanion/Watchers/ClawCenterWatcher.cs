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
        public ClawCenterWatcher()
        {
            taskNames = new() { "MSI_Center_M_Server", "MSI_Center_M_Updater" };
            executableNames = new() { "MSI_Center_M_Server", "MSI Center M", "MCMOSDInfo", "MSI Center OSD Info", "Gamebar_Widget" };
            serviceNames = new() { "MSI Foundation Service" };

            watchdogTimer = new Timer(4000);
            watchdogTimer.Elapsed += WatchdogTimer_Elapsed;
        }

        private void WatchdogTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            bool status = HasProcesses() || HasEnabledTasks() || HasRunningServices();
            if (status != prevStatus)
            {
                prevStatus = status;
                UpdateStatus(status);
            }
        }
    }
}
