using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ControllerCommon.Utils;

public static class WPFUtils
{
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield return (T)Enumerable.Empty<T>();
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var ithChild = VisualTreeHelper.GetChild(depObj, i);
            if (ithChild == null) continue;
            if (ithChild is T t) yield return t;
            foreach (var childOfChild in FindVisualChildren<T>(ithChild)) yield return childOfChild;
        }
    }
}