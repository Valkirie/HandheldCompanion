﻿using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Control = System.Windows.Controls.Control;

namespace HandheldCompanion.Utils;

public static class WPFUtils
{
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_CHANGEUISTATE = 0x0127;
    public const int UIS_SET = 1;
    public const int UIS_CLEAR = 2;
    public const int UISF_HIDEFOCUS = 0x1;

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    public static HwndSource GetControlHandle(Control control)
    {
        return PresentationSource.FromVisual(control) as HwndSource;
    }

    public static void MakeFocusVisible(Control c)
    {
        HwndSource hwndSource = GetControlHandle(c);
        if (hwndSource == null)
            return;

        IntPtr hWnd = hwndSource.Handle;
        SendMessage(hWnd, 257, 0x0000000000000009, (IntPtr)0x00000000c00f0001);
        // SendMessage(hWnd, WM_CHANGEUISTATE, (IntPtr)MakeLong((int)UIS_CLEAR, (int)UISF_HIDEFOCUS), IntPtr.Zero);
    }

    public static void MakeFocusInvisible(Control c)
    {
        HwndSource hwndSource = GetControlHandle(c);
        if (hwndSource == null)
            return;

        IntPtr hWnd = hwndSource.Handle;
        SendMessage(hWnd, WM_CHANGEUISTATE, MakeLong(UIS_SET, UISF_HIDEFOCUS), IntPtr.Zero);
    }

    public static int MakeLong(int wLow, int wHigh)
    {
        int low = IntLoWord(wLow);
        short high = IntLoWord(wHigh);
        int product = 0x10000 * high;
        int mkLong = low | product;
        return mkLong;
    }

    private static short IntLoWord(int word)
    {
        return (short)(word & short.MaxValue);
    }

    // A function that takes a list of controls and returns the top-left control
    public static Control GetTopLeftControl<T>(List<Control> controls) where T : Control
    {
        // filter list
        controls = controls.Where(c => c is T && c.IsEnabled).ToList();

        // If no controls are found, return null
        if (controls == null || controls.Count == 0)
        {
            return null;
        }

        // Initialize the top left control with the first element of the list
        Control topLeft = controls[0];

        // Browse other list items
        for (int i = 1; i < controls.Count; i++)
        {
            // Get current control
            Control current = controls[i];

            // Compare the Canvas.Top and Canvas.Left properties of the current control with those of the top-left control
            // If the current control is farther up or to the left, replace it with the farthest control to the left
            if (Canvas.GetTop(current) < Canvas.GetTop(topLeft) || (Canvas.GetTop(current) == Canvas.GetTop(topLeft) && Canvas.GetLeft(current) < Canvas.GetLeft(topLeft)))
            {
                topLeft = current;
            }
        }

        // Return the top left control
        return topLeft;
    }

    public enum Direction { None, Left, Right, Up, Down }

    public static Control GetClosestControl<T>(Control source, List<Control> controls, Direction direction, List<Type> typesToIgnore = null) where T : Control
    {
        // Filter list based on requested type
        controls = controls.Where(c => c is T && c.IsEnabled && c.Opacity != 0).ToList();

        // Filter based on exclusion type list
        if (typesToIgnore is not null)
            controls = controls.Where(c => !typesToIgnore.Contains(c.GetType())).ToList();

        // Filter out the controls that are not in the given direction
        controls = controls.Where(c => c != source && IsInDirection(source, c, direction)).ToList();

        // If no controls are found, return source
        if (controls.Count == 0) return source;

        /*
        // Group controls by their nearest common parent
        var groupedControls = controls
            .GroupBy(c => GetNearestCommonParent(source, c))
            .OrderBy(g => g.Key == null ? double.MaxValue : GetDistanceV2(source, g.First(), direction))
            .ToList();
        */

        // Flatten the groups and sort controls by distance
        Control[] closestControls = controls.OrderBy(c => GetDistanceV3(source, c, direction)).ToArray();
        return closestControls.FirstOrDefault();
    }

    // Helper method to find the nearest common parent of two controls
    private static DependencyObject GetNearestCommonParent(Control c1, Control c2)
    {
        // Get the visual tree parents of both controls
        var parents1 = GetVisualParents(c1).ToList();
        var parents2 = GetVisualParents(c2).ToList();

        // Find the nearest common parent
        return parents1.Intersect(parents2).FirstOrDefault();
    }

