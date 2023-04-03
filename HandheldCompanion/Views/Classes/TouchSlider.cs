using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HandheldCompanion.Views.Classes
{
    public partial class TouchSlider : Slider
    {
        protected override void OnTouchDown(TouchEventArgs e)
        {
            base.OnTouchDown(e);
            return;
        }

        protected override void OnTouchMove(TouchEventArgs e)
        {
            TouchPoint point = e.GetTouchPoint(this);
            double d = 1.0 / ActualWidth * point.Position.X;
            double p = Maximum * d;

            if (IsSnapToTickEnabled)
                p = RoundToTick(p, TickFrequency);

            Value = p;

            e.Handled = true;
        }

        private static Thumb GetThumb(Slider slider)
        {
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            return track == null ? null : track.Thumb;
        }

        private static double RoundToTick(double Number, double MultipleOf)
        {           
            // Determine amount of digits to round to based on tick frequency
            // Convert to string, determine length of string after decimal sign, if there's no decimal sign, round to 0 decimals.
            string TickFrequencyAsString = MultipleOf.ToString();
            int Decimals;

            int IndexPos = TickFrequencyAsString.IndexOf(".");

            if (IndexPos == -1 || IndexPos == 0)
                Decimals = 0;
            else
                Decimals = TickFrequencyAsString.Substring(IndexPos + 1).Length;

            return Math.Round(Number, Decimals);

        }
    }
}
