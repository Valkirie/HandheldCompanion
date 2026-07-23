using ColorPicker;
using ColorPicker.Models;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Linq;
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
        }

        public DevicePage(string? Tag) : this()
        {
            this.Tag = Tag;

            if (DataContext is DevicePageViewModel viewModel)
                viewModel.RestartConfirmationRequested += ShowRestartConfirmation;

            App.uiSettings.ColorValuesChanged += OnColorValuesChanged;

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            SettingsManager_SettingValueChanged("ConfigurableTDPOverride", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverride"), false, true);
            SettingsManager_SettingValueChanged("ConfigurableTDPOverrideDown", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideDown"), false, true);
            SettingsManager_SettingValueChanged("ConfigurableTDPOverrideUp", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideUp"), false, true);
            SettingsManager_SettingValueChanged("LEDSettingsEnabled", ManagerFactory.settingsManager.GetString("LEDSettingsEnabled"), false, true);
            SettingsManager_SettingValueChanged("LEDSettingsUseAccentColor", ManagerFactory.settingsManager.GetString("LEDSettingsUseAccentColor"), false, true);
            SettingsManager_SettingValueChanged("LEDSettingsLevel", ManagerFactory.settingsManager.GetString("LEDSettingsLevel"), false, true);
            SettingsManager_SettingValueChanged("LEDBrightness", ManagerFactory.settingsManager.GetString("LEDBrightness"), false, true);
            SettingsManager_SettingValueChanged("LEDSpeed", ManagerFactory.settingsManager.GetString("LEDSpeed"), false, true);
            SettingsManager_SettingValueChanged("LEDDirection", ManagerFactory.settingsManager.GetString("LEDDirection"), false, true);
            SettingsManager_SettingValueChanged("LEDMainColor", ManagerFactory.settingsManager.GetString("LEDMainColor"), false, true);
            SettingsManager_SettingValueChanged("LEDSecondColor", ManagerFactory.settingsManager.GetString("LEDSecondColor"), false, true);
            SettingsManager_SettingValueChanged("LEDAmbilightVerticalBlackBarDetection", ManagerFactory.settingsManager.GetString("LEDAmbilightVerticalBlackBarDetection"), false, true);
            SettingsManager_SettingValueChanged("LEDUseSecondColor", ManagerFactory.settingsManager.GetString("LEDUseSecondColor"), false, true);
            SettingsManager_SettingValueChanged("LEDPresetIndex", ManagerFactory.settingsManager.GetString("LEDPresetIndex"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerPassthrough", ManagerFactory.settingsManager.GetString("LegionControllerPassthrough"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerSwap", ManagerFactory.settingsManager.GetString("LegionControllerSwap"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerGyroIndex", ManagerFactory.settingsManager.GetString("LegionControllerGyroIndex"), false, true);
            SettingsManager_SettingValueChanged("ZotacGamingZoneVRAM", ManagerFactory.settingsManager.GetString("ZotacGamingZoneVRAM"), false, true);
            SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetString("BatteryChargeLimit"), false, true);
            SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetString("BatteryChargeLimitPercent"), false, true);
            SettingsManager_SettingValueChanged("BatteryBypassChargingMode", ManagerFactory.settingsManager.GetString("BatteryBypassChargingMode"), false, true);
            SettingsManager_SettingValueChanged("SensorSelection", ManagerFactory.settingsManager.GetString("SensorSelection"), false, true);
            SettingsManager_SettingValueChanged("SensorPlacement", ManagerFactory.settingsManager.GetString("SensorPlacement"), false, true);
            SettingsManager_SettingValueChanged("SensorPlacementUpsideDown", ManagerFactory.settingsManager.GetString("SensorPlacementUpsideDown"), false, true);
            SettingsManager_SettingValueChanged("RyzenAdjCoAll", ManagerFactory.settingsManager.GetString("RyzenAdjCoAll"), false, true);
            SettingsManager_SettingValueChanged("RyzenAdjCoGfx", ManagerFactory.settingsManager.GetString("RyzenAdjCoGfx"), false, true);
            SettingsManager_SettingValueChanged("MsrUndervoltCore", ManagerFactory.settingsManager.GetString("MsrUndervoltCore"), false, true);
            SettingsManager_SettingValueChanged("MsrUndervoltGpu", ManagerFactory.settingsManager.GetString("MsrUndervoltGpu"), false, true);
            SettingsManager_SettingValueChanged("MsrUndervoltSoc", ManagerFactory.settingsManager.GetString("MsrUndervoltSoc"), false, true);
            SettingsManager_SettingValueChanged("EnhancedSleep", ManagerFactory.settingsManager.GetString("EnhancedSleep"), false, true);
            SettingsManager_SettingValueChanged("GoBackToSleep", ManagerFactory.settingsManager.GetString("GoBackToSleep"), false, true);
            SettingsManager_SettingValueChanged("GoBackToSleepOnPowerButton", ManagerFactory.settingsManager.GetString("GoBackToSleepOnPowerButton"), false, true);
            SettingsManager_SettingValueChanged("GoBackToSleepOnFingerprintReader", ManagerFactory.settingsManager.GetString("GoBackToSleepOnFingerprintReader"), false, true);
            SettingsManager_SettingValueChanged("GoBackToSleepOnJoystick", ManagerFactory.settingsManager.GetString("GoBackToSleepOnJoystick"), false, true);
            SettingsManager_SettingValueChanged("GoBackToSleepOnChargerConnected", ManagerFactory.settingsManager.GetString("GoBackToSleepOnChargerConnected"), false, true);
            SettingsManager_SettingValueChanged("DockedDisplayBehavior", ManagerFactory.settingsManager.GetString("DockedDisplayBehavior"), false, true);
        }

        public void Page_Closed()
        {
            if (DataContext is DevicePageViewModel viewModel)
                viewModel.RestartConfirmationRequested -= ShowRestartConfirmation;

            App.uiSettings.ColorValuesChanged -= OnColorValuesChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        }

        private async void ShowRestartConfirmation()
        {
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(ShowRestartConfirmation);
                return;
            }

            if (DataContext is not DevicePageViewModel viewModel)
                return;

            ContentDialogResult result = await new Dialog(MainWindow.GetCurrent())
            {
                Title = Properties.Resources.Dialog_ForceRestartTitle,
                Content = Properties.Resources.Dialog_ForceRestartDesc,
                DefaultButton = ContentDialogButton.Close,
                CloseButtonText = Properties.Resources.Dialog_No,
                PrimaryButtonText = Properties.Resources.Dialog_Yes
            }.ShowAsync();

            viewModel.CompleteRestartConfirmation(result == ContentDialogResult.Primary);
        }

        private void SettingsManager_SettingValueChanged(string? name, object? value, bool temporary, bool initializing)
        {
            // UI thread
            UIHelper.TryBeginInvoke(() =>
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

                        if (MatchAccentColor.IsOn)
                            SetAccentColor();
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
                        cB_SensorSelection.SelectedIndex = Convert.ToInt32(value);
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
                    case "GoBackToSleepOnPowerButton":
                        CB_GoBackToSleepOnPowerButton.IsChecked = Convert.ToBoolean(value);
                        break;
                    case "GoBackToSleepOnFingerprintReader":
                        CB_GoBackToSleepOnFingerprintReader.IsChecked = Convert.ToBoolean(value);
                        break;
                    case "GoBackToSleepOnJoystick":
                        CB_GoBackToSleepOnJoystick.IsChecked = Convert.ToBoolean(value);
                        break;
                    case "GoBackToSleepOnChargerConnected":
                        CB_GoBackToSleepOnChargerConnected.IsChecked = Convert.ToBoolean(value);
                        break;
                    case "DockedDisplayBehavior":
                        cB_DockedDisplayBehavior.SelectedIndex = Convert.ToInt32(value);
                        break;
                }
            });
        }

        private void OnColorValuesChanged(UISettings sender, object args)
        {
            // UI thread
            UIHelper.TryBeginInvoke(() =>
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

            if (MatchAccentColor.IsOn)
                SetAccentColor();

            ManagerFactory.settingsManager.SetProperty("LEDSettingsUseAccentColor", MatchAccentColor.IsOn);
        }

        private void SetAccentColor()
        {
            MainColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(App.uiSettings.GetColorValue(UIColorType.Accent).ToString()));
            SecondColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(App.uiSettings.GetColorValue(UIColorType.Accent).ToString()));

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

        private void NumberBox_TctlLimit_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_TctlLimit.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("TctlLimit", (uint)value);
        }

        private void NumberBox_SkinTemperatureLimit_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            var value = NumberBox_SkinTemperatureLimit.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("SkinTemperatureLimit", (uint)value);
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

        private void CB_GoBackToSleepOnWakeReason_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("GoBackToSleepOnPowerButton", CB_GoBackToSleepOnPowerButton.IsChecked == true);
            ManagerFactory.settingsManager.SetProperty("GoBackToSleepOnFingerprintReader", CB_GoBackToSleepOnFingerprintReader.IsChecked == true);
            ManagerFactory.settingsManager.SetProperty("GoBackToSleepOnJoystick", CB_GoBackToSleepOnJoystick.IsChecked == true);
            ManagerFactory.settingsManager.SetProperty("GoBackToSleepOnChargerConnected", CB_GoBackToSleepOnChargerConnected.IsChecked == true);
        }

        #region Display
        private void cB_DockedDisplayBehavior_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ManagerFactory.settingsManager.SetProperty("DockedDisplayBehavior", cB_DockedDisplayBehavior.SelectedIndex);
        }
        #endregion

        #region Sensor
        private void cB_SensorSelection_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (cB_SensorSelection.SelectedIndex == -1)
                return;

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
                    IController? controller = ControllerManager.GetTarget();
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
            foreach (Button button in Grid_SensorPlacementVisualisation.Children.OfType<Button>())
                if (int.Parse((string)button.Tag) == SensorPlacement)
                    button.SetResourceReference(BackgroundProperty, "SystemControlForegroundAccentBrush");
                else
                    button.SetResourceReference(BackgroundProperty, "SystemControlHighlightAltBaseLowBrush");
        }

        private void Toggle_SensorPlacementUpsideDown_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            if (sender is ToggleSwitch toggleSwitch)
            {
                bool isUpsideDown = toggleSwitch.IsOn;
                ManagerFactory.settingsManager.SetProperty("SensorPlacementUpsideDown", isUpsideDown);
            }
        }
        #endregion
    }
}