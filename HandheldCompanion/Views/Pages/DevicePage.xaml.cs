using ColorPicker;
using ColorPicker.Models;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using iNKORE.UI.WPF.Modern.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.UI.ViewManagement;
using static HandheldCompanion.Utils.DeviceUtils;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for DevicePage.xaml
    /// </summary>
    public partial class DevicePage : Page
    {
        private Color prevMainColor = new();
        private Color prevSecondColor = new();

        public DevicePage()
        {
            InitializeComponent();

            // call function
            UpdateDevice();

            // Adjust UI element availability based on device capabilities
            DynamicLightingPanel.IsEnabled = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.DynamicLighting);
            LEDBrightness.Visibility = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.DynamicLightingBrightness) ? Visibility.Visible : Visibility.Collapsed;
            StackSecondColor.Visibility = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor) ? Visibility.Visible : Visibility.Collapsed;

            SetControlEnabledAndVisible(LEDSolidColor, LEDLevel.SolidColor);
            SetControlEnabledAndVisible(LEDBreathing, LEDLevel.Breathing);
            SetControlEnabledAndVisible(LEDRainbow, LEDLevel.Rainbow);
            SetControlEnabledAndVisible(LEDWave, LEDLevel.Wave);
            SetControlEnabledAndVisible(LEDWheel, LEDLevel.Wheel);
            SetControlEnabledAndVisible(LEDGradient, LEDLevel.Gradient);
            SetControlEnabledAndVisible(LEDAmbilight, LEDLevel.Ambilight);
            
        }

        public DevicePage(string? Tag) : this()
        {
            this.Tag = Tag;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            MainWindow.uiSettings.ColorValuesChanged += OnColorValuesChanged;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            DeviceManager.UsbDeviceArrived += GenericDeviceUpdated;
            DeviceManager.UsbDeviceRemoved += GenericDeviceUpdated;
        }

        private void GenericDeviceUpdated(PnPDevice device, DeviceEventArgs obj)
        {
            UpdateDevice(device);
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                SensorController.IsEnabled = Controller.Capabilities.HasFlag(ControllerCapabilities.MotionSensor);
            });
        }

        private void Page_Loaded(object? sender, RoutedEventArgs? e)
        {
            if (IDevice.GetCurrent() is LegionGo)
            {
                LegionGoPanel.Visibility = Visibility.Visible;

                // Left joycon settings
                SliderLeftJoystickDeadzone.Value = SapientiaUsb.GetStickCustomDeadzone(LegionGo.LeftJoyconIndex) + 1;
                SliderLeftAutoSleepTime.Value = SapientiaUsb.GetAutoSleepTime(LegionGo.LeftJoyconIndex);

                SapientiaUsb.LegionTriggerDeadzone legionGoLeftTrigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGo.LeftJoyconIndex);
                SliderLeftTriggerDeadzone.Value = legionGoLeftTrigger.Deadzone + 1;
                SliderLeftTriggerMargin.Value = legionGoLeftTrigger.Margin + 1;

                // Right joycon settings
                SliderRightJoystickDeadzone.Value = SapientiaUsb.GetStickCustomDeadzone(LegionGo.RightJoyconIndex) + 1;
                SliderRightAutoSleepTime.Value = SapientiaUsb.GetAutoSleepTime(LegionGo.RightJoyconIndex);

                SapientiaUsb.LegionTriggerDeadzone legionGoRightTrigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGo.RightJoyconIndex);
                SliderRightTriggerDeadzone.Value = legionGoRightTrigger.Deadzone + 1;
                SliderRightTriggerMargin.Value = legionGoRightTrigger.Margin + 1;
            }
        }

        public void Page_Closed()
        { }

        private void SettingsManager_SettingValueChanged(string? name, object value)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
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
                    case "LEDSettingsEnabled":
                        UseDynamicLightingToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LEDSettingsUseAccentColor":
                        MatchAccentColor.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LEDSettingsLevel":
                        {
                            foreach (Control control in LEDSettingsLevel.Items)
                            {
                                if (control is not ComboBoxItem)
                                    continue;

                                ComboBoxItem comboBoxItem = (ComboBoxItem)control;
                                if (Convert.ToInt32(comboBoxItem.Tag) == Convert.ToInt32(value))
                                {
                                    LEDSettingsLevel.SelectedItem = comboBoxItem;
                                    break;
                                }
                            }
                        }
                        break;
                    case "LEDBrightness":
                        SliderLEDBrightness.Value = Convert.ToDouble(value);
                        break;
                    case "LEDSpeed":
                        SliderLEDSpeed.Value = Convert.ToDouble(value);
                        break;
                    case "LEDDirection":
                        LEDDirection.SelectedIndex = Convert.ToInt32(value);
                        break;
                    case "LEDMainColor":
                        MainColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(value));
                        break;
                    case "LEDSecondColor":
                        SecondColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(value));
                        break;
                    case "LEDAmbilightVerticalBlackBarDetection":
                        Toggle_AmbilightVerticalBlackBarDetection.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LEDUseSecondColor":
                        Toggle_UseSecondColor.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LegionControllerPassthrough":
                        Toggle_TouchpadPassthrough.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LegionControllerGyroIndex":
                        ComboBox_GyroController.SelectedIndex = Convert.ToInt32(value);
                        break;
                    case "SensorSelection":
                        {
                            int idx = Convert.ToInt32(value);

                            // default value
                            if (idx == -1)
                            {
                                if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor))
                                {
                                    SettingsManager.SetProperty(name, cB_SensorSelection.Items.IndexOf(SensorInternal));
                                }
                                else if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor))
                                {
                                    SettingsManager.SetProperty(name, cB_SensorSelection.Items.IndexOf(SensorExternal));
                                }
                                else
                                {
                                    SettingsManager.SetProperty(name, cB_SensorSelection.Items.IndexOf(SensorNone));
                                }

                                return;
                            }

                            cB_SensorSelection.SelectedIndex = idx;

                            // bug: SelectionChanged not triggered when control isn't loaded
                            if (!IsLoaded)
                                cB_SensorSelection_SelectionChanged(this, null);
                        }
                        break;
                    case "SensorPlacement":
                        UpdateUI_SensorPlacement(Convert.ToInt32(value));
                        break;
                    case "SensorPlacementUpsideDown":
                        Toggle_SensorPlacementUpsideDown.IsOn = Convert.ToBoolean(value);
                        break;
                }
            });
        }

        public void UpdateDevice(PnPDevice device = null)
        {
            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                SensorInternal.IsEnabled = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor);
                SensorExternal.IsEnabled = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor);
            });
        }

        private void OnColorValuesChanged(UISettings sender, object args)
        {
            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MatchAccentColor.IsOn)
                    SetAccentColor();
            });
        }

        private async void Toggle_cTDP_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            if (Toggle_cTDP.IsOn)
            {
                // todo: translate me
                Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                {
                    Title = "Warning",
                    Content = "Altering minimum and maximum CPU power values might cause instabilities. Product warranties may not apply if the processor is operated beyond its specifications. Use at your own risk.",
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_OK
                }.ShowAsync();

                await dialogTask; // sync call

                switch (dialogTask.Result)
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
            IDevice.GetCurrent().cTDP[1] = value;

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
            IDevice.GetCurrent().cTDP[0] = value;

            SettingsManager.SetProperty("ConfigurableTDPOverrideDown", value);
        }

        private void UseDynamicLightingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDSettingsEnabled", UseDynamicLightingToggle.IsOn);
        }

        private void MatchAccentColor_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            MainColorPicker.IsEnabled = !MatchAccentColor.IsOn;
            SecondColorPicker.IsEnabled = !MatchAccentColor.IsOn;

            if (MatchAccentColor.IsOn)
                SetAccentColor();

            SettingsManager.SetProperty("LEDSettingsUseAccentColor", MatchAccentColor.IsOn);
        }

        private void SetAccentColor()
        {
            MainColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(MainWindow.uiSettings.GetColorValue(UIColorType.Accent).ToString()));
            SecondColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(MainWindow.uiSettings.GetColorValue(UIColorType.Accent).ToString()));

            SettingsManager.SetProperty("LEDMainColor", MainColorPicker.SelectedColor);
            SettingsManager.SetProperty("LEDSecondColor", MainColorPicker.SelectedColor);
        }

        private void LEDSettingsLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ComboBoxItem comboBoxItem = (ComboBoxItem)LEDSettingsLevel.SelectedItem;
            int level = Convert.ToInt32(comboBoxItem.Tag);

            SettingsManager.SetProperty("LEDSettingsLevel", level);
        }

        private void MainColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            // workaround: NotifyableColor is raising ColorChanged event infinitely
            ColorRoutedEventArgs colorArgs = (ColorRoutedEventArgs)e;
            if (prevMainColor == colorArgs.Color)
            {
                MainColorPicker.Color = new NotifyableColor(new PickerControlBase());
                return;
            }
            prevMainColor = colorArgs.Color;

            if (!IsLoaded)
                return;
            
            SettingsManager.SetProperty("LEDMainColor", prevMainColor.ToString());
        }
        
        private void SecondColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            // workaround: NotifyableColor is raising ColorChanged event infinitely
            ColorRoutedEventArgs colorArgs = (ColorRoutedEventArgs)e;
            if (prevSecondColor == colorArgs.Color)
            {
                SecondColorPicker.Color = new NotifyableColor(new PickerControlBase());
                return;
            }
            prevSecondColor = colorArgs.Color;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDSecondColor", prevSecondColor.ToString());
        }

        private void SliderLEDBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLEDBrightness.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDBrightness", value);
        }

        private async void Toggle_AmbilightVerticalBlackBarDetection_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDAmbilightVerticalBlackBarDetection", Toggle_AmbilightVerticalBlackBarDetection.IsOn);
        }

        private async void Toggle_UseSecondColor_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDUseSecondColor", Toggle_UseSecondColor.IsOn);
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void SliderLEDSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLEDSpeed.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDSpeed", value);
        }

        private void LEDDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LEDDirection", LEDDirection.SelectedIndex);
        }

        private void SetControlEnabledAndVisible(UIElement control, LEDLevel level)
        {
            bool isCapabilitySupported = IDevice.GetCurrent().DynamicLightingCapabilities.HasFlag(level);
            control.IsEnabled = isCapabilitySupported;
            control.Visibility = isCapabilitySupported ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Sensor
        private void cB_SensorSelection_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (cB_SensorSelection.SelectedIndex == -1)
                return;

            // update dependencies
            SensorFamily sensorFamily = (SensorFamily)cB_SensorSelection.SelectedIndex;

            Toggle_SensorPlacementUpsideDown.IsEnabled = sensorFamily == SensorFamily.SerialUSBIMU;
            Grid_SensorPlacementVisualisation.IsEnabled = sensorFamily == SensorFamily.SerialUSBIMU;
            ui_button_calibrate.IsEnabled = sensorFamily != SensorFamily.None;

            if (IsLoaded)
                SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.SelectedIndex);
        }

        private void ui_button_calibrate_Click(object sender, RoutedEventArgs e)
        {
            // update dependencies
            SensorFamily sensorFamily = (SensorFamily)cB_SensorSelection.SelectedIndex;

            switch(sensorFamily)
            {
                case SensorFamily.Windows:
                case SensorFamily.SerialUSBIMU:
                    SensorsManager.Calibrate(IDevice.GetCurrent().GamepadMotion);
                    break;

                case SensorFamily.Controller:
                    IController controller = ControllerManager.GetTargetController();
                    controller?.Calibrate();
                    break;
            }
        }

        private void SensorPlacement_Click(object sender, RoutedEventArgs? e)
        {
            var Tag = int.Parse((string)((Button)sender).Tag);

            UpdateUI_SensorPlacement(Tag);

            if (IsLoaded)
                SettingsManager.SetProperty("SensorPlacement", Tag);
        }

        private void UpdateUI_SensorPlacement(int? SensorPlacement)
        {
            foreach (SimpleStackPanel panel in Grid_SensorPlacementVisualisation.Children)
                foreach (Button button in panel.Children)
                    if (int.Parse((string)button.Tag) == SensorPlacement)
                        button.Background = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
                    else
                        button.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltBaseLowBrush"];
        }

        private void Toggle_SensorPlacementUpsideDown_Toggled(object? sender, RoutedEventArgs? e)
        {
            var isUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

            if (IsLoaded)
                SettingsManager.SetProperty("SensorPlacementUpsideDown", isUpsideDown);
        }
        #endregion

        #region Legion Go Device Settings

        private void Toggle_TouchpadPassthrough_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LegionControllerPassthrough", Toggle_TouchpadPassthrough.IsOn);
        }

        private void ComboBox_GyroController_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LegionControllerGyroIndex", ComboBox_GyroController.SelectedIndex);
        }

        private void SliderLeftJoystickDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftJoystickDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetStickCustomDeadzone(LegionGo.LeftJoyconIndex, (int) value - 1);
        }

        private void SliderLeftAutoSleepTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftAutoSleepTime.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetAutoSleepTime(LegionGo.LeftJoyconIndex, (int)value);
        }

        private void SliderLeftTriggerDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftTriggerDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGo.LeftJoyconIndex);
            trigger.Deadzone = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGo.LeftJoyconIndex, trigger);
        }

        private void SliderLeftTriggerMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftTriggerMargin.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGo.LeftJoyconIndex);
            trigger.Margin = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGo.LeftJoyconIndex, trigger);
        }

        private void SliderRightJoystickDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightJoystickDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetStickCustomDeadzone(LegionGo.RightJoyconIndex, (int)value - 1);
        }

        private void SliderRightAutoSleepTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightAutoSleepTime.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetAutoSleepTime(LegionGo.RightJoyconIndex, (int)value);
        }

        private void SliderRightTriggerDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightTriggerDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGo.RightJoyconIndex);
            trigger.Deadzone = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGo.RightJoyconIndex, trigger);
        }

        private void SliderRightTriggerMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightTriggerMargin.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGo.RightJoyconIndex);
            trigger.Margin = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGo.RightJoyconIndex, trigger);
        }

        #endregion
    }
}