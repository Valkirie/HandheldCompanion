using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels.Pages
{
    public class NotificationPageViewModel : BaseViewModel
    {
        public ObservableCollection<NotificationViewModel> Notifications { get; set; } = [];
        public bool HasNotifications => Notifications.Any();

        public NotificationPageViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Notifications, _collectionLock);

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
        }

        private void QueryNotifications()
        {
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

        public void NotificationManager_Discarded(Notification notification)
        {
            if (notification.IsInternal)
                return;

            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                NotificationViewModel? foundNotification = Notifications.FirstOrDefault(n => n.Notification == notification || n.Notification.Guid == notification.Guid);
                if (foundNotification is not null)
                {
                    Notifications.Remove(foundNotification);
                    foundNotification.Dispose();
                }
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }

            OnPropertyChanged(nameof(HasNotifications));
        }

        public void NotificationManager_Added(Notification notification)
        {
            if (notification.IsInternal)
                return;

            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                NotificationViewModel? foundNotification = Notifications.FirstOrDefault(n => n.Notification == notification || n.Notification.Guid == notification.Guid);
                if (foundNotification is null)
                    Notifications.Add(new NotificationViewModel(notification));
                else
                    foundNotification.Notification = notification;
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }

            OnPropertyChanged(nameof(HasNotifications));
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ManagerFactory.notificationManager.Initialized -= NotificationManager_Initialized;
                ManagerFactory.notificationManager.Added -= NotificationManager_Added;
                ManagerFactory.notificationManager.Discarded -= NotificationManager_Discarded;
            }

            base.Dispose(disposing);
        }
    }
}
