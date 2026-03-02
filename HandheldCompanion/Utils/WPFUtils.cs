using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Documents;
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
    public static Control GetTopLeftControl<T>(List<Control> controls, List<Type> typesToIgnore = null) where T : Control
    {
        // filter list
        controls = controls.Where(c => c is T && c.IsEnabled).ToList();

        // Filter based on exclusion type list
        if (typesToIgnore is not null)
            controls = controls.Where(c => !typesToIgnore.Contains(c.GetType())).ToList();

        // If no controls are found, return null
        if (controls == null || controls.Count == 0)
            return null;

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

    public static Control? GetClosestControl<T>(Control source, List<Control> controls, Direction direction, List<Type> typesToIgnore = null) where T : Control
    {
        // Priority pass: if the source is inside an open Expander, first look for
        // focusable descendants of that same Expander in the requested direction.
        // This ensures Down/Up navigation traverses the Expander's own children
        // (e.g. SettingsExpander.Items) before jumping to controls outside it.
        // When the Expander is collapsed its content is invisible, so FindChildren
        // will not have returned those controls and this pass produces no candidates —
        // navigation then falls through to the global pass naturally.
        Expander expander = FindParent<Expander>(source);
        if (expander is not null && expander.IsExpanded)
        {
            List<Control> scopedControls = controls
                .Where(c => GetVisualParents(c).Contains(expander))
                .ToList();

            List<Control> scopedCandidates = GetControlInDirection<T>(source, scopedControls, direction, typesToIgnore, strictAxis: false);
            if (scopedCandidates.Count > 0)
                return scopedCandidates.OrderBy(c => GetDistanceV3(source, c, direction)).First();
        }

        // Standard pass: prefer candidates whose bounding box overlaps the source on the secondary axis.
        // This prevents L/R navigation from jumping to elements far above or below the current row.
        List<Control> candidates = GetControlInDirection<T>(source, controls, direction, typesToIgnore, strictAxis: true);
        if (candidates.Count > 0)
            return candidates.OrderBy(c => GetDistanceV3(source, c, direction)).First();

        // Relaxed pass: no strict-axis requirement, but restrict to controls whose secondary-axis
        // bounding box is within one control-height (or -width) of the source's own boundary.
        // This lets small controls in immediately adjacent rows/columns be reached
        // (e.g. the '+' button and the Gear/Remove buttons in ButtonStackTemplate/ButtonMappingTemplate
        // share no vertical overlap, yet they are only a few pixels apart on the secondary axis).
        // Controls that are far away on the secondary axis are still excluded so navigation
        // does not jump to completely unrelated rows or columns.
        List<Control> relaxedCandidates = GetControlInDirection<T>(source, controls, direction, typesToIgnore, strictAxis: false)
            .Where(c => SecondaryAxisGap(source, c, direction) <=
                (direction is Direction.Left or Direction.Right
                    ? Math.Min(source.ActualHeight, c.ActualHeight)
                    : Math.Min(source.ActualWidth, c.ActualWidth)))
            .ToList();
        if (relaxedCandidates.Count > 0)
            return relaxedCandidates.OrderBy(c => GetDistanceV3(source, c, direction)).First();

        return source;
    }

    // Returns the gap in pixels between the source and target bounding boxes
    // on the secondary axis for a given navigation direction:
    //   Left / Right → vertical gap   (secondary axis = Y)
    //   Up   / Down  → horizontal gap (secondary axis = X)
    // Returns 0 when the boxes touch or overlap.
    private static double SecondaryAxisGap(Control source, Control target, Direction direction)
    {
        var p = target.TranslatePoint(new Point(0, 0), source);
        double x = Math.Round(p.X);
        double y = Math.Round(p.Y);

        switch (direction)
        {
            case Direction.Left:
            case Direction.Right:
                // Vertical gap between [0, source.H] and [y, y + target.H]
                return Math.Max(0, Math.Max(y - source.ActualHeight, -(y + target.ActualHeight)));
            case Direction.Up:
            case Direction.Down:
                // Horizontal gap between [0, source.W] and [x, x + target.W]
                return Math.Max(0, Math.Max(x - source.ActualWidth, -(x + target.ActualWidth)));
            default:
                return double.MaxValue;
        }
    }

    public static Control? GetFurthestControl<T>(Control source, List<Control> controls, Direction direction, List<Type> typesToIgnore = null) where T : Control
    {
        List<Control> controlsInDirection = GetControlInDirection<T>(source, controls, direction, typesToIgnore);

        // If no controls are found, return source
        if (controlsInDirection.Count == 0) return source;

        // Flatten the groups and sort controls by distance
        Control[] furthests = controlsInDirection.OrderByDescending(c => GetDistanceV3(source, c, direction)).ToArray();
        return furthests.FirstOrDefault();
    }


    private static List<Control> GetControlInDirection<T>(Control source, List<Control> controls, Direction direction, List<Type> typesToIgnore = null, bool strictAxis = false) where T : Control
    {
        // Filter list based on requested type
        controls = controls.Where(c => c is T && c.IsEnabled && c.Opacity != 0).ToList();

        // Filter based on exclusion type list
        if (typesToIgnore is not null)
            controls = controls.Where(c => !typesToIgnore.Contains(c.GetType())).ToList();

        // Filter out the controls that are not in the given direction
        controls = controls.Where(c => c != source && IsInDirection(source, c, direction, strictAxis)).ToList();

        return controls;
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

    // Helper method to check if a control is in a given direction from another control.
    // When strictAxis is true, also requires bounding-box overlap on the secondary axis:
    //   Left/Right → vertical ranges must overlap (guards against jumping to rows above/below)
    //   Up/Down    → horizontal ranges must overlap (guards against jumping to columns left/right)
    private static bool IsInDirection(Control source, Control target, Direction direction, bool strictAxis = false)
    {
        var p = target.TranslatePoint(new Point(0, 0), source);
        double x = Math.Round(p.X);
        double y = Math.Round(p.Y);

        switch (direction)
        {
            case Direction.Left:
                if (x + (target.ActualWidth / 2) > 0) return false;
                return !strictAxis || (y <= source.ActualHeight && y + target.ActualHeight >= 0);
            case Direction.Right:
                if (x < (source.ActualWidth / 2)) return false;
                return !strictAxis || (y <= source.ActualHeight && y + target.ActualHeight >= 0);
            case Direction.Up:
                if (y + (target.ActualHeight / 2) > 0) return false;
                return !strictAxis || (x <= source.ActualWidth && x + target.ActualWidth >= 0);
            case Direction.Down:
                if (y < (source.ActualHeight / 2)) return false;
                return !strictAxis || (x <= source.ActualWidth && x + target.ActualWidth >= 0);
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
                // from c1's left-edge center -> c2's right-edge center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left, r.Top + r.Height),
                  r => new Point(r.Right, r.Top + r.Height)
                );

            case Direction.Right:
                // from c1's right center -> c2's left center
                return Measure(
                  r1, r2,
                  r => new Point(r.Right, r.Top + r.Height),
                  r => new Point(r.Left, r.Top + r.Height)
                );

            case Direction.Up:
                // from c1's top center -> c2's bottom center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left + r.Width, r.Top),
                  r => new Point(r.Left + r.Width, r.Bottom)
                );

            case Direction.Down:
                // from c1's bottom center -> c2's top center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left + r.Width, r.Bottom),
                  r => new Point(r.Left + r.Width, r.Top)
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

                        // skip if read only
                        if (textBox.IsReadOnly)
                            break;

                        goto case "Slider";
                    }

                case "RepeatButton":
                    {
                        RepeatButton repeatButton = (RepeatButton)current;

                        // skip if repeat button is part of scrollbar
                        if (repeatButton.Name.StartsWith("PART_"))
                            break;

                        goto case "Slider";
                    }

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

                case "SettingsCard":
                    {
                        SettingsCard settingsCard = (SettingsCard)current;

                        // skip if not clickable
                        if (!settingsCard.IsClickEnabled)
                            break;

                        goto case "Slider";
                    }

                case "DropDownButton":
                    goto case "Slider";

                case "MenuItem":
                    goto case "Slider";

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
                case "HyperlinkButton":
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

    // Shared core: walks each element's visual parent chain, adds the element to the
    // result if the chain terminates at a node satisfying isTarget before hitting an
    // earlyStop sentinel (or null).  Used by the two public helpers below.
    private static List<T> GetElementsByAncestor<T>(
        List<FrameworkElement> elements,
        Func<DependencyObject, bool> isTarget,
        Func<DependencyObject, bool>? earlyStop = null)
        where T : FrameworkElement
    {
        List<T> result = [];
        foreach (FrameworkElement element in elements)
        {
            if (element is not T typed) continue;

            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent is not null && !isTarget(parent) && earlyStop?.Invoke(parent) != true)
                parent = VisualTreeHelper.GetParent(parent);

            if (parent is not null && isTarget(parent))
                result.Add(typed);
        }
        return result;
    }

    // Returns elements whose visual parent chain reaches a Popup or ContentDialog.
    // Visual (not logical) traversal is required because MenuFlyout items live
    // inside a MenuPopup in the visual tree whose logical chain does not include Popup.
    public static List<T> GetElementsFromPopup<T>(List<FrameworkElement> elements) where T : FrameworkElement
        => GetElementsByAncestor<T>(elements, p => p is Popup or ContentDialog);

    // Returns elements whose visual parent chain reaches an AdornerLayer before a
    // Window.  ContentDialog (iNKORE) is hosted in the window's adorner overlay: its
    // C# instance is a logical controller, not a visual node, so this is the only
    // reliable way to find its controls.
    public static List<T> GetElementsFromAdornerLayer<T>(List<FrameworkElement> elements) where T : FrameworkElement
        => GetElementsByAncestor<T>(elements, p => p is AdornerLayer, p => p is Window);

    public static void SendKeyToControl(Control control, int keyCode)
    {
        SendMessage(GetControlHandle(control).Handle.ToInt32(), WM_KEYDOWN, keyCode, IntPtr.Zero);
    }
}