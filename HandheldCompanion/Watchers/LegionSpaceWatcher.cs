using iNKORE.UI.WPF.Modern.Controls;
using System.Timers;

namespace HandheldCompanion.Watchers
{
    public class LegionSpaceWatcher : ISpaceWatcher
    {
        public LegionSpaceWatcher()
        {
            executableNames = new() { "LegionGoQuickSettings", "LegionSpace", "LSDaemon" };
            serviceNames = new() { "DAService" };

            // set notification
            notification = new(
                Properties.Resources.Hint_LegionGoServices,
                Properties.Resources.Hint_LegionGoServicesDesc,
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
