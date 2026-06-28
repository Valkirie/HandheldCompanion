using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using static HandheldCompanion.WinAPI;
using Control = System.Windows.Controls.Control;
using ProgressBar = iNKORE.UI.WPF.Modern.Controls.ProgressBar;

namespace HandheldCompanion.Utils;

public static class WPFUtils
{
    public static bool CanTarget(FrameworkElement? control, Window? targetWindow = null, bool includeContentRules = false, bool includeNavigationViewItems = true)
    {
        return control is not null
            && control is not Window
            && control.IsLoaded
            && control.IsVisible
            && control.IsEnabled
            && control.Focusable
            && control.Opacity > 0
            && control.ActualWidth > 0
            && control.ActualHeight > 0
            && control is not ProgressBar
            && control is not ProgressRing
            && (control is not TextBox textBox || !textBox.IsReadOnly)
            && (control is not SettingsCard settingsCard || settingsCard.IsClickEnabled)
            && (!includeContentRules || !IsTransientContainerControl(control))
            && (!includeContentRules || includeNavigationViewItems || control is not NavigationViewItem)
            && (!includeContentRules || control is not ItemsControl || control is Selector)
            && (targetWindow is null || Window.GetWindow(control) == targetWindow);
    }

    private static bool IsTransientContainerControl(FrameworkElement control)
    {
        return control is iNKORE.UI.WPF.Modern.Controls.MessageBox
            || control is iNKORE.UI.WPF.Modern.Controls.ContentDialog
            || control is iNKORE.UI.WPF.Modern.Controls.SplitView
            || control is System.Windows.Controls.ScrollViewer
            || control.GetType().Name is "TouchScrollViewer"
            || control is iNKORE.UI.WPF.Modern.Controls.Frame
            || control is iNKORE.UI.WPF.Modern.Controls.Page;
    }

    public static HwndSource? GetControlHandle(Control control)
    {
        return PresentationSource.FromVisual(control) as HwndSource;
    }

    public static void MakeFocusVisible(Control c)
    {
        HwndSource? hwndSource = GetControlHandle(c);
        if (hwndSource == null)
            return;

        IntPtr hWnd = hwndSource.Handle;
        SendMessage(hWnd, 257, 0x0000000000000009, unchecked((IntPtr)0x00000000c00f0001));
        // SendMessage(hWnd, WM_CHANGEUISTATE, (IntPtr)MakeLong((int)UIS_CLEAR, (int)UISF_HIDEFOCUS), IntPtr.Zero);
    }

