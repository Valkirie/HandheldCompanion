using ColorPicker;
using ColorPicker.Models;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Devices.Zotac;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Controls;
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
            DataContext = new DevicePageViewModel();
            InitializeComponent();

            // call function
            UpdateDevice();

            // get device
            IDevice device = IDevice.GetCurrent();

            // Adjust UI element availability based on device capabilities
            DynamicLightingPanel.Visibility = device.Capabilities.HasFlag(DeviceCapabilities.DynamicLighting) ? Visibility.Visible : Visibility.Collapsed;
            LEDBrightness.Visibility = device.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingBrightness) ? Visibility.Visible : Visibility.Collapsed;
            SecondColorToggleCard.Visibility = SecondColorPickerCard.Visibility = device.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor) ? Visibility.Visible : Visibility.Collapsed;

            if (device is LegionGoTablet)
            {
                // Left joycon settings
                SliderLeftJoystickDeadzone.Value = SapientiaUsb.GetStickCustomDeadzone(LegionGoTablet.LeftJoyconIndex) + 1;
                SliderLeftAutoSleepTime.Value = SapientiaUsb.GetAutoSleepTime(LegionGoTablet.LeftJoyconIndex);

                var leftTrigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex);
                SliderLeftTriggerDeadzone.Value = leftTrigger.Deadzone + 1;
                SliderLeftTriggerMargin.Value = leftTrigger.Margin + 1;

                // Right joycon settings
                SliderRightJoystickDeadzone.Value = SapientiaUsb.GetStickCustomDeadzone(LegionGoTablet.RightJoyconIndex) + 1;
                SliderRightAutoSleepTime.Value = SapientiaUsb.GetAutoSleepTime(LegionGoTablet.RightJoyconIndex);

                var rightTrigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex);
                SliderRightTriggerDeadzone.Value = rightTrigger.Deadzone + 1;
                SliderRightTriggerMargin.Value = rightTrigger.Margin + 1;

                // Show LegionGoPanel
                LegionGoPanel.Visibility = Visibility.Visible;

                if (device.GetType() == typeof(LegionGoTablet) ||
                    device.GetType() == typeof(LegionGoTablet2))
                {
                    LegionGoSensorSelection.Visibility = Visibility.Visible;
                    LegionGoLeftController.Visibility = Visibility.Visible;
                    LegionGoRightController.Visibility = Visibility.Visible;
                }
            }
            else if (device is ClawA2VM || device is ClawA1M)
            {
                // Show MSIClawPanel
                MSIClawPanel.Visibility = Visibility.Visible;
            }
            else if (device is GamingZone)
            {
                ZotacGamingZonePanel.Visibility = Visibility.Visible;
            }

            SetControlEnabledAndVisible(LEDSolidColor, LEDLevel.SolidColor);
            SetControlEnabledAndVisible(LEDBreathing, LEDLevel.Breathing);
            SetControlEnabledAndVisible(LEDRainbow, LEDLevel.Rainbow);
            SetControlEnabledAndVisible(LEDWave, LEDLevel.Wave);
            SetControlEnabledAndVisible(LEDWheel, LEDLevel.Wheel);
            SetControlEnabledAndVisible(LEDGradient, LEDLevel.Gradient);
            SetControlEnabledAndVisible(LEDAmbilight, LEDLevel.Ambilight);
            SetControlEnabledAndVisible(LEDPreset, LEDLevel.LEDPreset);
        }

        public DevicePage(string? Tag) : this()
        {
            this.Tag = Tag;

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            MainWindow.uiSettings.ColorValuesChanged += OnColorValuesChanged;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            ManagerFactory.deviceManager.UsbDeviceArrived += GenericDeviceUpdated;
            ManagerFactory.deviceManager.UsbDeviceRemoved += GenericDeviceUpdated;

            // raise events
            if (ControllerManager.HasTargetController)
                ControllerManager_ControllerSelected(ControllerManager.GetTarget());
        }

        private void GenericDeviceUpdated(PnPDevice device, Guid IntefaceGuid)
        {
            UpdateDevice(device);
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                SensorController.IsEnabled = Controller.Capabilities.HasFlag(ControllerCapabilities.MotionSensor);
            });
        }

        private void Page_Loaded(object? sender, RoutedEventArgs? e)
        {
            if (LedPresetsComboBox.ItemsSource is null)
            {
                // First Time
                LedPresetsComboBox.ItemsSource = IDevice.GetCurrent().LEDPresets;
            }
            else
            {
                // Refresh preset ComboBox when localization changed or re-enter page
                int currentSelected = LedPresetsComboBox.SelectedIndex;
                LedPresetsComboBox.ItemsSource = null;
                LedPresetsComboBox.ItemsSource = IDevice.GetCurrent().LEDPresets;
                LedPresetsComboBox.SelectedIndex = currentSelected;
            }

            // Battery Charge settings
            if (CB_BatteryBypassCharging.ItemsSource is null)
            {
                CB_BatteryBypassCharging.ItemsSource = IDevice.GetCurrent().BatteryBypassPresets;
            }
            else
            {
                int currentSelected = CB_BatteryBypassCharging.SelectedIndex;
                CB_BatteryBypassCharging.ItemsSource = null;
                CB_BatteryBypassCharging.ItemsSource = IDevice.GetCurrent().BatteryBypassPresets;
                CB_BatteryBypassCharging.SelectedIndex = currentSelected;
            }
        }

        public void Page_Closed()
        { }

        private void SettingsManager_SettingValueChanged(string? name, object value, bool temporary)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
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
                    case "LEDPresetIndex":
                        int presetIndex = Convert.ToInt32(value);
                        if (presetIndex < IDevice.GetCurrent().LEDPresets.Count)
                        {
                            LedPresetsComboBox.SelectedIndex = presetIndex;
                        }
                        break;
                    #region Legion Go
                    case "LegionControllerPassthrough":
                        Toggle_TouchpadPassthrough.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LegionControllerSwap":
                        Toggle_ControllerSwap.IsOn = Convert.ToBoolean(value);
                        break;
                    case "LegionControllerGyroIndex":
                        ComboBox_GyroController.SelectedIndex = Convert.ToInt32(value);
                        break;
                    #endregion
                    #region Zotac Gaming ZOne
                    case "ZotacGamingZoneVRAM":
                        ComboBox_GamingZoneVRAM.SelectedIndex = Convert.ToInt32(value);
                        break;
                    #endregion
                    case "BatteryChargeLimit":
                        Toggle_BatteryChargeLimit.IsOn = Convert.ToBoolean(value);
                        break;
                    case "BatteryChargeLimitPercent":
                        Slider_BatteryChargeLimitPercent.Value = Convert.ToInt32(value);
                        break;
                    case "BatteryBypassChargingMode":
                        CB_BatteryBypassCharging.SelectedIndex = Convert.ToInt32(value);
                        break;
                    case "SensorSelection":
                        {
                            int idx = Convert.ToInt32(value);

                            // default value
                            if (idx == -1)
                            {
                                if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor))
                                {
                                    ManagerFactory.settingsManager.SetProperty(name, cB_SensorSelection.Items.IndexOf(SensorInternal));
                                }
                                else if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor))
                                {
                                    ManagerFactory.settingsManager.SetProperty(name, cB_SensorSelection.Items.IndexOf(SensorExternal));
                                }
                                else
                                {
                                    ManagerFactory.settingsManager.SetProperty(name, cB_SensorSelection.Items.IndexOf(SensorNone));
                                }

                                return;
                            }

                            cB_SensorSelection.SelectedIndex = idx;
                        }
                        break;
                    case "SensorPlacement":
                        UpdateUI_SensorPlacement(Convert.ToInt32(value));
                        break;
                    case "SensorPlacementUpsideDown":
                        Toggle_SensorPlacementUpsideDown.IsOn = Convert.ToBoolean(value);
                        break;
                    case "RyzenAdjCoAll":
                        NumberBox_SetCoAll.Value = Convert.ToInt32(value);
                        break;
                    case "RyzenAdjCoGfx":
                        NumberBox_SetCoGfx.Value = Convert.ToInt32(value);
                        break;
                    case "MsrUndervoltCore":
                        NumberBox_SetMsrCore.Value = Convert.ToInt32(value);
                        break;
                    case "MsrUndervoltGpu":
                        NumberBox_SetMsrGpu.Value = Convert.ToInt32(value);
                        break;
                    case "MsrUndervoltSoc":
                        NumberBox_SetMsrSoc.Value = Convert.ToInt32(value);
                        break;
                    case "EnhancedSleep":
                        Toggle_EnhancedSleep.IsOn = Convert.ToBoolean(value);
                        break;
                    case "GoBackToSleep":
                        Toggle_GoBackToSleep.IsOn = Convert.ToBoolean(value);
                        break;
                }
            });
        }

        public void UpdateDevice(PnPDevice device = null)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                SensorInternal.IsEnabled = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor);
                SensorExternal.IsEnabled = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor);
            });
        }

        private void OnColorValuesChanged(UISettings sender, object args)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                if (MatchAccentColor.IsOn)
                    SetAccentColor();
            });
        }

        private async void Toggle_cTDP_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            bool enabled = Toggle_cTDP.IsOn;
            if (enabled)
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

            ManagerFactory.settingsManager.SetProperty("ConfigurableTDPOverride", enabled);
            ManagerFactory.settingsManager.SetProperty("ConfigurableTDPOverrideUp", NumberBox_TDPMax.Value);
            ManagerFactory.settingsManager.SetProperty("ConfigurableTDPOverrideDown", NumberBox_TDPMin.Value);
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

            ManagerFactory.settingsManager.SetProperty("ConfigurableTDPOverrideUp", value);
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

            ManagerFactory.settingsManager.SetProperty("ConfigurableTDPOverrideDown", value);
        }

        private void UseDynamicLightingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LEDSettingsEnabled", UseDynamicLightingToggle.IsOn);
        }

        private void MatchAccentColor_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            MainColorPicker.IsEnabled = !MatchAccentColor.IsOn;
            SecondColorPicker.IsEnabled = !MatchAccentColor.IsOn;

            if (MatchAccentColor.IsOn)
                SetAccentColor();

            ManagerFactory.settingsManager.SetProperty("LEDSettingsUseAccentColor", MatchAccentColor.IsOn);
        }

        private void SetAccentColor()
        {
            MainColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(MainWindow.uiSettings.GetColorValue(UIColorType.Accent).ToString()));
            SecondColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(MainWindow.uiSettings.GetColorValue(UIColorType.Accent).ToString()));

            ManagerFactory.settingsManager.SetProperty("LEDMainColor", MainColorPicker.SelectedColor);
            ManagerFactory.settingsManager.SetProperty("LEDSecondColor", MainColorPicker.SelectedColor);
        }

        private void LEDSettingsLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ComboBoxItem comboBoxItem = (ComboBoxItem)LEDSettingsLevel.SelectedItem;
            int level = Convert.ToInt32(comboBoxItem.Tag);

            ManagerFactory.settingsManager.SetProperty("LEDSettingsLevel", level);
        }

        private void LEDOEMPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            int selectedIndex = LedPresetsComboBox.SelectedIndex;
            ManagerFactory.settingsManager.SetProperty("LEDPresetIndex", selectedIndex);
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

            ManagerFactory.settingsManager.SetProperty("LEDMainColor", prevMainColor.ToString());
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

            ManagerFactory.settingsManager.SetProperty("LEDSecondColor", prevSecondColor.ToString());
        }

        private void SliderLEDBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLEDBrightness.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LEDBrightness", value);
        }

        private async void Toggle_AmbilightVerticalBlackBarDetection_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LEDAmbilightVerticalBlackBarDetection", Toggle_AmbilightVerticalBlackBarDetection.IsOn);
        }

        private async void Toggle_UseSecondColor_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LEDUseSecondColor", Toggle_UseSecondColor.IsOn);
        }

        private void Expander_Expanded(object sender, EventArgs e)
        {
            ((SettingsExpander)sender).BringIntoView();
        }

        private void SliderLEDSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLEDSpeed.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LEDSpeed", value);
        }

        private void LEDDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LEDDirection", LEDDirection.SelectedIndex);
        }

        private void SetControlEnabledAndVisible(UIElement control, LEDLevel level)
        {
            bool isCapabilitySupported = IDevice.GetCurrent().DynamicLightingCapabilities.HasFlag(level);
            control.IsEnabled = isCapabilitySupported;
            control.Visibility = isCapabilitySupported ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Toggle_BatteryChargeLimit_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("BatteryChargeLimit", Toggle_BatteryChargeLimit.IsOn);
        }

        private void Slider_BatteryChargeLimitPercent_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = Slider_BatteryChargeLimitPercent.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("BatteryChargeLimitPercent", (int)value);
        }

        private void CB_BatteryBypassCharging_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CB_BatteryBypassCharging.SelectedIndex == -1)
                return;

            ManagerFactory.settingsManager.SetProperty("BatteryBypassChargingMode", CB_BatteryBypassCharging.SelectedIndex);
        }

        private void NumberBox_SetCoAll_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_SetCoAll.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("RyzenAdjCoAll", value);
        }

        private void NumberBox_SetCoGfx_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_SetCoGfx.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("RyzenAdjCoGfx", value);
        }

        private void NumberBox_SetMsrCore_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_SetMsrCore.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("MsrUndervoltCore", value);
        }

        private void NumberBox_SetMsrGpu_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_SetMsrGpu.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("MsrUndervoltGpu", value);
        }

        private void NumberBox_SetMsrSoc_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_SetMsrSoc.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("MsrUndervoltSoc", value);
        }

        private void Toggle_EnhancedSleep_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("EnhancedSleep", Toggle_EnhancedSleep.IsOn);
        }

        private void Toggle_GoBackToSleep_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("GoBackToSleep", Toggle_GoBackToSleep.IsOn);
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
                ManagerFactory.settingsManager.SetProperty("SensorSelection", cB_SensorSelection.SelectedIndex);
        }

        private void ui_button_calibrate_Click(object sender, RoutedEventArgs e)
        {
            // update dependencies
            SensorFamily sensorFamily = (SensorFamily)cB_SensorSelection.SelectedIndex;

            switch (sensorFamily)
            {
                case SensorFamily.Windows:
                case SensorFamily.SerialUSBIMU:
                    SensorsManager.Calibrate(IDevice.GetCurrent().GamepadMotion);
                    break;

                case SensorFamily.Controller:
                    IController controller = ControllerManager.GetTarget();
                    controller?.Calibrate();
                    break;
            }
        }

        private void SensorPlacement_Click(object sender, RoutedEventArgs? e)
        {
            var Tag = int.Parse((string)((Button)sender).Tag);

            UpdateUI_SensorPlacement(Tag);

            if (IsLoaded)
                ManagerFactory.settingsManager.SetProperty("SensorPlacement", Tag);
        }

        private void UpdateUI_SensorPlacement(int? SensorPlacement)
        {
            foreach (SimpleStackPanel panel in Grid_SensorPlacementVisualisation.Children)
                foreach (Button button in panel.Children)
                    if (int.Parse((string)button.Tag) == SensorPlacement)
                        button.SetResourceReference(BackgroundProperty, "SystemControlForegroundAccentBrush");
                    else
                        button.SetResourceReference(BackgroundProperty, "SystemControlHighlightAltBaseLowBrush");
        }

        private void Toggle_SensorPlacementUpsideDown_Toggled(object? sender, RoutedEventArgs? e)
        {
            var isUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

            if (IsLoaded)
                ManagerFactory.settingsManager.SetProperty("SensorPlacementUpsideDown", isUpsideDown);
        }
        #endregion

        #region Legion Go
        private void Toggle_TouchpadPassthrough_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LegionControllerPassthrough", Toggle_TouchpadPassthrough.IsOn);
        }

        private void Toggle_ControllerSwap_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LegionControllerSwap", Toggle_ControllerSwap.IsOn);
        }

        private void ComboBox_GyroController_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("LegionControllerGyroIndex", ComboBox_GyroController.SelectedIndex);
        }

        private void SliderLeftJoystickDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftJoystickDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetStickCustomDeadzone(LegionGoTablet.LeftJoyconIndex, (int)value - 1);
        }

        private void SliderLeftAutoSleepTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftAutoSleepTime.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetAutoSleepTime(LegionGoTablet.LeftJoyconIndex, (int)value);
        }

        private void SliderLeftTriggerDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftTriggerDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex);
            trigger.Deadzone = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex, trigger);
        }

        private void SliderLeftTriggerMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderLeftTriggerMargin.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex);
            trigger.Margin = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex, trigger);
        }

        private void SliderRightJoystickDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightJoystickDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetStickCustomDeadzone(LegionGoTablet.RightJoyconIndex, (int)value - 1);
        }

        private void SliderRightAutoSleepTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightAutoSleepTime.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SapientiaUsb.SetAutoSleepTime(LegionGoTablet.RightJoyconIndex, (int)value);
        }

        private void SliderRightTriggerDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightTriggerDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex);
            trigger.Deadzone = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex, trigger);
        }

        private void SliderRightTriggerMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = SliderRightTriggerMargin.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            var trigger = SapientiaUsb.GetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex);
            trigger.Margin = (int)value - 1;

            SapientiaUsb.SetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex, trigger);
        }
        #endregion

        #region Zotac Gaming Zone
        private void ComboBox_GamingZoneVRAM_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            if (IDevice.GetCurrent() is GamingZone gamingZone)
                gamingZone.SetVRamSize((uint)ComboBox_GamingZoneVRAM.SelectedIndex);

            ManagerFactory.settingsManager.SetProperty("ZotacGamingZoneVRAM", ComboBox_GamingZoneVRAM.SelectedIndex);
        }
        #endregion
    }
}