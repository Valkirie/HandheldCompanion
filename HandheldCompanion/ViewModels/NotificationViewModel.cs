using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.ViewModels.Commands;
using iNKORE.UI.WPF.Modern.Controls;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class NotificationViewModel : BaseViewModel
    {

        private Notification _Notification;
        public Notification Notification
        {
            get => _Notification;
            set
            {
                _Notification = value;

                // refresh all properties
                OnPropertyChanged(string.Empty);
            }
        }

        public string Title => _Notification.Title;
        public string Message => _Notification.Message;
        public string Action => _Notification.Action;
        public InfoBarSeverity Severity => _Notification.Severity;
        public bool IsClosable => _Notification.IsClosable;
        public bool IsClickable => _Notification.IsClickable;

        public ICommand InfoBarCloseCommand { get; }
        public ICommand ActionCommand { get; }

        public NotificationViewModel(Notification notification)
        {
            Notification = notification;

            InfoBarCloseCommand = new RelayCommand(OnInfoBarClosed);
            ActionCommand = new RelayCommand(OnInfobarAction);
        }

        private void OnInfoBarClosed(object obj)
        {
            ManagerFactory.notificationManager.Discard(Notification);
        }

        private void OnInfobarAction(object obj)
        {
            Notification?.Execute();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
