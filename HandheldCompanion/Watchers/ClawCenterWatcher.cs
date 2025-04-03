using iNKORE.UI.WPF.Modern.Controls;
using System.Timers;

namespace HandheldCompanion.Watchers
{
    public class ClawCenterWatcher : ISpaceWatcher
    {
        public ClawCenterWatcher()
        {
            taskNames = new() { "MSI_Center_M_Server", "MSI_Center_M_Updater" };
            executableNames = new() { "MSI_Center_M_Server", "MSI Center M", "MCMOSDInfo", "MSI Center OSD Info" };
            serviceNames = new() { "MSI Foundation Service" };

            // set notification
            notification = new(
                Properties.Resources.Hint_MSIClawCenterCheck,
                Properties.Resources.Hint_MSIClawCenterCheckDesc,
                string.Empty,
                InfoBarSeverity.Warning);

            // prepare timer
            watchdogTimer = new Timer(4000);
            watchdogTimer.Elapsed += WatchdogTimer_Elapsed;
        }

        public override void Start()
        {
            watchdogTimer.Start();
            base.Start();
        }

        public override void Stop()
        {
            watchdogTimer.Stop();
            base.Stop();
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
