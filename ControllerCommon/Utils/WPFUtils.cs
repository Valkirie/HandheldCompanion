using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;

namespace ControllerCommon.Utils;

public static class WPFUtils
{
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    public const int WM_CHANGEUISTATE = 0x0127;
    public const int UIS_SET = 1;
    public const int UIS_CLEAR = 2;
    public const int UISF_HIDEFOCUS = 0x1;

    public static HwndSource GetControlHandle(Control control)
    {
        return PresentationSource.FromVisual(control) as HwndSource;
    }

    public static void MakeFocusVisible(Control c)
    {
        IntPtr hWnd = GetControlHandle(c).Handle;
        // SendMessage(hWnd, WM_CHANGEUISTATE, (IntPtr)MakeLong((int)UIS_CLEAR, (int)UISF_HIDEFOCUS), IntPtr.Zero);
        SendMessage(hWnd, 257, (IntPtr)0x0000000000000009, (IntPtr)0x00000000c00f0001);
    }

    public static void MakeFocusInvisible(Control c)
    {
        IntPtr hWnd = GetControlHandle(c).Handle;
        SendMessage(hWnd, WM_CHANGEUISTATE, (IntPtr)MakeLong((int)UIS_SET, (int)UISF_HIDEFOCUS), IntPtr.Zero);
    }

    public static int MakeLong(int wLow, int wHigh)
    {
        int low = (int)IntLoWord(wLow);
        short high = IntLoWord(wHigh);
        int product = 0x10000 * (int)high;
        int mkLong = (int)(low | product);
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
        controls = controls.Where(c => c is T).ToList();

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
        controls = controls.Where(c => c is T).ToList();

        // Filter based on exclusion type list
        if (typesToIgnore is not null)
            controls = controls.Where(c => !typesToIgnore.Contains(c.GetType())).ToList();

        // Filter out the controls that are not in the given direction
        controls = controls.Where(c => c != source && IsInDirection(source, c, direction)).ToList();

        // If no controls are found, return source
        if (controls.Count == 0) return source;

        // Find the control with the same parent and the minimum distance to the source
        // If no control has the same parent, find the control with the minimum distance to the source
        controls = controls.OrderBy(c => GetDistanceV2(source, c, Direction.None)).ToList();

        return controls.First();
    }

    // Helper method to check if a control is in a given direction from another control
    private static bool IsInDirection(Control source, Control target, Direction direction)
    {
        // Get the position of the target on the canvas
        var p = target.TranslatePoint(new Point(0, 0), source);
        double x = p.X;
        double y = p.Y;

        switch (direction)
        {
            case Direction.Left:
                return x + target.ActualWidth <= 0;
            case Direction.Right:
                return x >= source.ActualWidth;
            case Direction.Up:
                return y + target.ActualHeight <= 0;
            case Direction.Down:
                return y >= source.ActualHeight;
            default:
                return false;
        }
    }

    // Helper method to calculate the distance between the centers of two controls
    private static double GetDistance(Control source, Control target, Direction direction)
    {
        try
        {
            // Get the relative position of the target with respect to the source
            var transform = target.TransformToVisual(source);
            var position = transform.Transform(new Point(0, 0));

            double dx = source.ActualWidth / 2 - (position.X + target.ActualWidth / 2);
            double dy = source.ActualHeight / 2 - (position.Y + target.ActualHeight / 2);

            switch (direction)
            {
                case Direction.Up:
                case Direction.Down:
                    return Math.Sqrt(dy * dy);

                case Direction.Left:
                case Direction.Right:
                    return Math.Sqrt(dx * dx);
            }

            return Math.Sqrt(dx * dx + dy * dy);
        }
        catch { }

        return 9999.0d;
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
        catch { }

        return 9999.0d;
    }

    public static List<Control> FindChildren(DependencyObject startNode)
    {
        int count = VisualTreeHelper.GetChildrenCount(startNode);
        List<Control> childs = new List<Control>();

        for (int i = 0; i < count; i++)
        {
            DependencyObject current = VisualTreeHelper.GetChild(startNode, i);

            string currentType = current.GetType().Name;
            switch (currentType)
            {
                case "Button":
                case "Slider":
                case "ToggleSwitch":
                case "NavigationViewItem":
                case "ComboBox":
                case "ComboBoxItem":
                case "AppBarButton":
                case "ToggleButton":
                case "CheckBox":
                case "RadioButton":
                    {
                        Control asType = (Control)current;

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
}