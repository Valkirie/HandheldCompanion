using iNKORE.UI.WPF.Modern.Controls;
using System.Timers;

namespace HandheldCompanion.Watchers
{
    public class ZotacLauncherWatcher : ISpaceWatcher
    {
        public ZotacLauncherWatcher()
        {
            executableNames = new() { "ZotacHandheldQuickSetting" };
            serviceNames = new() { "ZotacHandheldDatabaseService", "ZotacHandheldService" };

            // set notification
            notification = new(
                Properties.Resources.Hint_GamingZoneServices,
                Properties.Resources.Hint_GamingZoneServicesDesc,
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