    // Helper method to get all visual parents of a control
    private static IEnumerable<DependencyObject> GetVisualParents(DependencyObject child)
    {
        while (child != null)
        {
            yield return child;
            child = VisualTreeHelper.GetParent(child);
        }
    }

    // Helper method to check if a control is in a given direction from another control
    private static bool IsInDirection(Control source, Control target, Direction direction)
    {
        var p = target.TranslatePoint(new Point(0, 0), source);
        double x = Math.Round(p.X);
        double y = Math.Round(p.Y);

        switch (direction)
        {
            case Direction.Left:
                return x + (target.ActualWidth / 2) <= 0;
            case Direction.Right:
                return x >= (source.ActualWidth / 2);
            case Direction.Up:
                return y + (target.ActualHeight / 2) <= 0;
            case Direction.Down:
                return y >= (source.ActualHeight / 2);
            default:
                return false;
        }
    }

    public static double GetDistanceV2(Control c1, Control c2, Direction direction)
    {
        try
        {
            // We retrieve the control's bounding box
            Rect r1 = c1.TransformToVisual(c1).TransformBounds(new Rect(c1.RenderSize));
            Rect r2 = c2.TransformToVisual(c1).TransformBounds(new Rect(c2.RenderSize));

            // Calculate the horizontal and vertical distances between the edges of the rectangles
            double dx = Math.Max(0, Math.Max(r1.Left, r2.Left) - Math.Min(r1.Right, r2.Right));
            double dy = Math.Max(0, Math.Max(r1.Top, r2.Top) - Math.Min(r1.Bottom, r2.Bottom));

            // Return the Euclidean distance between the nearest edges
            return Math.Sqrt(dx * dx + dy * dy);
        }
        catch
        {
            return 9999.0d;
        }
    }

    private static Rect GetBoundsRelativeTo(FrameworkElement ctrl, Visual relativeTo)
    {
        return ctrl
            .TransformToVisual(relativeTo)
            .TransformBounds(new Rect(ctrl.RenderSize));
    }

    // core point-to-point measurer
    private static double Measure(
        Rect r1, Rect r2,
        Func<Rect, Point> a1,
        Func<Rect, Point> a2)
    {
        var p1 = a1(r1);
        var p2 = a2(r2);
        return (p2 - p1).Length;
    }

    public static double GetDistanceV3(
        FrameworkElement c1,
        FrameworkElement c2,
        Direction direction = Direction.None)
    {
        // 1) pick a shared coordinate space (their Window)
        var win = Window.GetWindow(c1)
                  ?? throw new InvalidOperationException(
                       "Controls must live in the same Window.");

        // 2) get their bounding boxes in window-coords
        Rect r1 = GetBoundsRelativeTo(c1, win);
        Rect r2 = GetBoundsRelativeTo(c2, win);

        // —— NEW: if one rect is fully inside the other, treat as zero distance
        bool c1InC2 = r2.Contains(r1.TopLeft) && r2.Contains(r1.BottomRight);
        bool c2InC1 = r1.Contains(r2.TopLeft) && r1.Contains(r2.BottomRight);
        if (c1InC2 || c2InC1)
            return 0;

        // 3) direction-aware logic
        switch (direction)
        {
            case Direction.Left:
                // from c1’s left-edge center → c2’s right-edge center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left, r.Top + r.Height / 2),
                  r => new Point(r.Right, r.Top + r.Height / 2)
                );

            case Direction.Right:
                // from c1’s right center → c2’s left center
                return Measure(
                  r1, r2,
                  r => new Point(r.Right, r.Top + r.Height / 2),
                  r => new Point(r.Left, r.Top + r.Height / 2)
                );

            case Direction.Up:
                // from c1’s top center → c2’s bottom center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left + r.Width / 2, r.Top),
                  r => new Point(r.Left + r.Width / 2, r.Bottom)
                );

