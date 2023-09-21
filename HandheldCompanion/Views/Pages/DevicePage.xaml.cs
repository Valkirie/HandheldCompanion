using ColorPicker.Models;
using ColorPicker;
using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Windows;
using Page = System.Windows.Controls.Page;
using System.Windows.Media;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for DevicePage.xaml
    /// </summary>
    public partial class DevicePage : Page
    {
        public DevicePage()
        {
            InitializeComponent();

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // Todo, not sure if this is the right way to do it?
            LEDPanel.Visibility = MainWindow.CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.LEDControl) ? Visibility.Visible : Visibility.Collapsed;
        }

        public DevicePage(string? Tag) : this()
        {
            this.Tag = Tag;
        }

        private void Page_Loaded(object? sender, RoutedEventArgs? e)
        {
        }

        public void Page_Closed()
        {
        }

        private void SettingsManager_SettingValueChanged(string? name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "ConfigurableTDPOverride":
                        Toggle_cTDP.IsOn = Convert.ToBoolean(value);
                        break;
                    case "ConfigurableTDPOverrideDown":
                        NumberBox_TDPMin.Value = Convert.ToDouble(value);
                        break;
                    case "ConfigurableTDPOverrideUp":
                        NumberBox_TDPMax.Value = Convert.ToDouble(value);
                        break;
                    case "LED":
                        Toggle_LED.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LEDBrightness":
                        SliderLEDBrightness.Value = Convert.ToDouble(value);
                        break;
                    case "LEDColor":
                        ColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(value));
                        break;
                }
            });
        }

        private async void Toggle_cTDP_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            if (Toggle_cTDP.IsOn)
            {
                // todo: localize me !
                var result = Dialog.ShowAsync(
                    "Warning",
                    "Altering minimum and maximum CPU power values might cause instabilities. Product warranties may not apply if the processor is operated beyond its specifications. Use at your own risk.",
                    ContentDialogButton.Primary, "Cancel", Properties.Resources.ProfilesPage_OK);

                await result; // sync call

                switch (result.Result)
                {
                    case ContentDialogResult.Primary:
                        break;
                    default:
                    case ContentDialogResult.None:
                        // restore previous state
                        Toggle_cTDP.IsOn = false;
                        return;
                }
            }

            SettingsManager.SetProperty("ConfigurableTDPOverride", Toggle_cTDP.IsOn);
            SettingsManager.SetProperty("ConfigurableTDPOverrideUp", NumberBox_TDPMax.Value);
            SettingsManager.SetProperty("ConfigurableTDPOverrideDown", NumberBox_TDPMin.Value);
        }

        private void NumberBox_TDPMax_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_TDPMax.Value;
            if (double.IsNaN(value))
                return;

            NumberBox_TDPMin.Maximum = value;

            if (!IsLoaded)
                return;

            // update current device cTDP
            MainWindow.CurrentDevice.cTDP[1] = value;

            SettingsManager.SetProperty("ConfigurableTDPOverrideUp", value);
        }

        private void NumberBox_TDPMin_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_TDPMin.Value;
            if (double.IsNaN(value))
                return;

            NumberBox_TDPMax.Minimum = value;

            if (!IsLoaded)
                return;

            // update current device cTDP
            MainWindow.CurrentDevice.cTDP[0] = value;

            SettingsManager.SetProperty("ConfigurableTDPOverrideDown", value);
        }

        private void Toggle_LED_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LED", Toggle_LED.IsOn, false, true);
        }

        private Color prevSelectedColor = new();
        private void StandardColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            // workaround: NotifyableColor is raising ColorChanged event infinitely
            ColorRoutedEventArgs colorArgs = (ColorRoutedEventArgs)e;
            if (prevSelectedColor == colorArgs.Color)
            {
                ColorPicker.Color = new NotifyableColor(new PickerControlBase());
                return;
            }
            prevSelectedColor = colorArgs.Color;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDColor", ColorPicker.SelectedColor);
        }

        private void SliderLEDBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLEDBrightness.Value;
            if (double.IsNaN(value))
                return;

            SliderLEDBrightness.Value = value;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDBrightness", value);
        }
    }
}
