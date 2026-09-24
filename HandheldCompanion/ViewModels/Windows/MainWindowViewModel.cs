using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        public LaunchProfileDialogViewModel LaunchProfileDialog { get; } = new();

        private bool _isInfoBarOpen;
        private string _infoBarMessage = string.Empty;
        private string _infoBarTitle = string.Empty;
        private InfoBarSeverity _infoBarSeverity;

        private Guid _currentNotification;
        private CancellationTokenSource? _closeCts;
        private readonly ObservableCollection<Profile> profileSearchProfiles = [];

        public ObservableCollection<Profile> ProfileSearchResults { get; } = [];

        private string _profileSearchText = string.Empty;
        public string ProfileSearchText
        {
            get => _profileSearchText;
            set
            {
                if (_profileSearchText.Equals(value))
                    return;

                _profileSearchText = value;
                OnPropertyChanged(nameof(ProfileSearchText));
                UpdateProfileSearchResults();
            }
        }

        public MainWindowViewModel()
        {
            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfiles();
                    break;
            }

            // raise events
            switch (ManagerFactory.notificationManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.notificationManager.Initialized += NotificationManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryNotifications();
                    break;
            }

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

            // Initialize MainWindowApplyNoise from settings
            MainWindowApplyNoise = ManagerFactory.settingsManager.GetBoolean("MainWindowApplyNoise");

            DismissInfoBarCommand = new DelegateCommand(async () =>
            {
                IsInfoBarOpen = false;
            });
        }

        private void ProfileManager_Initialized()
        {
            QueryProfiles();
        }

        private void QueryProfiles()
        {
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;
            ManagerFactory.profileManager.Applied += ProfileManager_Applied;
            RefreshProfileSearchProfiles();
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            RefreshProfileSearchProfiles();
        }

        private void ProfileManager_Deleted(Profile profile)
        {
            RefreshProfileSearchProfiles();
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            RefreshProfileSearchProfiles();
        }

        private void RefreshProfileSearchProfiles()
        {
            UIHelper.TryBeginInvoke(() =>
            {
                profileSearchProfiles.Clear();

                foreach (Profile profile in ManagerFactory.profileManager.GetProfiles())
                {
                    profileSearchProfiles.Add(profile);

                    foreach (Profile subProfile in ManagerFactory.profileManager.GetSubProfilesFromProfile(profile))
                        profileSearchProfiles.Add(subProfile);
                }
            });
        }

        private void UpdateProfileSearchResults()
        {
            string searchText = ProfileSearchText.Trim();
            ProfileSearchResults.Clear();

            if (string.IsNullOrWhiteSpace(searchText))
                return;

            foreach (Profile profile in profileSearchProfiles.Where(profile =>
            profile.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            profile.Executable.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            {
                ProfileSearchResults.Add(profile);
            }
        }

        private void QueryNotifications()
        {
            // manage events
            ManagerFactory.notificationManager.Added += NotificationManager_Added;
            ManagerFactory.notificationManager.Discarded += NotificationManager_Discarded;

            if (ManagerFactory.notificationManager.Notifications.TryGetSnapshot(out Notification[] notifications, 2000))
                foreach (Notification notification in notifications)
                    NotificationManager_Added(notification);
        }

        private void NotificationManager_Initialized()
        {
            QueryNotifications();
        }

        private void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            SettingsManager_SettingValueChanged("PerformanceManagerEnabled", ManagerFactory.settingsManager.GetString("PerformanceManagerEnabled"), false, true);
            SettingsManager_SettingValueChanged("LibraryPageEnabled", ManagerFactory.settingsManager.GetString("LibraryPageEnabled"), false, true);
            SettingsManager_SettingValueChanged("MainWindowApplyNoise", ManagerFactory.settingsManager.GetString("MainWindowApplyNoise"), false, true);
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private bool _isInitializing = true;
        public bool IsInitializing
        {
            get => _isInitializing;
            set => SetProperty(ref _isInitializing, value, null, nameof(IsInitializing));
        }

        private bool _isNavPerformanceEnabled = true;
        public bool IsNavPerformanceEnabled
        {
            get => _isNavPerformanceEnabled;
            set => SetProperty(ref _isNavPerformanceEnabled, value, null, nameof(IsNavPerformanceEnabled));
        }

        private bool _isNavLibraryVisible = true;
        public bool IsNavLibraryVisible
        {
            get => _isNavLibraryVisible;
            set => SetProperty(ref _isNavLibraryVisible, value, null, nameof(IsNavLibraryVisible));
        }

        private bool _mainWindowApplyNoise;
        public bool MainWindowApplyNoise
        {
            get => _mainWindowApplyNoise;
            set => SetProperty(ref _mainWindowApplyNoise, value, null, nameof(MainWindowApplyNoise));
        }

        public bool IsInfoBarOpen
        {
            get => _isInfoBarOpen;
            set => SetProperty(ref _isInfoBarOpen, value, null, nameof(IsInfoBarOpen));
        }

        public string InfoBarMessage
        {
            get => _infoBarMessage;
            set => SetProperty(ref _infoBarMessage, value, null, nameof(InfoBarMessage));
        }

        public string InfoBarTitle
        {
            get => _infoBarTitle;
            set => SetProperty(ref _infoBarTitle, value, null, nameof(InfoBarTitle));
        }

        public InfoBarSeverity InfoBarSeverity
        {
            get => _infoBarSeverity;
            set => SetProperty(ref _infoBarSeverity, value, null, nameof(InfoBarSeverity));
        }

        public ICommand DismissInfoBarCommand { get; }

        private void NotificationManager_Added(Notification notification)
        {
            UIHelper.TryBeginInvoke(() => _ = ShowNotificationAsync(notification));
        }

        private async Task ShowNotificationAsync(Notification notification)
        {
            if (!notification.IsInternal)
                return;

            // Only display if different to current notification
            if (notification.Guid == _currentNotification)
                return;

            // Cancel any pending close
            _closeCts?.Cancel();

            // Remember this as the "active" one
            _currentNotification = notification.Guid;
            _closeCts = new CancellationTokenSource();

            // Wait and hide previous InfoBar, if any
            if (IsInfoBarOpen)
            {
                IsInfoBarOpen = false;
                await Task.Delay(1000);
            }

            // Set up the InfoBar
            InfoBarTitle = notification.Title;
            InfoBarMessage = notification.Message;
            InfoBarSeverity = notification.Severity;
            IsInfoBarOpen = true;

            // After 5 seconds, close automatically
            if (notification.IsIndeterminate)
                _ = AutoCloseAfterDelayAsync(_closeCts.Token);
        }

        private void NotificationManager_Discarded(Notification notification)
        {
            UIHelper.TryBeginInvoke(() => DiscardNotification(notification));
        }

        private void DiscardNotification(Notification notification)
        {
            if (!notification.IsInternal)
                return;

            // Only hide if discarding current notification
            if (notification.Guid != _currentNotification)
                return;

            // cancel the pending auto-close so it doesn't race
            _closeCts?.Cancel();

            // immediately hide the bar
            IsInfoBarOpen = false;

            // Clear the active marker
            _currentNotification = Guid.Empty;
        }

        private async Task AutoCloseAfterDelayAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);

                // Only close if nothing else has replaced it
                if (!ct.IsCancellationRequested)
                {
                    IsInfoBarOpen = false;
                    _currentNotification = Guid.Empty;
                }
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "PerformanceManagerEnabled":
                    {
                        bool enabled = Convert.ToBoolean(value);
                        IsNavPerformanceEnabled = enabled;
                        if (!enabled && MainWindow.CurrentPageName == "PerformancePage")
                            UIHelper.TryBeginInvoke(() => MainWindow.GetCurrent()?.NavigateToPage("ControllerPage"));
                    }
                    break;

                case "LibraryPageEnabled":
                    {
                        bool enabled = Convert.ToBoolean(value);
                        IsNavLibraryVisible = enabled;
                        if (!enabled && MainWindow.CurrentPageName == "LibraryPage")
                            UIHelper.TryBeginInvoke(() => MainWindow.GetCurrent()?.NavigateToPage("ControllerPage"));
                    }
                    break;

                case "MainWindowApplyNoise":
                    {
                        MainWindowApplyNoise = Convert.ToBoolean(value);
                    }
                    break;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ManagerFactory.notificationManager.Added -= NotificationManager_Added;
                ManagerFactory.notificationManager.Discarded -= NotificationManager_Discarded;
                ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

                _closeCts?.Cancel();
            }

            base.Dispose(disposing);
        }
    }
}
