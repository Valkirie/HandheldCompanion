using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HandheldCompanion.Views.Classes
{
    public partial class TouchSlider : Slider
    {
        protected override void OnTouchMove(TouchEventArgs e)
        {
            TouchPoint point = e.GetTouchPoint(this);
            double d = 1.0 / ActualWidth * point.Position.X;
            int p = Convert.ToInt32(Maximum * d);

            if (IsSnapToTickEnabled)
                p = RoundToTick(p, TickFrequency);

            Value = p;

            base.OnTouchMove(e);
        }

        private static Thumb GetThumb(Slider slider)
        {
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            return track == null ? null : track.Thumb;
        }

        private static int RoundToTick(double value, double nearest)
        {
            return Convert.ToInt32(Math.Round(value / nearest) * nearest);
        }
    }
}
