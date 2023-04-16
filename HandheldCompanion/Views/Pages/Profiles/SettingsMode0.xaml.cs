using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerCommon.Pipes;
using ControllerService.Sensors;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages.Profiles
{
    /// <summary>
    /// Interaction logic for SettingsMode0.xaml
    /// </summary>
    public partial class SettingsMode0 : Page
    {
        private Hotkey ProfilesPageHotkey;

        public SettingsMode0()
        {
            InitializeComponent();
        }

        public SettingsMode0(string Tag) : this()
        {
            this.Tag = Tag;

            PipeClient.ServerMessage += OnServerMessage;

            HotkeysManager.HotkeyCreated += TriggerCreated;
            InputsManager.TriggerUpdated += TriggerUpdated;
        }

        public void SetProfile()
        {
            SliderSensitivityX.Value = ProfilesPage.currentProfile.MotionSensivityX;
            SliderSensitivityY.Value = ProfilesPage.currentProfile.MotionSensivityY;
            tb_ProfileAimingDownSightsMultiplier.Value = ProfilesPage.currentProfile.AimingSightsMultiplier;
            Toggle_FlickStick.IsOn = ProfilesPage.currentProfile.FlickstickEnabled;
            tb_ProfileFlickDuration.Value = ProfilesPage.currentProfile.FlickstickDuration * 1000;
            tb_ProfileStickSensitivity.Value = ProfilesPage.currentProfile.FlickstickSensivity;

            // todo: improve me ?
            ProfilesPageHotkey.inputsChord.State = ProfilesPage.currentProfile.AimingSightsTrigger.Clone() as ButtonState;
            ProfilesPageHotkey.DrawInput();

            // temp
            StackCurve.Children.Clear();
            foreach (var elem in ProfilesPage.currentProfile.MotionSensivityArray)
            {
                // skip first item ?
                if (elem.Key == 0)
                    continue;

                double height = elem.Value * StackCurve.Height;
                Thumb thumb = new Thumb()
                {
                    Tag = elem.Key,
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
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
        }

        private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
        }

        private void Highlight_Thumb(float value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                double dist_x = value / IMUGyrometer.sensorSpec.maxIn;

                foreach (Control control in StackCurve.Children)
                {
                    double x = (double)control.Tag;

                    if (dist_x > x)
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
            if (ProfilesPage.currentProfile is null)
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

                double x = (double)Thumb.Tag;
                Thumb.Height = StackCurve.ActualHeight - e.GetPosition(StackCurve).Y;
                ProfilesPage.currentProfile.MotionSensivityArray[x] = Thumb.Height / StackCurve.Height;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // default preset
            foreach (Control Thumb in StackCurve.Children)
            {
                double x = (double)Thumb.Tag;
                Thumb.Height = StackCurve.Height / 2.0f;
                ProfilesPage.currentProfile.MotionSensivityArray[x] = Thumb.Height / StackCurve.Height;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // agressive preset
            float tempx = 24f / Profile.SensivityArraySize;
            foreach (Control Thumb in StackCurve.Children)
            {
                double x = (double)Thumb.Tag;
                float value = (float)(-Math.Sqrt(x * tempx) + 0.85f);

                Thumb.Height = StackCurve.Height * value;
                ProfilesPage.currentProfile.MotionSensivityArray[x] = Thumb.Height / StackCurve.Height;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // precise preset
            float tempx = 12f / Profile.SensivityArraySize;
            foreach (Control Thumb in StackCurve.Children)
            {
                double x = (double)Thumb.Tag;
                float value = (float)(Math.Sqrt(x * tempx) + 0.25f - (tempx * x));

                Thumb.Height = StackCurve.Height * value;
                ProfilesPage.currentProfile.MotionSensivityArray[x] = Thumb.Height / StackCurve.Height;
            }
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void SliderAimingDownSightsMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.AimingSightsMultiplier = (float)tb_ProfileAimingDownSightsMultiplier.Value;
        }

        private void Toggle_FlickStick_Toggled(object sender, RoutedEventArgs e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.FlickstickEnabled = (bool)Toggle_FlickStick.IsOn;
        }

        private void SliderFlickDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.FlickstickDuration = (float)tb_ProfileFlickDuration.Value / 1000;
        }

        private void SliderStickSensivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.FlickstickSensivity = (float)tb_ProfileStickSensitivity.Value;
        }

        private void TriggerCreated(Hotkey hotkey)
        {
            switch (hotkey.inputsHotkey.Listener)
            {
                case "shortcutProfilesSettingsMode0":
                    {
                        // pull hotkey
                        ProfilesPageHotkey = hotkey;

                        // add to UI
                        HotkeyControl hotkeyBorder = ProfilesPageHotkey.GetControl();
                        if (hotkeyBorder is null || hotkeyBorder.Parent is not null)
                            return;

                        if (UMC_Activator.Children.Count == 0)
                            UMC_Activator.Children.Add(hotkeyBorder);
                    }
                    break;
            }
        }

        private void TriggerUpdated(string listener, InputsChord inputs, InputsManager.ListenerType type)
        {
            switch (listener)
            {
                case "shortcutProfilesSettingsMode0":
                    ProfilesPage.currentProfile.AimingSightsTrigger = inputs.State.Clone() as ButtonState;
                    break;
            }
        }
    }
}
