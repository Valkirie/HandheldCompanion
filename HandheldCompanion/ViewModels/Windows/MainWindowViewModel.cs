using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private bool _isInfoBarOpen;
        private string _infoBarMessage;
        private string _infoBarTitle;
        private InfoBarSeverity _infoBarSeverity;

        private Guid _currentNotification;
        private CancellationTokenSource _closeCts;

        public MainWindowViewModel()
        {
            // manage events
            ManagerFactory.notificationManager.Added += NotificationManager_Added;
            ManagerFactory.notificationManager.Discarded += NotificationManager_Discarded;

            DismissInfoBarCommand = new DelegateCommand(async () =>
            {
                IsInfoBarOpen = false;
            });
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

        private async void NotificationManager_Added(Notification notification)
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
                await Task.Delay(1000).ConfigureAwait(false);
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

        private async void NotificationManager_Discarded(Notification notification)
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

        public override void Dispose()
        {
            ManagerFactory.notificationManager.Added -= NotificationManager_Added;
            ManagerFactory.notificationManager.Discarded -= NotificationManager_Discarded;

            _closeCts?.Cancel();
            base.Dispose();
        }
    }
}
