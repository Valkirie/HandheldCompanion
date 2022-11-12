using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for ProfileSettingsMode0.xaml
    /// </summary>
    public partial class ProfileSettingsMode0 : Page
    {
        private Profile profileCurrent;

        private Dictionary<ControllerButtonFlags, CheckBox> AimingDownSightsActivators = new();

        public ProfileSettingsMode0()
        {
            InitializeComponent();
        }

        public ProfileSettingsMode0(string Tag, Profile profileCurrent) : this()
        {
            this.Tag = Tag;

            this.profileCurrent = profileCurrent;
            PipeClient.ServerMessage += OnServerMessage;

            SliderSensivity.Value = profileCurrent.aiming_sensivity;
            tb_ProfileAimingDownSightsMultiplier.Value = profileCurrent.aiming_down_sights_multiplier;
            Toggle_FlickStick.IsOn = profileCurrent.flickstick_enabled;
            tb_ProfileFlickDuration.Value = profileCurrent.flick_duration * 1000;
            tb_ProfileStickSensitivity.Value = profileCurrent.stick_sensivity;

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
                    Width = 8,
                    MaxHeight = StackCurve.Height,
                    Height = height,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentLowBrush"],
                    BorderThickness = new Thickness(0),
                    BorderBrush = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentHighBrush"],
                    IsEnabled = false // prevent the control from being clickable
                };

                StackCurve.Children.Add(thumb);
            }

            // draw aiming down sight activators
            foreach (ControllerButtonFlags button in (ControllerButtonFlags[])Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                // create panel
                SimpleStackPanel panel = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // create icon
                FontIcon icon = new FontIcon() { Glyph = "" };
                icon.Glyph = InputUtils.GamepadButtonToGlyph(button);

                if (icon.Glyph != "")
                    panel.Children.Add(icon);

                // create textblock
                string description = EnumUtils.GetDescriptionFromEnumValue(button);
                TextBlock text = new TextBlock() { Text = description };
                panel.Children.Add(text);

                // create checkbox
                CheckBox checkbox = new CheckBox() { Tag = button, Content = panel, Width = 170 };
                checkbox.Checked += AimingDownSightsActivatorsTickedEvent;
                checkbox.Unchecked += AimingDownSightsActivatorsUntickedEvent;

                cB_AimingDownSightsActivationButtons.Children.Add(checkbox);
                AimingDownSightsActivators.Add(button, checkbox);
            }

            // Fill activators based on profile
            foreach (ControllerButtonFlags button in (ControllerButtonFlags[])Enum.GetValues(typeof(ControllerButtonFlags)))
                if (profileCurrent.aiming_down_sights_activation.HasFlag(button))
                    AimingDownSightsActivators[button].IsChecked = true;
                else
                    AimingDownSightsActivators[button].IsChecked = false;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            PipeClient.ServerMessage -= OnServerMessage;
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_SENSOR:
                    PipeSensor sensor = (PipeSensor)message;

                    switch (sensor.type)
                    {
                        case SensorType.Girometer:
                            Highlight_Thumb(Math.Max(Math.Max(Math.Abs(sensor.z), Math.Abs(sensor.x)), Math.Abs(sensor.y)));
                            break;
                    }
                    break;
            }
        }

        private void SliderSensivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.aiming_sensivity = (float)SliderSensivity.Value;
        }

        private void Highlight_Thumb(float value)
        {
            this.Dispatcher.Invoke(() =>
            {
                double dist_x = value / IMUGyrometer.sensorSpec.maxIn;

                foreach (Control control in StackCurve.Children)
                {
                    int idx = (int)control.Tag;
                    ProfileVector vector = profileCurrent.aiming_array[idx];

                    if (dist_x > vector.x)
                        control.BorderThickness = new Thickness(0, 0, 0, 20);
                    else
                        control.BorderThickness = new Thickness(0);
                }
            });
        }

        private void StackCurve_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StackCurve_MouseMove(sender, (MouseEventArgs)e);
        }

        private void StackCurve_MouseMove(object sender, MouseEventArgs e)
        {
            if (profileCurrent is null)
                return;

            Control Thumb = null;

            foreach (Control control in StackCurve.Children)
            {
                Point position = e.GetPosition(control);
                double dist_x = Math.Abs(position.X);

                control.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentLowBrush"];

                if (dist_x <= control.Width)
                    Thumb = control;
            }

            if (Thumb is null)
                return;

            Thumb.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltListAccentHighBrush"];

            if (e.LeftButton == MouseButtonState.Pressed)
            {

                int idx = (int)Thumb.Tag;
                Thumb.Height = StackCurve.ActualHeight - e.GetPosition(StackCurve).Y;
                profileCurrent.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }
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

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void SliderAimingDownSightsMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.aiming_down_sights_multiplier = (float)tb_ProfileAimingDownSightsMultiplier.Value;
        }

        private void AimingDownSightsActivatorsTickedEvent(object sender, RoutedEventArgs e)
        {
            foreach (ControllerButtonFlags button in (ControllerButtonFlags[])Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                if ((bool)AimingDownSightsActivators[button].IsChecked)
                {
                    profileCurrent.aiming_down_sights_activation |= button;
                }
            }
        }

        private void AimingDownSightsActivatorsUntickedEvent(object sender, RoutedEventArgs e)
        {
            foreach (ControllerButtonFlags button in (ControllerButtonFlags[])Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                if (!(bool)AimingDownSightsActivators[button].IsChecked)
                {
                    // Perform a bitwise "AND" mask with every bit set
                    // except the ones already ticked, use complement.
                    profileCurrent.aiming_down_sights_activation &= ~button;
                }
            }
        }

        private void Toggle_FlickStick_Toggled(object sender, RoutedEventArgs e)
        {
            if (profileCurrent == null)
                return;

            profileCurrent.flickstick_enabled = (bool)Toggle_FlickStick.IsOn;
        }

        private void SliderFlickDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.flick_duration = (float)tb_ProfileFlickDuration.Value / 1000;
        }

        private void SliderStickSensivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.stick_sensivity = (float)tb_ProfileStickSensitivity.Value;
        }
    }
}
