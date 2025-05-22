using HandheldCompanion.Notifications;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public class NotificationManager : IManager
    {
        public event AddedEventHandler Added;
        public delegate void AddedEventHandler(Notification notification);

        public event DiscardedEventHandler Discarded;
        public delegate void DiscardedEventHandler(Notification notification);

        public ConcurrentList<Notification> Notifications = new();
        public bool Any => Notifications.Any();
        public int Count => Notifications.Count;

        public void Add(Notification notification)
        {
            if (!notification.IsInternal)
                Notifications.Add(notification);

            Added?.Invoke(notification);
        }

        public void Discard(Notification notification)
        {
            if (!notification.IsInternal)
                Notifications.Remove(notification);

            Discarded?.Invoke(notification);
        }
    }
}
