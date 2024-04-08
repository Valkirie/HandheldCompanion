using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace HandheldCompanion.Extensions
{
    public static class UIThreadExtensions
    {
        public static void ReplaceWith<T>(this ObservableCollection<T> collection, List<T> list)
        {
            Application.Current.Dispatcher.Invoke(() =>
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                collection.Add(item);
            });
        }

        public static void SafeRemove<T>(this ObservableCollection<T> collection, T item)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                collection.Remove(item);
            });
        }
    }
}
