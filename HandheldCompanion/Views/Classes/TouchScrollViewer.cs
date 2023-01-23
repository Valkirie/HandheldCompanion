using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.Views.Classes
{
    public class TouchScrollViewer : ScrollViewer
    {
        DependencyObject ancestor;

        protected override void OnManipulationStarted(ManipulationStartedEventArgs e)
        {
            // Retrieve the coordinate of the mouse position.
            Point pt = e.ManipulationOrigin;

            // Perform the hit test against a given portion of the visual object tree.
            HitTestResult result = VisualTreeHelper.HitTest(this, pt);

            if (result is null)
                return;

            DependencyObject hit = result.VisualHit;

            while (hit is not null && hit != ancestor)
            {
                ancestor = hit;

                PanningMode mode = (PanningMode)hit.GetValue(PanningModeProperty);

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
}
