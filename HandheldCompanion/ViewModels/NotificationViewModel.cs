using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels.Commands;
using HandheldCompanion.ViewModels.Pages;
using HandheldCompanion.Views.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;

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

        private NotificationPageViewModel NotificationPage;

        public ICommand InfoBarCloseCommand { get; }
        public ICommand ActionCommand { get; }

        public NotificationViewModel(NotificationPageViewModel notificationPage, Notification notification)
        {
            Notification = notification;
            NotificationPage = notificationPage;

            InfoBarCloseCommand = new RelayCommand(OnInfoBarClosed);
            ActionCommand = new RelayCommand(OnInfobarAction);
        }

        private void OnInfoBarClosed(object obj)
        {
            ManagerFactory.notificationManager.Discard(Notification);
        }

        private void OnInfobarAction(object obj)
        {
            Notification.Execute();
        }
    }
}
