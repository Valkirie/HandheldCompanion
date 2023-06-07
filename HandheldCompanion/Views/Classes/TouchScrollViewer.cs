using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.Views.Classes;

public class TouchScrollViewer : ScrollViewer
{
    private DependencyObject ancestor;

    protected override void OnManipulationStarted(ManipulationStartedEventArgs e)
    {
        // Retrieve the coordinate of the mouse position.
        var pt = e.ManipulationOrigin;

        // Perform the hit test against a given portion of the visual object tree.
        var result = VisualTreeHelper.HitTest(this, pt);

        if (result is null)
            return;

        var hit = result.VisualHit;

        while (hit is not null && hit != ancestor)
        {
            ancestor = hit;

            var mode = (PanningMode)hit.GetValue(PanningModeProperty);

            if (mode == PanningMode.HorizontalOnly)
            {
                PanningMode = PanningMode.None;
                return;
            }

            hit = VisualTreeHelper.GetParent(hit);
        }

        base.OnManipulationStarted(e);
    }

    protected override void OnManipulationCompleted(ManipulationCompletedEventArgs e)
    {
        PanningMode = PanningMode.VerticalOnly;
        base.OnManipulationCompleted(e);
    }
}