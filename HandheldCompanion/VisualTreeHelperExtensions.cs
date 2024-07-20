using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace HandheldCompanion
{
    public static class VisualTreeHelperExtensions
    {
        public static T FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public static DependencyObject FindCommonAncestor(DependencyObject obj1, DependencyObject obj2)
        {
            var ancestors1 = new List<DependencyObject>();
            var current = obj1;

            while (current != null)
            {
                ancestors1.Add(current);
                current = VisualTreeHelper.GetParent(current);
            }

            current = obj2;

            while (current != null)
            {
                if (ancestors1.Contains(current))
                {
                    return current;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
