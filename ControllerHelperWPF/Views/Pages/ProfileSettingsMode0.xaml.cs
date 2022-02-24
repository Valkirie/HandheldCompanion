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
                double height = profileCurrent.aiming_array[i - 1].y * StackCurve.Height;

                Thumb thumb = new Thumb()
                {
                    Tag = i - 1,
                    Width = 40,
                    MaxHeight = StackCurve.Height,
                    Height = height,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentLowBrush"]
                };
                thumb.DragDelta += Thumb_DragDelta;

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

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            //Move the Thumb to the mouse position during the drag operation
            Thumb Thumb = (Thumb)sender;
            int idx = (int)Thumb.Tag;

            Vector offset = VisualTreeHelper.GetOffset(Thumb);
            double pos_y = offset.Y;
            double height = (Thumb.MaxHeight) - e.VerticalChange - pos_y;

            try
            {
                Thumb.Height = height;
                if (profileCurrent != null)
                    profileCurrent.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }
            catch (Exception) { }

            e.Handled = true;
        }
    }
}
