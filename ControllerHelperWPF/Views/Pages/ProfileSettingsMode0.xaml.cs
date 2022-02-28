using ControllerCommon;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ControllerHelperWPF.Views.Pages
{
    /// <summary>
    /// Interaction logic for ProfileSettingsMode0.xaml
    /// </summary>
    public partial class ProfileSettingsMode0 : Page
    {
        private Profile profileCurrent;

        public ProfileSettingsMode0()
        {
            InitializeComponent();
        }

        public ProfileSettingsMode0(Profile profileCurrent) : this()
        {
            this.profileCurrent = profileCurrent;

            SliderSensivity.Value = profileCurrent.aiming_sensivity;
            SliderIntensity.Value = profileCurrent.aiming_intensity;

            // temp
            StackCurve.Children.Clear();
            for (int i = 1; i <= Profile.array_size; i++)
            {
                // skip first item ?
                if (i == 1)
                    continue;

                double height = profileCurrent.aiming_array[i - 1].y * StackCurve.Height;
                Thumb thumb = new Thumb()
                {
                    Tag = i - 1,
                    Width = 10,
                    MaxHeight = StackCurve.Height,
                    Height = height,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentLowBrush"],
                    IsEnabled = false // prevent the control from being clickable
                };

                StackCurve.Children.Add(thumb);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void SliderSensivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.aiming_sensivity = (float)SliderSensivity.Value;
        }

        private void SliderIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.aiming_intensity = (float)SliderIntensity.Value;
        }

        private void StackCurve_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = true;

            if (profileCurrent is null)
                return;

            Control Thumb = null;

            double min_x = StackCurve.ActualWidth;
            double dist_y = StackCurve.ActualHeight - e.GetPosition(StackCurve).Y;

            foreach (Control control in StackCurve.Children)
            {
                Point position = e.GetPosition(control);
                double dist_x = Math.Abs(position.X);

                control.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentLowBrush"];

                if (dist_x < min_x)
                {
                    Thumb = control;
                    min_x = dist_x;
                }
            }

            if (Thumb is null)
                return;

            Thumb.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentHighBrush"];

            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {

                int idx = (int)Thumb.Tag;
                Thumb.Height = dist_y;
                profileCurrent.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }

            e.Handled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // default preset
            foreach (Control Thumb in StackCurve.Children)
            {
                int idx = (int)Thumb.Tag;

                Thumb.Height = StackCurve.Height / 2.0f;
                profileCurrent.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // agressive preset
            float tempx = 0.50f / Profile.array_size;
            foreach (Control Thumb in StackCurve.Children)
            {
                int idx = (int)Thumb.Tag;
                float value = (float)(-Math.Sqrt(idx * tempx) + 0.85f);

                Thumb.Height = StackCurve.Height * value;
                profileCurrent.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // precise preset
            float tempx = 0.25f / Profile.array_size;
            foreach (Control Thumb in StackCurve.Children)
            {
                int idx = (int)Thumb.Tag;
                float value = (float)(Math.Sqrt(idx * tempx) + 0.25f - (tempx * idx));

                Thumb.Height = StackCurve.Height * value;
                profileCurrent.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }
        }

        private void StackCurve_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = false;
        }

        private void StackCurve_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = true;
        }
    }
}
