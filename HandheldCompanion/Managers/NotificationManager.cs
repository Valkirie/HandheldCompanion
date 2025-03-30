using HandheldCompanion.Notifications;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public class NotificationManager : IManager
    {
        public event AddedEventHandler Added;
        public delegate void AddedEventHandler(Notification notification);

        public event DiscardedEventHandler Discarded;
        public delegate void DiscardedEventHandler(Notification notification);

        public List<Notification> Notifications = new();
        public bool Any => Notifications.Any();
        public int Count => Notifications.Count;

        public void Add(Notification notification)
        {
            Notifications.Add(notification);
            Added?.Invoke(notification);
        }

        public void Discard(Notification notification)
        {
            Notifications.Remove(notification);
            Discarded?.Invoke(notification);
        }
    }
}
