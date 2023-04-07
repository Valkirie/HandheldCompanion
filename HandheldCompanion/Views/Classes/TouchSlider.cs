using System;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HandheldCompanion.Views.Classes
{
    public partial class TouchSlider : Slider
    {
        protected Timer Timer { get; set; } = new(50);
        protected TouchPoint TouchPoint { get; set; }

        public TouchSlider()
        {
            Timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            double d = 1.0 / ActualWidth * TouchPoint.Position.X * Maximum;

            if (IsSnapToTickEnabled)
                d = RoundToTick(d, TickFrequency);

            // set value
            this.Value = d;
        }

        protected override void OnTouchMove(TouchEventArgs e)
        {
            Timer.Stop();
            Timer.Start();

            TouchPoint = e.GetTouchPoint(this);
            e.Handled = true;
        }

        private static double RoundToTick(double num, double multipleOf)
        {
            return Math.Floor((num + multipleOf / 2) / multipleOf) * multipleOf;
        }
    }
}