            case Direction.Down:
                // from c1’s bottom center → c2’s top center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left + r.Width / 2, r.Bottom),
                  r => new Point(r.Left + r.Width / 2, r.Top)
                );

            case Direction.None:
            default:
                // edge-to-edge minimal distance:
                double dx = Math.Max(0,
                               Math.Max(r1.Left, r2.Left) - Math.Min(r1.Right, r2.Right));
                double dy = Math.Max(0,
                               Math.Max(r1.Top, r2.Top) - Math.Min(r1.Bottom, r2.Bottom));
                return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public static List<FrameworkElement> FindChildren(DependencyObject startNode)
    {
        int count = VisualTreeHelper.GetChildrenCount(startNode);
        List<FrameworkElement> childs = [];

        for (int i = 0; i < count; i++)
        {
            DependencyObject current = VisualTreeHelper.GetChild(startNode, i);

            string currentType = current.GetType().Name;
            switch (currentType)
            {
                case "TextBox":
                    {
                        TextBox textBox = (TextBox)current;
                        if (!textBox.IsReadOnly)
                            goto case "Slider";
                    }
                    break;

                case "RepeatButton":
                    {
                        RepeatButton repeatButton = (RepeatButton)current;
                        if (!repeatButton.Name.StartsWith("PART_"))
                        {
                            // skip if repeat button is part of scrollbar
                            goto case "Slider";
                        }
                    }
                    break;

                case "Button":
                    {
                        Button button = (Button)current;
                        if (button.Name.Equals("NavigationViewBackButton"))
                            break;
                        else if (button.Name.Equals("TogglePaneButton"))
                            break;
                        else
                            goto case "Slider";
                    }
                    break;

                case "Slider":
                case "ToggleSwitch":
                case "NavigationViewItem":
                case "ComboBox":
                case "ComboBoxItem":
                case "ListView":
                case "ListViewItem":
                case "AppBarButton":
                case "ToggleButton":
                case "CheckBox":
                case "RadioButton":
                    {
                        FrameworkElement asType = (FrameworkElement)current;
                        if (asType.IsEnabled && asType.Focusable && asType.IsVisible)
                            childs.Add(asType);
                    }
                    break;
            }

            foreach (var item in FindChildren(current))
            {
                childs.Add(item);
            }
        }

        return childs;
    }

    public static T FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        T parent = null;
        if (child is null)
        {
            return parent;
        }
        DependencyObject CurrentParent = VisualTreeHelper.GetParent(child);
        while (CurrentParent is not null)
        {
            if (CurrentParent is T)
            {
                parent = (T)CurrentParent;
                break;
            }
            CurrentParent = VisualTreeHelper.GetParent(CurrentParent);
        }
        return parent;
    }

    public static Visual FindCommonAncestor(Visual visual1, Visual visual2)
    {
        var ancestor1 = visual1;
        while (ancestor1 != null)
        {
            var ancestor2 = visual2;
            while (ancestor2 != null)
            {
                if (ancestor1 == ancestor2)
                {
                    return ancestor1;
                }
                ancestor2 = VisualTreeHelper.GetParent(ancestor2) as Visual;
            }
            ancestor1 = VisualTreeHelper.GetParent(ancestor1) as Visual;
        }
        return null;
    }

    public static Point TransformToAncestor(Visual child, Visual ancestor, Point point)
    {
        return child.TransformToAncestor(ancestor).Transform(point);
    }

    public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
            return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            T childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }

        return null;
    }

    // Helper method to find all visual children of a given type
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent != null)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T)
                {
                    yield return (T)child;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }

    // Returns all FrameworkElement of specified type from a list, where their parent or parents of their parent is oftype() Popup
    public static List<T> GetElementsFromPopup<T>(List<FrameworkElement> elements) where T : FrameworkElement
    {
        // Create an empty list to store the result
        List<T> result = [];

        // Loop through each element in the input list
        foreach (FrameworkElement element in elements)
        {
            // Check if the element is of the specified type
            if (element is T)
            {
                // Get the parent of the element
                FrameworkElement parent = element.Parent as FrameworkElement;

                // Loop until the parent is null or a Popup
                while (parent != null && (!(parent is Popup) && !(parent is ContentDialog)))
                {
                    // Get the parent of the parent
                    parent = parent.Parent as FrameworkElement;
                }

                // Check if the parent is a Popup
                if (parent is Popup || parent is ContentDialog)
                {
                    // Add the element to the result list
                    result.Add(element as T);
                }
            }
        }

        // Return the result list
        return result;
    }

    public static void SendKeyToControl(Control control, int keyCode)
    {
        SendMessage(GetControlHandle(control).Handle.ToInt32(), WM_KEYDOWN, keyCode, IntPtr.Zero);
    }
}