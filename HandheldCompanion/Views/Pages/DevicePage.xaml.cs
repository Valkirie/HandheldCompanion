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
using iNKORE.UI.WPF.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.UI.ViewManagement;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;
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

            // manage events
            IDevice.GetCurrent().CapabilitiesChanged += OnCapabilitiesChanged;
            IDevice.GetCurrent().Opened += Device_Opened;
            IDevice.GetCurrent().Closed += Device_Closed;
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

            // manage events
            ControllerManager.Initialized += ControllerManager_Initialized;

            // raise events
            if (ControllerManager.IsInitialized)
                ControllerManager_Initialized();
        }

        private void ControllerManager_Initialized()
        {
            // manage events
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // raise events
            if (ControllerManager.HasTargetController && ControllerManager.GetTarget() is IController controller)
                ControllerManager_ControllerSelected(controller);
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
            SettingsManager_SettingValueChanged("ConfigurableTDPOverride", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverride"), false, false);
            SettingsManager_SettingValueChanged("ConfigurableTDPOverrideDown", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideDown"), false, false);
            SettingsManager_SettingValueChanged("ConfigurableTDPOverrideUp", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideUp"), false, false);
            SettingsManager_SettingValueChanged("LEDSettingsEnabled", ManagerFactory.settingsManager.GetString("LEDSettingsEnabled"), false, false);
            SettingsManager_SettingValueChanged("LEDSettingsUseAccentColor", ManagerFactory.settingsManager.GetString("LEDSettingsUseAccentColor"), false, false);
            SettingsManager_SettingValueChanged("LEDSettingsLevel", ManagerFactory.settingsManager.GetString("LEDSettingsLevel"), false, false);
            SettingsManager_SettingValueChanged("LEDBrightness", ManagerFactory.settingsManager.GetString("LEDBrightness"), false, false);
            SettingsManager_SettingValueChanged("LEDSpeed", ManagerFactory.settingsManager.GetString("LEDSpeed"), false, false);
            SettingsManager_SettingValueChanged("LEDDirection", ManagerFactory.settingsManager.GetString("LEDDirection"), false, false);
            SettingsManager_SettingValueChanged("LEDMainColor", ManagerFactory.settingsManager.GetString("LEDMainColor"), false, false);
            SettingsManager_SettingValueChanged("LEDSecondColor", ManagerFactory.settingsManager.GetString("LEDSecondColor"), false, false);
            SettingsManager_SettingValueChanged("LEDAmbilightVerticalBlackBarDetection", ManagerFactory.settingsManager.GetString("LEDAmbilightVerticalBlackBarDetection"), false, false);
            SettingsManager_SettingValueChanged("LEDUseSecondColor", ManagerFactory.settingsManager.GetString("LEDUseSecondColor"), false, false);
            SettingsManager_SettingValueChanged("LEDPresetIndex", ManagerFactory.settingsManager.GetString("LEDPresetIndex"), false, false);
            SettingsManager_SettingValueChanged("LegionControllerPassthrough", ManagerFactory.settingsManager.GetString("LegionControllerPassthrough"), false, false);
            SettingsManager_SettingValueChanged("LegionControllerSwap", ManagerFactory.settingsManager.GetString("LegionControllerSwap"), false, false);
            SettingsManager_SettingValueChanged("LegionControllerGyroIndex", ManagerFactory.settingsManager.GetString("LegionControllerGyroIndex"), false, false);
            SettingsManager_SettingValueChanged("ZotacGamingZoneVRAM", ManagerFactory.settingsManager.GetString("ZotacGamingZoneVRAM"), false, false);
            SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetString("BatteryChargeLimit"), false, false);
            SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetString("BatteryChargeLimitPercent"), false, false);
            SettingsManager_SettingValueChanged("BatteryBypassChargingMode", ManagerFactory.settingsManager.GetString("BatteryBypassChargingMode"), false, false);
            SettingsManager_SettingValueChanged("SensorSelection", ManagerFactory.settingsManager.GetString("SensorSelection"), false, false);
            SettingsManager_SettingValueChanged("SensorPlacement", ManagerFactory.settingsManager.GetString("SensorPlacement"), false, false);
            SettingsManager_SettingValueChanged("SensorPlacementUpsideDown", ManagerFactory.settingsManager.GetString("SensorPlacementUpsideDown"), false, false);
            SettingsManager_SettingValueChanged("RyzenAdjCoAll", ManagerFactory.settingsManager.GetString("RyzenAdjCoAll"), false, false);
            SettingsManager_SettingValueChanged("RyzenAdjCoGfx", ManagerFactory.settingsManager.GetString("RyzenAdjCoGfx"), false, false);
            SettingsManager_SettingValueChanged("MsrUndervoltCore", ManagerFactory.settingsManager.GetString("MsrUndervoltCore"), false, false);
            SettingsManager_SettingValueChanged("MsrUndervoltGpu", ManagerFactory.settingsManager.GetString("MsrUndervoltGpu"), false, false);
            SettingsManager_SettingValueChanged("MsrUndervoltSoc", ManagerFactory.settingsManager.GetString("MsrUndervoltSoc"), false, false);
            SettingsManager_SettingValueChanged("EnhancedSleep", ManagerFactory.settingsManager.GetString("EnhancedSleep"), false, false);
            SettingsManager_SettingValueChanged("GoBackToSleep", ManagerFactory.settingsManager.GetString("GoBackToSleep"), false, false);
            SettingsManager_SettingValueChanged("GoBackToSleepOnPowerButton", ManagerFactory.settingsManager.GetString("GoBackToSleepOnPowerButton"), false, false);
            SettingsManager_SettingValueChanged("GoBackToSleepOnFingerprintReader", ManagerFactory.settingsManager.GetString("GoBackToSleepOnFingerprintReader"), false, false);
            SettingsManager_SettingValueChanged("GoBackToSleepOnJoystick", ManagerFactory.settingsManager.GetString("GoBackToSleepOnJoystick"), false, false);
            SettingsManager_SettingValueChanged("GoBackToSleepOnChargerConnected", ManagerFactory.settingsManager.GetString("GoBackToSleepOnChargerConnected"), false, false);
            SettingsManager_SettingValueChanged("DockedDisplayBehavior", ManagerFactory.settingsManager.GetString("DockedDisplayBehavior"), false, false);
        }

        private void OnCapabilitiesChanged(IDevice sender, DeviceCapabilities capabilities)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                SensorInternal.IsEnabled = sender.Capabilities.HasFlag(DeviceCapabilities.InternalSensor);
                SensorExternal.IsEnabled = sender.Capabilities.HasFlag(DeviceCapabilities.ExternalSensor);
            });
        }

        private LegionTriggerDeadzone legionTriggerDeadzoneLeft = new();
        private LegionTriggerDeadzone legionTriggerDeadzoneRight = new();

        private void Device_Opened(IDevice sender)
        {
            // Update UI on UI thread
            UIHelper.TryInvoke(() =>
            {
                // Adjust UI element availability based on device capabilities
                if (sender.Capabilities.HasFlag(DeviceCapabilities.DynamicLighting))
                {
                    DynamicLightingPanel.Visibility = Visibility.Visible;

                    SetControlEnabledAndVisible(sender, LEDSolidColor, LEDLevel.SolidColor);
                    SetControlEnabledAndVisible(sender, LEDBreathing, LEDLevel.Breathing);
                    SetControlEnabledAndVisible(sender, LEDRainbow, LEDLevel.Rainbow);
                    SetControlEnabledAndVisible(sender, LEDWave, LEDLevel.Wave);
                    SetControlEnabledAndVisible(sender, LEDWheel, LEDLevel.Wheel);
                    SetControlEnabledAndVisible(sender, LEDGradient, LEDLevel.Gradient);
                    SetControlEnabledAndVisible(sender, LEDAmbilight, LEDLevel.Ambilight);
                    SetControlEnabledAndVisible(sender, LEDPreset, LEDLevel.LEDPreset);
                }

                LEDBrightness.Visibility = sender.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingBrightness) ? Visibility.Visible : Visibility.Collapsed;
                SecondColorToggleCard.Visibility = SecondColorPickerCard.Visibility = sender.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor) ? Visibility.Visible : Visibility.Collapsed;
            });

            // device-specific logic
            // we might need the device to be opened
            if (sender is LegionGoTablet legionGoTablet)
            {
                // Perform USB I/O operations (left joystick)
                int leftJoystickDeadzone = GetStickCustomDeadzone(LegionGoTablet.LeftJoyconIndex);
                int leftAutoSleepTime = GetAutoSleepTime(LegionGoTablet.LeftJoyconIndex);
                legionTriggerDeadzoneLeft = GetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex);

                // Perform USB I/O operations (right joystick)
                int rightJoystickDeadzone = GetStickCustomDeadzone(LegionGoTablet.RightJoyconIndex);
                int rightAutoSleepTime = GetAutoSleepTime(LegionGoTablet.RightJoyconIndex);
                legionTriggerDeadzoneRight = GetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex);

                // Update UI on UI thread
                UIHelper.TryInvoke(() =>
                {
                    // Show LegionGoPanel immediately
                    LegionGoPanel.Visibility = Visibility.Visible;
                    LegionGoSensorSelection.Visibility = Visibility.Visible;

                    // left joystick
                    SliderLeftJoystickDeadzone.Value = leftJoystickDeadzone;
                    SliderLeftAutoSleepTime.Value = leftAutoSleepTime;
                    SliderLeftTriggerDeadzone.Value = legionTriggerDeadzoneLeft.Deadzone;
                    SliderLeftTriggerMargin.Value = legionTriggerDeadzoneLeft.Margin;
                    LegionGoLeftController.Visibility = Visibility.Visible;

                    // right joystick
                    SliderRightJoystickDeadzone.Value = rightJoystickDeadzone;
                    SliderRightAutoSleepTime.Value = rightAutoSleepTime;
                    SliderRightTriggerDeadzone.Value = legionTriggerDeadzoneRight.Deadzone;
                    SliderRightTriggerMargin.Value = legionTriggerDeadzoneRight.Margin;
                    LegionGoRightController.Visibility = Visibility.Visible;
                });
            }
            else if (sender is ClawA1M)
            {
                // Update UI on UI thread
                UIHelper.TryInvoke(() =>
                {
                    // Show MSIClawPanel
                    MSIClawPanel.Visibility = Visibility.Visible;
                });
            }
            else if (sender is GamingZone)
            {
                // Update UI on UI thread
                UIHelper.TryInvoke(() =>
                {
                    ZotacGamingZonePanel.Visibility = Visibility.Visible;
                });
            }
            else if (sender is OneXPlayerX1)
            {
                // Update UI on UI thread
                UIHelper.TryInvoke(() =>
                {
                    LedPresetsComboBox.ItemsSource = sender.LEDPresets;
                    CB_BatteryBypassCharging.ItemsSource = sender.BatteryBypassPresets;
                });
            }
        }

        private void Device_Closed(IDevice sender)
        {
            // do something
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            // UI thread (async to prevent blocking event callers)
            UIHelper.TryBeginInvoke(() =>
            {
                SensorController.IsEnabled = Controller.Capabilities.HasFlag(ControllerCapabilities.MotionSensor);
            });
        }

        private void Page_Loaded(object? sender, RoutedEventArgs? e)
        {
            // do something
        }

        public void Page_Closed()
        {
            IDevice.GetCurrent().CapabilitiesChanged -= OnCapabilitiesChanged;
            IDevice.GetCurrent().Opened -= Device_Opened;
            IDevice.GetCurrent().Closed -= Device_Closed;
            App.uiSettings.ColorValuesChanged -= OnColorValuesChanged;
            ControllerManager.Initialized -= ControllerManager_Initialized;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string? name, object? value, bool temporary, bool initializing)
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
                        MainColorPicker.IsEnabled = !MatchAccentColor.IsOn;
                        SecondColorPicker.IsEnabled = !MatchAccentColor.IsOn;

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

            MainColorPicker.IsEnabled = !MatchAccentColor.IsOn;
            SecondColorPicker.IsEnabled = !MatchAccentColor.IsOn;

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

        private void SetControlEnabledAndVisible(IDevice device, UIElement control, LEDLevel level)
        {
            // Update UI on UI thread
            UIHelper.TryInvoke(() =>
            {
                bool isCapabilitySupported = device.DynamicLightingCapabilities.HasFlag(level);
                control.IsEnabled = isCapabilitySupported;
                control.Visibility = isCapabilitySupported ? Visibility.Visible : Visibility.Collapsed;
            });
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

            // update dependencies
            SensorFamily sensorFamily = (SensorFamily)cB_SensorSelection.SelectedIndex;

            bool isExternal = sensorFamily == SensorFamily.SerialUSBIMU;

            ui_button_calibrate.IsEnabled = sensorFamily != SensorFamily.None;
            SensorPlacementUpsideDown.IsEnabled = isExternal;
            SensorPlacementVisualisation.IsEnabled = isExternal;
            Toggle_SensorPlacementUpsideDown.IsEnabled = isExternal;
            Grid_SensorPlacementVisualisation.IsEnabled = isExternal;

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
            double value = SliderLeftJoystickDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            bool success = SetStickCustomDeadzone(LegionGoTablet.LeftJoyconIndex, (int)value);
        }

        private void SliderLeftAutoSleepTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderLeftAutoSleepTime.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            bool success = SetAutoSleepTime(LegionGoTablet.LeftJoyconIndex, (int)value);
        }

        private void SliderLeftTriggerDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderLeftTriggerDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            legionTriggerDeadzoneLeft.Deadzone = (int)value;

            bool success = SetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex, legionTriggerDeadzoneLeft);
        }

        private void SliderLeftTriggerMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderLeftTriggerMargin.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            legionTriggerDeadzoneLeft.Margin = (int)value;

            bool success = SetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex, legionTriggerDeadzoneLeft);
        }

        private void SliderRightJoystickDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderRightJoystickDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            bool success = SetStickCustomDeadzone(LegionGoTablet.RightJoyconIndex, (int)value);
        }

        private void SliderRightAutoSleepTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderRightAutoSleepTime.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            bool success = SetAutoSleepTime(LegionGoTablet.RightJoyconIndex, (int)value);
        }

        private void SliderRightTriggerDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderRightTriggerDeadzone.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            legionTriggerDeadzoneRight.Deadzone = (int)value;

            bool success = SetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex, legionTriggerDeadzoneRight);
        }

        private void SliderRightTriggerMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderRightTriggerMargin.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            legionTriggerDeadzoneRight.Margin = (int)value;

            bool success = SetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex, legionTriggerDeadzoneRight);
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