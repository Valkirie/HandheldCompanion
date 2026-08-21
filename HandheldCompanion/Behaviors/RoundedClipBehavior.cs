using System;
using System.Windows;
using System.Windows.Media;

namespace HandheldCompanion.Behaviors;

public static class RoundedClipBehavior
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius",
        typeof(CornerRadius),
        typeof(RoundedClipBehavior),
        new FrameworkPropertyMetadata(new CornerRadius(), OnCornerRadiusChanged));

    public static CornerRadius GetCornerRadius(DependencyObject element)
        => (CornerRadius)element.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(DependencyObject element, CornerRadius value)
        => element.SetValue(CornerRadiusProperty, value);

    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        element.SizeChanged -= Element_SizeChanged;
        element.SizeChanged += Element_SizeChanged;
        UpdateClip(element);
    }

    private static void Element_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateClip((FrameworkElement)sender);
    }

    private static void UpdateClip(FrameworkElement element)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            element.Clip = null;
            return;
        }

        CornerRadius radius = GetCornerRadius(element);
        double radiusX = Math.Min(radius.TopLeft, Math.Min(element.ActualWidth, element.ActualHeight) / 2);
        double radiusY = Math.Min(radius.TopLeft, Math.Min(element.ActualWidth, element.ActualHeight) / 2);

        element.Clip = new RectangleGeometry(
            new Rect(0, 0, element.ActualWidth, element.ActualHeight),
            radiusX,
            radiusY);
    }
}
