using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using System.Collections.ObjectModel;
using System.Linq;
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
            BindingOperations.EnableCollectionSynchronization(Notifications, new object());

            // manage events
            ManagerFactory.notificationManager.Added += NotificationManager_Added;
            ManagerFactory.notificationManager.Discarded += NotificationManager_Discarded;

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
            foreach (Notification notification in ManagerFactory.notificationManager.Notifications)
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

            NotificationViewModel? foundNotification = Notifications.FirstOrDefault(n => n.Notification == notification || n.Notification.Guid == notification.Guid);
            if (foundNotification is not null)
            {
                Notifications.SafeRemove(foundNotification);
                foundNotification.Dispose();
            }

            OnPropertyChanged(nameof(HasNotifications));
        }

        public void NotificationManager_Added(Notification notification)
        {
            if (notification.IsInternal)
                return;

            NotificationViewModel? foundNotification = Notifications.FirstOrDefault(n => n.Notification == notification || n.Notification.Guid == notification.Guid);
            if (foundNotification is null)
                Notifications.SafeAdd(new NotificationViewModel(notification));
            else
                foundNotification.Notification = notification;

            OnPropertyChanged(nameof(HasNotifications));
        }

        public override void Dispose()
        {
            ManagerFactory.notificationManager.Added -= NotificationManager_Added;
            ManagerFactory.notificationManager.Discarded -= NotificationManager_Discarded;
        }
    }
}
