using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels.Misc
{
    /// <summary>
    /// Known monitors section of the device page: lists the monitors HC remembers and lets the user forget disconnected ones.
    /// </summary>
    public class KnownMonitorsViewModel : BaseViewModel
    {
        // connected monitors, then a Not connected header and the disconnected monitors
        public ObservableCollection<object> Rows { get; } = [];

        private string _summary = string.Empty;
        public string Summary
        {
            get => _summary;
            private set => SetProperty(ref _summary, value);
        }

        private int _refreshQueued;

        public KnownMonitorsViewModel()
        {
            ManagerFactory.multimediaManager.KnownMonitorsChanged += QueueRefresh;
            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
            ManagerFactory.multimediaManager.PrimaryScreenChanged += MultimediaManager_ScreenChanged;
            ManagerFactory.multimediaManager.ScreenConnected += MultimediaManager_ScreenChanged;
            ManagerFactory.multimediaManager.ScreenDisconnected += MultimediaManager_ScreenChanged;
            ManagerFactory.multimediaManager.Initialized += QueueRefresh;
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;
            ManagerFactory.profileManager.Initialized += QueueRefresh;

            QueueRefresh();
        }

        private void MultimediaManager_DisplaySettingsChanged(DesktopScreen screen, ScreenResolution resolution) => QueueRefresh();
        private void MultimediaManager_ScreenChanged(DesktopScreen screen) => QueueRefresh();
        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent) => QueueRefresh();
        private void ProfileManager_Deleted(Profile profile) => QueueRefresh();

        // events arrive in bursts and from any thread, rebuild once on the UI thread
        private void QueueRefresh()
        {
            if (Interlocked.Exchange(ref _refreshQueued, 1) == 1)
                return;

            UIHelper.TryBeginInvoke(() =>
            {
                Interlocked.Exchange(ref _refreshQueued, 0);
                Refresh();
            });
        }

        private void Refresh()
        {
            List<KnownMonitor> known = ManagerFactory.multimediaManager.GetKnownMonitors();
            Dictionary<Guid, DesktopScreen> connected = ManagerFactory.multimediaManager.GetConnectedMonitors()
                .GroupBy(s => s.KnownMonitorId)
                .ToDictionary(g => g.Key, g => g.First());
            List<Profile> profiles = ManagerFactory.profileManager.GetProfiles(true);

            int connectedCount = known.Count(m => connected.ContainsKey(m.Id));
            int disconnectedCount = known.Count - connectedCount;
            Summary = disconnectedCount > 0
                ? string.Format(Properties.Resources.DevicePage_KnownMonitorsConnectedAndNotConnected, connectedCount, disconnectedCount)
                : string.Format(Properties.Resources.DevicePage_KnownMonitorsConnected, connectedCount);

            List<KnownMonitorItemViewModel> items = known
                .Select(m =>
                {
                    connected.TryGetValue(m.Id, out DesktopScreen? screen);
                    return new KnownMonitorItemViewModel(this, m, screen is not null, screen?.IsPrimary == true, CountProfileOverrides(profiles, m.Id),
                        screen is null ? 0 : MonitorIdentity.GetDisplayNumber(screen));
                })
                .OrderByDescending(i => i.IsConnected)
                .ThenByDescending(i => i.IsMain)
                .ThenByDescending(i => i.LastSeen)
                .ToList();

            Rows.Clear();
            foreach (KnownMonitorItemViewModel item in items.Where(i => i.IsConnected))
                Rows.Add(item);

            if (disconnectedCount > 0)
            {
                Rows.Add(new KnownMonitorsSectionHeader(Properties.Resources.DevicePage_KnownMonitorsNotConnected));
                foreach (KnownMonitorItemViewModel item in items.Where(i => !i.IsConnected))
                    Rows.Add(item);
            }
        }

        // profiles (sub profiles and default included) with a resolution or refresh rate override for the monitor
        private static int CountProfileOverrides(List<Profile> profiles, Guid monitorId)
        {
            int count = 0;
            foreach (Profile profile in profiles)
            {
                lock (profile.SyncRoot)
                {
                    if (profile.PerformanceOverrides.Values.Any(o => o?.DisplayModes is not null
                        && o.DisplayModes.TryGetValue(monitorId, out DisplayModeOverride? mode)
                        && mode is not null && (mode.HasResolution || mode.HasRefreshRate)))
                        count++;
                }
            }

            return count;
        }

        internal async Task ForgetAsync(KnownMonitorItemViewModel item)
        {
            if (item.IsConnected)
                return;

            ContentDialogResult result = await new Dialog(MainWindow.GetCurrent())
            {
                Title = Properties.Resources.DevicePage_KnownMonitorForgetTitle,
                Content = string.Format(Properties.Resources.DevicePage_KnownMonitorForgetContent, item.Name),
                CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                PrimaryButtonText = Properties.Resources.DevicePage_KnownMonitorForget
            }.ShowAsync();

            if (result != ContentDialogResult.Primary)
                return;

            Guid monitorId = item.Id;
            await Task.Run(() =>
            {
                try
                {
                    if (!ManagerFactory.multimediaManager.ForgetKnownMonitor(monitorId))
                        return;

                    int profiles = ManagerFactory.profileManager.RemoveMonitorReferences(monitorId);
                    LogManager.LogInformation("Forgot monitor {0}, {1} profile(s) updated", monitorId, profiles);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to forget monitor {0}: {1}", monitorId, ex.Message);
                }
            });

            QueueRefresh();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ManagerFactory.multimediaManager.KnownMonitorsChanged -= QueueRefresh;
                ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
                ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_ScreenChanged;
                ManagerFactory.multimediaManager.ScreenConnected -= MultimediaManager_ScreenChanged;
                ManagerFactory.multimediaManager.ScreenDisconnected -= MultimediaManager_ScreenChanged;
                ManagerFactory.multimediaManager.Initialized -= QueueRefresh;
                ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
                ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;
                ManagerFactory.profileManager.Initialized -= QueueRefresh;
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A subheader row of the known monitors list.
    /// </summary>
    public sealed record KnownMonitorsSectionHeader(string Title);

    /// <summary>
    /// Picks the template of a known monitors row. The SettingsExpander lists ItemsSource through an ItemsRepeater
    /// that only uses its ItemTemplate, so implicit DataType templates never apply there.
    /// </summary>
    public class KnownMonitorRowTemplateSelector : System.Windows.Controls.DataTemplateSelector
    {
        public System.Windows.DataTemplate? SectionHeaderTemplate { get; set; }
        public System.Windows.DataTemplate? MonitorTemplate { get; set; }

        public override System.Windows.DataTemplate? SelectTemplate(object item, System.Windows.DependencyObject container)
        {
            return item is KnownMonitorsSectionHeader ? SectionHeaderTemplate : MonitorTemplate;
        }
    }

    /// <summary>
    /// One known monitor row.
    /// </summary>
    public class KnownMonitorItemViewModel
    {
        private readonly KnownMonitorsViewModel _owner;

        public Guid Id { get; }
        public string Name { get; }
        public string Model { get; }
        public DateTime LastSeen { get; }
        public bool IsConnected { get; }
        public bool IsMain { get; }
        public bool IsInternal { get; }
        public int ProfileOverrides { get; }

        public bool CanForget => !IsConnected;
        public double RowOpacity => IsConnected ? 1.0 : 0.6;

        public string LastSeenText => IsConnected
            ? Properties.Resources.DevicePage_KnownMonitorConnectedNow
            : LastSeen == DateTime.MinValue
                ? Properties.Resources.DevicePage_KnownMonitorLastSeenUnknown
                : string.Format(Properties.Resources.DevicePage_KnownMonitorLastSeen, LastSeen.Kind == DateTimeKind.Utc ? LastSeen.ToLocalTime() : LastSeen);

        public string ProfileOverridesText => ProfileOverrides switch
        {
            0 => Properties.Resources.DevicePage_KnownMonitorNoOverrides,
            1 => Properties.Resources.DevicePage_KnownMonitorOneOverride,
            _ => string.Format(Properties.Resources.DevicePage_KnownMonitorOverrides, ProfileOverrides),
        };

        public string Description => $"{Model} · {LastSeenText} · {ProfileOverridesText}";

        public ICommand ForgetCommand { get; }

        public KnownMonitorItemViewModel(KnownMonitorsViewModel owner, KnownMonitor monitor, bool isConnected, bool isMain, int profileOverrides, int displayNumber)
        {
            _owner = owner;

            Id = monitor.Id;
            // internal is a badge, not part of the name
            Name = string.IsNullOrWhiteSpace(monitor.FriendlyName) ? Properties.Resources.DevicePage_KnownMonitorUnknownDisplay : monitor.FriendlyName;
            IsInternal = monitor.IsInternal;
            // connected monitors carry the number Windows shows in Settings > Display
            if (displayNumber > 0)
                Name = MonitorIdentity.FormatDisplayLabel(displayNumber, Name);
            Model = string.IsNullOrEmpty(monitor.Manufacturer)
                ? Properties.Resources.DevicePage_KnownMonitorUnknownModel
                : $"{monitor.Manufacturer} {monitor.ProductCode:X4}";
            LastSeen = monitor.LastSeen;
            IsConnected = isConnected;
            IsMain = isMain;
            ProfileOverrides = profileOverrides;

            ForgetCommand = new AsyncDelegateCommand(() => _owner.ForgetAsync(this));
        }
    }
}