    public static void MakeFocusInvisible(Control c)
    {
        HwndSource? hwndSource = GetControlHandle(c);
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
    public static Control? GetTopLeftControl<T>(List<Control> controls, List<Type>? typesToIgnore = null) where T : Control
    {
        controls = controls.Where(c => c is T && CanTarget(c)).ToList();

        if (typesToIgnore is not null)
            controls = controls.Where(c => !typesToIgnore.Contains(c.GetType())).ToList();

        if (controls.Count == 0)
            return null;

        Control topLeft = controls[0];
        for (int i = 1; i < controls.Count; i++)
        {
            Control current = controls[i];
            if (Canvas.GetTop(current) < Canvas.GetTop(topLeft) || (Canvas.GetTop(current) == Canvas.GetTop(topLeft) && Canvas.GetLeft(current) < Canvas.GetLeft(topLeft)))
                topLeft = current;
        }

        return topLeft;
    }

    public enum Direction { None, Left, Right, Up, Down }

    private const double SecondaryAxisCenterOffsetThresholdMultiplier = 4.0;

    public static Control? GetClosestControl<T>(Control source, List<Control> controls, Direction direction, List<Type>? typesToIgnore = null) where T : Control
    {
        Expander? expander = FindParent<Expander>(source);
        if (expander is not null && expander.IsExpanded)
        {
            Control? scopedCandidate = GetClosestCandidate<T>(
                source,
                controls.Where(c => HasVisualAncestor(c, expander)),
                direction,
                typesToIgnore,
                strictAxis: false);
            if (scopedCandidate is not null)
                return scopedCandidate;
        }

        // When the source is inside a ScrollViewer, prefer candidates within the same
        // scroll container (even if they are scrolled out of view) over controls that
        // are visible but live in a sibling container such as a CommandBar above the
        // scroll area.  UIGamepad.Focus() calls BringIntoView(), so the selected
        // control will scroll into view automatically.
        ScrollViewer? scrollViewer = FindParent<ScrollViewer>(source);
        if (scrollViewer is not null)
        {
            IEnumerable<Control> scrollerControls = controls.Where(c => HasVisualAncestor(c, scrollViewer));
            Control? svStrict = GetClosestCandidate<T>(source, scrollerControls, direction, typesToIgnore, strictAxis: true);
            Control? svRelaxed = GetClosestCandidate<T>(source, scrollerControls, direction, typesToIgnore, strictAxis: false, restrictSecondaryAxisGap: true);
            Control? svCandidate = GetBetterCandidate(source, direction, svStrict, svRelaxed);
            if (svCandidate is not null)
                return svCandidate;
        }

        Control? strictCandidate = GetClosestCandidate<T>(source, controls, direction, typesToIgnore, strictAxis: true);
        Control? relaxedCandidate = GetClosestCandidate<T>(source, controls, direction, typesToIgnore, strictAxis: false, restrictSecondaryAxisGap: true);
        Control? candidate = GetBetterCandidate(source, direction, strictCandidate, relaxedCandidate);
        if (candidate is not null)
            return candidate;

        return source;
    }

    private static double PrimaryAxisGap(Control source, Control target, Direction direction)
    {
        try
        {
            var p = target.TranslatePoint(new Point(0, 0), source);
            double x = Math.Round(p.X);
            double y = Math.Round(p.Y);

            switch (direction)
            {
                case Direction.Left:
                    return Math.Max(0, -(x + target.ActualWidth));
                case Direction.Right:
                    return Math.Max(0, x - source.ActualWidth);
                case Direction.Up:
                    return Math.Max(0, -(y + target.ActualHeight));
                case Direction.Down:
                    return Math.Max(0, y - source.ActualHeight);
                default:
                    return double.MaxValue;
            }
        }
        catch
        {
            // Fallback: use screen coordinates for controls without shared ancestor (e.g., flyouts)
            try
            {
                Point sourceScreenPos = source.PointToScreen(new Point(source.ActualWidth / 2, source.ActualHeight / 2));
                Point targetScreenPos = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));

                double dx = targetScreenPos.X - sourceScreenPos.X;
                double dy = targetScreenPos.Y - sourceScreenPos.Y;

                switch (direction)
                {
                    case Direction.Left:
                        return Math.Max(0, -dx - target.ActualWidth / 2);
                    case Direction.Right:
                        return Math.Max(0, dx - source.ActualWidth / 2);
                    case Direction.Up:
                        return Math.Max(0, -dy - target.ActualHeight / 2);
                    case Direction.Down:
                        return Math.Max(0, dy - source.ActualHeight / 2);
                    default:
                        return double.MaxValue;
                }
            }
            catch
            {
                return double.MaxValue;
            }
        }
    }

    private static double GetDirectionalScore(Control source, Control target, Direction direction)
    {
        double primaryGap = PrimaryAxisGap(source, target, direction);
        double secondaryGap = SecondaryAxisGap(source, target, direction);
        double overallDistance = GetDistanceV3(source, target, Direction.None);
        double directionalDistance = GetDistanceV3(source, target, direction);

        return (primaryGap * 25) + (secondaryGap * 8) + (overallDistance * 6) + directionalDistance;
    }

    private static double SecondaryAxisCenterOffset(Control source, Control target, Direction direction)
    {
        try
        {
            var p = target.TranslatePoint(new Point(0, 0), source);
            double targetCenterX = Math.Round(p.X + (target.ActualWidth / 2));
            double targetCenterY = Math.Round(p.Y + (target.ActualHeight / 2));
            double sourceCenterX = Math.Round(source.ActualWidth / 2);
            double sourceCenterY = Math.Round(source.ActualHeight / 2);

            return direction switch
            {
                Direction.Left or Direction.Right => Math.Abs(targetCenterY - sourceCenterY),
                Direction.Up or Direction.Down => Math.Abs(targetCenterX - sourceCenterX),
                _ => 0
            };
        }
        catch
        {
            // Fallback: use screen coordinates for controls without shared ancestor (e.g., flyouts)
            try
            {
                Point sourceScreenCenter = source.PointToScreen(new Point(source.ActualWidth / 2, source.ActualHeight / 2));
                Point targetScreenCenter = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));

                return direction switch
                {
                    Direction.Left or Direction.Right => Math.Abs(targetScreenCenter.Y - sourceScreenCenter.Y),
                    Direction.Up or Direction.Down => Math.Abs(targetScreenCenter.X - sourceScreenCenter.X),
                    _ => 0
                };
            }
            catch
            {
                return double.MaxValue;
            }
        }
    }

    private static bool IsWithinSecondaryAxisCenterOffsetThreshold(Control source, Control target, Direction direction)
    {
        if (direction == Direction.None)
            return true;

        double sourceAxisSize = direction is Direction.Left or Direction.Right
            ? source.ActualHeight
            : source.ActualWidth;
        double targetAxisSize = direction is Direction.Left or Direction.Right
            ? target.ActualHeight
            : target.ActualWidth;

        double maxOffset = Math.Max(sourceAxisSize * SecondaryAxisCenterOffsetThresholdMultiplier, targetAxisSize);
        return SecondaryAxisCenterOffset(source, target, direction) <= maxOffset;
    }

    private static Control? GetBetterCandidate(Control source, Direction direction, Control? first, Control? second)
    {
        if (first is null)
            return second;

        if (second is null)
            return first;

        double firstScore = GetDirectionalScore(source, first, direction);
        double secondScore = GetDirectionalScore(source, second, direction);

        return secondScore < firstScore ? second : first;
    }

    // Returns the gap in pixels between the source and target bounding boxes
    // on the secondary axis for a given navigation direction:
    //   Left / Right → vertical gap   (secondary axis = Y)
    //   Up   / Down  → horizontal gap (secondary axis = X)
    // Returns 0 when the boxes touch or overlap.
    private static double SecondaryAxisGap(Control source, Control target, Direction direction)
    {
        try
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
        catch
        {
            // Fallback: use screen coordinates for controls without shared ancestor (e.g., flyouts)
            try
            {
                Point sourceScreenPos = source.PointToScreen(new Point(0, 0));
                Point targetScreenPos = target.PointToScreen(new Point(0, 0));

                double dy = targetScreenPos.Y - sourceScreenPos.Y;
                double dx = targetScreenPos.X - sourceScreenPos.X;

                switch (direction)
                {
                    case Direction.Left:
                    case Direction.Right:
                        return Math.Max(0, Math.Max(dy - source.ActualHeight, -(dy + target.ActualHeight)));
                    case Direction.Up:
                    case Direction.Down:
                        return Math.Max(0, Math.Max(dx - source.ActualWidth, -(dx + target.ActualWidth)));
                    default:
                        return double.MaxValue;
                }
            }
            catch
            {
                return double.MaxValue;
            }
        }
    }

    public static Control? GetFurthestControl<T>(Control source, List<Control> controls, Direction direction, List<Type>? typesToIgnore = null) where T : Control
    {
        List<Control> controlsInDirection = GetControlInDirection<T>(source, controls, direction, typesToIgnore);

        // If no controls are found, return source
        if (controlsInDirection.Count == 0) return source;

        // Flatten the groups and sort controls by distance
        Control[] furthests = controlsInDirection.OrderByDescending(c => GetDistanceV3(source, c, direction)).ToArray();
        return furthests.FirstOrDefault();
    }


    private static List<Control> GetControlInDirection<T>(Control source, List<Control> controls, Direction direction, List<Type>? typesToIgnore = null, bool strictAxis = false) where T : Control
    {
        // Filter list based on requested type
        controls = controls.Where(c => c is T && CanTarget(c)).ToList();

        // Filter based on exclusion type list
        if (typesToIgnore is not null)
            controls = controls.Where(c => !typesToIgnore.Contains(c.GetType())).ToList();

        // Filter out the controls that are not in the given direction
        controls = controls.Where(c => c != source && IsInDirection(source, c, direction, strictAxis)).ToList();

        return controls;
    }

    private static Control? GetClosestCandidate<T>(
        Control source,
        IEnumerable<Control> controls,
        Direction direction,
        List<Type>? typesToIgnore = null,
        bool strictAxis = false,
        bool restrictSecondaryAxisGap = false) where T : Control
    {
        Control? closest = null;
        double closestScore = double.MaxValue;
        Control? closestAncestor = null;
        double closestAncestorScore = double.MaxValue;

        foreach (Control control in controls)
        {
            if (control == source || control is not T || !CanTarget(control))
                continue;

            if (typesToIgnore is not null && typesToIgnore.Contains(control.GetType()))
                continue;

            if (!IsInDirection(source, control, direction, strictAxis))
                continue;

            if (!IsWithinSecondaryAxisCenterOffsetThreshold(source, control, direction))
                continue;

            if (restrictSecondaryAxisGap)
            {
                double axisGap = SecondaryAxisGap(source, control, direction);
                double maxGap = direction is Direction.Left or Direction.Right
                    ? Math.Min(source.ActualHeight, control.ActualHeight)
                    : Math.Min(source.ActualWidth, control.ActualWidth);

                if (axisGap > maxGap)
                    continue;
            }

            double score = GetDirectionalScore(source, control, direction);
            if (HasVisualAncestor(source, control))
            {
                if (score >= closestAncestorScore)
                    continue;

                closestAncestor = control;
                closestAncestorScore = score;
                continue;
            }

            if (score >= closestScore)
                continue;

            closest = control;
            closestScore = score;
        }

        return closest ?? closestAncestor;
    }

    // Helper method to find the nearest common parent of two controls
    private static DependencyObject? GetNearestCommonParent(Control c1, Control c2)
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

    private static bool HasVisualAncestor(DependencyObject child, DependencyObject ancestor)
    {
        while (child is not null)
        {
            if (ReferenceEquals(child, ancestor))
                return true;

            child = VisualTreeHelper.GetParent(child);
        }

        return false;
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
        try
        {
            if (relativeTo is FrameworkElement relativeToElement)
            {
                // Get screen position of the control
                Point ctrlScreenPos = ctrl.PointToScreen(new Point(0, 0));

                // Get screen position of the reference element
                Point refScreenPos = relativeToElement.PointToScreen(new Point(0, 0));

                // Calculate offset in screen coordinates
                double offsetX = ctrlScreenPos.X - refScreenPos.X;
                double offsetY = ctrlScreenPos.Y - refScreenPos.Y;

                // Create a rect in the reference element's coordinate space
                return new Rect(offsetX, offsetY, ctrl.ActualWidth, ctrl.ActualHeight);
            }
        }
        catch
        {
            try
            {
                return ctrl.TransformToVisual(relativeTo).TransformBounds(new Rect(ctrl.RenderSize));
            }
            catch { }
        }
        return Rect.Empty;
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
                  r => new Point(r.Left, r.Top + (r.Height / 2)),
                  r => new Point(r.Right, r.Top + (r.Height / 2))
                );

            case Direction.Right:
                // from c1's right center -> c2's left center
                return Measure(
                  r1, r2,
                  r => new Point(r.Right, r.Top + (r.Height / 2)),
                  r => new Point(r.Left, r.Top + (r.Height / 2))
                );

            case Direction.Up:
                // from c1's top center -> c2's bottom center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left + (r.Width / 2), r.Top),
                  r => new Point(r.Left + (r.Width / 2), r.Bottom)
                );

            case Direction.Down:
                // from c1's bottom center -> c2's top center
                return Measure(
                  r1, r2,
                  r => new Point(r.Left + (r.Width / 2), r.Bottom),
                  r => new Point(r.Left + (r.Width / 2), r.Top)
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
        List<FrameworkElement> childs = [];
        Stack<DependencyObject> nodes = new();
        nodes.Push(startNode);

        while (nodes.Count > 0)
        {
            DependencyObject current = nodes.Pop();
            int count = VisualTreeHelper.GetChildrenCount(current);

            for (int i = count - 1; i >= 0; i--)
            {
                DependencyObject child = VisualTreeHelper.GetChild(current, i);
                nodes.Push(child);

                if (child is TextBox textBox)
                {
                    // skip if read only
                    if (textBox.IsReadOnly)
                        continue;

                    if (CanTarget(textBox))
                        childs.Add(textBox);

                    continue;
                }

                if (child is RepeatButton repeatButton)
                {
                    // skip if repeat button is part of scrollbar
                    if (repeatButton.Name.StartsWith("PART_"))
                        continue;

                    if (CanTarget(repeatButton))
                        childs.Add(repeatButton);

                    continue;
                }

                if (child is Button button)
                {
                    if (button.Name.Equals("NavigationViewBackButton"))
                        continue;
                    else if (button.Name.Equals("TogglePaneButton"))
                        continue;
                    else if (CanTarget(button))
                        childs.Add(button);

                    continue;
                }

                if (child is SettingsCard settingsCard)
                {
                    // skip if not clickable
                    if (!settingsCard.IsClickEnabled)
                        continue;

                    if (CanTarget(settingsCard))
                        childs.Add(settingsCard);

                    continue;
                }

                if (child is Slider
                    or ToggleSwitch
                    or NavigationViewItem
                    or ComboBox
                    or ComboBoxItem
                    or System.Windows.Controls.ListView
                    or System.Windows.Controls.ListViewItem
                    or AppBarButton
                    or ToggleButton
                    or CheckBox
                    or RadioButton
                    or ContentDialog
                    or HyperlinkButton
                    or DropDownButton
                    or MenuItem)
                {
                    if (child is FrameworkElement asType && CanTarget(asType))
                        childs.Add(asType);
                }
            }
        }

        return childs;
    }

    public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        T? parent = null;
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

    public static List<MenuItem> GetDirectMenuItems(MenuFlyout menuFlyout)
    {
        List<MenuItem> menuItems = new();

        foreach (object item in menuFlyout.Items)
            if (item is MenuItem menuItem)
                menuItems.Add(menuItem);

        return menuItems;
    }

    public static List<MenuItem> GetDirectMenuItems(ItemsControl? itemsControl)
    {
        List<MenuItem> menuItems = new();

        if (itemsControl is null)
            return menuItems;

        foreach (object item in itemsControl.Items)
        {
            if (item is MenuItem menuItem)
                menuItems.Add(menuItem);
            else if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is MenuItem container)
                menuItems.Add(container);
        }

        return menuItems;
    }

    public static List<MenuItem> GetSiblingMenuItems(MenuItem menuItem)
    {
        return GetDirectMenuItems(ItemsControl.ItemsControlFromItemContainer(menuItem));
    }

    public static MenuItem? GetParentMenuItem(MenuItem menuItem)
    {
        return ItemsControl.ItemsControlFromItemContainer(menuItem) as MenuItem;
    }

    public static Visual? FindCommonAncestor(Visual visual1, Visual visual2)
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

    public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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

            T? childOfChild = FindVisualChild<T>(child);
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
        => GetElementsByAncestor<T>(elements, p => p is Popup or ContentDialog or Flyout);

    // Returns elements that are children of a Flyout's content.
    // Used when currentFlyout is set to get only the controls inside the flyout.
    public static List<T> GetElementsFromFlyout<T>(FlyoutBase? flyout) where T : FrameworkElement
    {
        List<T> result = [];

        if (flyout is Flyout standardFlyout && standardFlyout.Content is DependencyObject contentRoot)
        {
            // Get all descendants of the flyout's content
            List<FrameworkElement> contentChildren = FindChildren(contentRoot);
            foreach (FrameworkElement child in contentChildren)
            {
                if (child is T typed)
                    result.Add(typed);
            }
        }

        return result;
    }

    // Returns elements whose visual parent chain reaches an AdornerLayer before a
    // Window.  ContentDialog (iNKORE) is hosted in the window's adorner overlay: its
    // C# instance is a logical controller, not a visual node, so this is the only
    // reliable way to find its controls.
    public static List<T> GetElementsFromAdornerLayer<T>(List<FrameworkElement> elements) where T : FrameworkElement
        => GetElementsByAncestor<T>(elements, p => p is AdornerLayer, p => p is Window);

    public static void SendKeyToControl(Control control, int keyCode)
    {
        HwndSource? controlHandle = GetControlHandle(control);
        if (controlHandle is null)
            return;

        SendMessage(controlHandle.Handle.ToInt32(), WM_KEYDOWN, keyCode, IntPtr.Zero);
    }
}