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

        public static void SafeInsert<T>(this ObservableCollection<T> collection, int index, T item)
        {
            UIHelper.TryInvoke(() =>
            {
                // Ensure the index is within valid bounds
                if (index > collection.Count || index < 0)
                    index = collection.Count;

                collection.Insert(index, item);
            });
        }

        public static void SafeRemove<T>(this ObservableCollection<T> collection, T item)
        {
            UIHelper.TryInvoke(() =>
            {
                collection.Remove(item);
            });
        }

        public static void SafeClear<T>(this ObservableCollection<T> collection)
        {
            UIHelper.TryInvoke(() =>
            {
                collection.Clear();
            });
        }
    }
}
