using ControllerCommon;
using ControllerCommon.Managers;
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
using HandheldCompanion.Managers;
using System.Linq;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for ProfileSettingsMode0.xaml
    /// </summary>
    public partial class ProfileSettingsMode0 : Page
    {
        private Profile currentProfile;
        private Hotkey ProfilesPageHotkey;

        public ProfileSettingsMode0()
        {
            InitializeComponent();
        }

        public ProfileSettingsMode0(string Tag) : this()
        {
            this.Tag = Tag;

            PipeClient.ServerMessage += OnServerMessage;

            HotkeysManager.HotkeyCreated += TriggerCreated;
            InputsManager.TriggerUpdated += TriggerUpdated;
        }

        public void Update(Profile currentProfile)
        {
            this.currentProfile = currentProfile;

            SliderSensitivityX.Value = currentProfile.aiming_sensitivity_x;
            SliderSensitivityY.Value = currentProfile.aiming_sensitivity_y;
            tb_ProfileAimingDownSightsMultiplier.Value = currentProfile.aiming_down_sights_multiplier;
            Toggle_FlickStick.IsOn = currentProfile.flickstick_enabled;
            tb_ProfileFlickDuration.Value = currentProfile.flick_duration * 1000;
            tb_ProfileStickSensitivity.Value = currentProfile.stick_sensivity;

            // todo: improve me ?
            ProfilesPageHotkey.inputsChord.GamepadButtons = currentProfile.aiming_down_sights_activation;
            ProfilesPageHotkey.Refresh();

            // temp
            StackCurve.Children.Clear();
            for (int i = 1; i <= Profile.array_size; i++)
            {
                // skip first item ?
                if (i == 1)
                    continue;

                double height = currentProfile.aiming_array[i - 1].y * StackCurve.Height;
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

        private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            currentProfile.aiming_sensitivity_x = (float)SliderSensitivityX.Value;
        }

        private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            currentProfile.aiming_sensitivity_y = (float)SliderSensitivityY.Value;
        }

        private void Highlight_Thumb(float value)
        {
            this.Dispatcher.Invoke(() =>
            {
                double dist_x = value / IMUGyrometer.sensorSpec.maxIn;

                foreach (Control control in StackCurve.Children)
                {
                    int idx = (int)control.Tag;
                    ProfileVector vector = currentProfile.aiming_array[idx];

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
            if (currentProfile is null)
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
                currentProfile.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // default preset
            foreach (Control Thumb in StackCurve.Children)
            {
                int idx = (int)Thumb.Tag;

                Thumb.Height = StackCurve.Height / 2.0f;
                currentProfile.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
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
                currentProfile.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
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
                currentProfile.aiming_array[idx].y = Thumb.Height / StackCurve.Height;
            }
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void SliderAimingDownSightsMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            currentProfile.aiming_down_sights_multiplier = (float)tb_ProfileAimingDownSightsMultiplier.Value;
        }

        private void Toggle_FlickStick_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null)
                return;

            currentProfile.flickstick_enabled = (bool)Toggle_FlickStick.IsOn;
        }

        private void SliderFlickDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            currentProfile.flick_duration = (float)tb_ProfileFlickDuration.Value / 1000;
        }

        private void SliderStickSensivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            currentProfile.stick_sensivity = (float)tb_ProfileStickSensitivity.Value;
        }

        private void TriggerCreated(Hotkey hotkey)
        {
            switch (hotkey.hotkeyId)
            {
                case 51:
                    {
                        // pull hotkey
                        ProfilesPageHotkey = hotkey;

                        // add to UI
                        Border hotkeyBorder = ProfilesPageHotkey.GetHotkey();
                        if (hotkeyBorder is null || hotkeyBorder.Parent != null)
                            return;

                        UMC_Activator.Children.Add(hotkeyBorder);
                    }
                    break;
            }
        }

        private void TriggerUpdated(string listener, InputsChord inputs, InputsManager.ListenerType type)
        {
            Hotkey hotkey = HotkeysManager.Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Equals(listener)).FirstOrDefault();

            if (hotkey is null)
                return;

            switch (hotkey.hotkeyId)
            {
                case 51:
                    {
                        // update profile
                        currentProfile.aiming_down_sights_activation = inputs.GamepadButtons;
                    }
                    break;
            }
        }
    }
}
