using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.Common
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
            DependencyObject hit = result.VisualHit;

            while (hit != null && hit != ancestor)
            {
                this.ancestor = hit;

                PanningMode mode = (PanningMode)hit.GetValue(ScrollViewer.PanningModeProperty);

                if (mode == PanningMode.HorizontalOnly)
                {
                    this.PanningMode = PanningMode.None;
                    return;
                }

                hit = VisualTreeHelper.GetParent(hit);
            }

            base.OnManipulationStarted(e);
        }

        protected override void OnManipulationCompleted(ManipulationCompletedEventArgs e)
        {
            this.PanningMode = PanningMode.VerticalOnly;
            base.OnManipulationCompleted(e);
        }
    }
}
