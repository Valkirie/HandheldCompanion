using HandheldCompanion.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HandheldCompanion.Extensions
{
    public static class UIThreadExtensions
    {
        public static void ReplaceWith<T>(this ObservableCollection<T> collection, List<T> list)
        {
            UIHelper.TryInvoke(() =>
            {
                collection.Clear();
                foreach (var item in list)
                {
                    collection.Add(item);
                }
            });
        }

        public static void SafeAdd<T>(this ObservableCollection<T> collection, T item)
        {
            UIHelper.TryInvoke(() =>
            {
                collection.Add(item);
            });
        }

        public static void SafeRemove<T>(this ObservableCollection<T> collection, T item)
        {
            UIHelper.TryInvoke(() =>
            {
                collection.Remove(item);
            });
        }
    }
}
