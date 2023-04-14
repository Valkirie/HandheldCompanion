using ControllerCommon.Devices;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using ModernWpf;
using ModernWpf.Controls;
using ModernWpf.Controls.Primitives;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static ControllerCommon.Utils.DeviceUtils;
using static HandheldCompanion.Managers.UpdateManager;
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.Managers.ServiceControllerStatus;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();

            // initialize components
            foreach (ServiceStartMode mode in ((ServiceStartMode[])Enum.GetValues(typeof(ServiceStartMode))).Where(mode => mode >= ServiceStartMode.Automatic))
            {
                RadioButton radio = new() { Content = EnumUtils.GetDescriptionFromEnumValue(mode) };
                switch (mode)
                {
                    case ServiceStartMode.Disabled:
                        radio.IsEnabled = false;
                        break;
                }

                cB_StartupType.Items.Add(radio);
            }


            cB_Language.Items.Add(new CultureInfo("en-US"));
            cB_Language.Items.Add(new CultureInfo("fr-FR"));
            cB_Language.Items.Add(new CultureInfo("de-DE"));
            cB_Language.Items.Add(new CultureInfo("zh-CN"));
            cB_Language.Items.Add(new CultureInfo("zh-Hant"));

            // call function
            UpdateDevice();

            // initialize manager(s)
            MainWindow.serviceManager.Updated += OnServiceUpdate;
            MainWindow.updateManager.Updated += UpdateManager_Updated;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        public SettingsPage(string? Tag) : this()
        {
            this.Tag = Tag;
        }

        private void SettingsManager_SettingValueChanged(string? name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "MainWindowTheme":
                        cB_Theme.SelectedIndex = Convert.ToInt32(value);
                        cB_Theme_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
                        break;
                    case "MainWindowBackdrop":
                        cB_Backdrop.SelectedIndex = Convert.ToInt32(value);
                        cB_Backdrop_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
                        break;
                    case "SensorSelection":
                        {
                            int idx = Convert.ToInt32(value);

                            // default value
                            if (idx == -1)
                            {
                                if (MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ControllerSensor))
                                    SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.Items.IndexOf(SensorController));
                                else if (MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.InternalSensor))
                                    SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.Items.IndexOf(SensorInternal));
                                else if (MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ExternalSensor))
                                    SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.Items.IndexOf(SensorExternal));
                                else
                                    SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.Items.IndexOf(SensorNone));

                                return;
                            }
                            else
                            {
                                cB_SensorSelection.SelectedIndex = idx;
                            }

                            cB_SensorSelection.SelectedIndex = idx;
                            cB_SensorSelection_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
                        }
                        break;
                    case "RunAtStartup":
                        Toggle_AutoStart.IsOn = Convert.ToBoolean(value);
                        break;
                    case "StartMinimized":
                        Toggle_Background.IsOn = Convert.ToBoolean(value);
                        break;
                    case "CloseMinimises":
                        Toggle_CloseMinimizes.IsOn = Convert.ToBoolean(value);
                        break;
                    case "DesktopProfileOnStart":
                        Toggle_DesktopProfileOnStart.IsOn = Convert.ToBoolean(value);
                        break;
                    case "ToastEnable":
                        Toggle_Notification.IsOn = Convert.ToBoolean(value);
                        break;
                    case "StartServiceWithCompanion":
                        Toggle_ServiceStartup.IsOn = Convert.ToBoolean(value);
                        break;
                    case "HaltServiceWithCompanion":
                        Toggle_ServiceShutdown.IsOn = Convert.ToBoolean(value);
                        break;
                    case "SensorPlacementUpsideDown":
                        Toggle_SensorPlacementUpsideDown.IsOn = Convert.ToBoolean(value);
                        break;
                    case "ConfigurableTDPOverride":
                        Toggle_cTDP.IsOn = Convert.ToBoolean(value);
                        break;
                    case "ConfigurableTDPOverrideDown":
                        NumberBox_TDPMin.Value = Convert.ToDouble(value);
                        break;
                    case "ConfigurableTDPOverrideUp":
                        NumberBox_TDPMax.Value = Convert.ToDouble(value);
                        break;
                    case "CurrentCulture":
                        cB_Language.SelectedItem = new CultureInfo((string)value);
                        cB_Language_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
                        break;
                    case "SensorPlacement":
                        UpdateUI_SensorPlacement(Convert.ToInt32(value));
                        break;
                    case "UseEnergyStar":
                        Toggle_EnergyStar.IsOn = Convert.ToBoolean(value);
                        break;
                    case "ServiceStartMode":
                        cB_StartupType.SelectedIndex = Convert.ToInt32(value);
                        cB_StartupType_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
                        break;
                    case "QuietModeEnabled":
                        Toggle_FanControl.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuietModeDuty":
                        FanDutyCycle.Value = Convert.ToDouble(value);
                        break;
                }
            });
        }

        public void UpdateDevice(PnPDevice device = null)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                SensorInternal.IsEnabled = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.InternalSensor);
                SensorExternal.IsEnabled = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ExternalSensor);
                SensorController.IsEnabled = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ControllerSensor);
                FanControlBorder.IsEnabled = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.FanControl);
            });
        }

        private void Page_Loaded(object? sender, RoutedEventArgs? e)
        {
            MainWindow.updateManager.Start();
        }

        public void Page_Closed()
        {
            MainWindow.serviceManager.Updated -= OnServiceUpdate;
        }

        private void Toggle_AutoStart_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("RunAtStartup", Toggle_AutoStart.IsOn);
        }

        private void Toggle_Background_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("StartMinimized", Toggle_Background.IsOn);
        }

        private void cB_StartupType_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (cB_StartupType.SelectedIndex == -1)
                return;

            ServiceStartMode mode;
            switch (cB_StartupType.SelectedIndex)
            {
                case 0:
                    mode = ServiceStartMode.Automatic;
                    break;
                default:
                case 1:
                    mode = ServiceStartMode.Manual;
                    break;
                case 2:
                    mode = ServiceStartMode.Disabled;
                    break;
            }

            MainWindow.serviceManager.SetStartType(mode);

            // service was not found
            if (!cB_StartupType.IsEnabled)
                return;

            SettingsManager.SetProperty("ServiceStartMode", cB_StartupType.SelectedIndex);
        }

        private void Toggle_CloseMinimizes_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("CloseMinimises", Toggle_CloseMinimizes.IsOn);
        }

        private void Toggle_DesktopProfileOnStart_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("DesktopProfileOnStart", Toggle_DesktopProfileOnStart.IsOn);
        }

        private void UpdateManager_Updated(UpdateStatus status, UpdateFile updateFile, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (status)
                {
                    case UpdateStatus.Failed: // lazy ?
                    case UpdateStatus.Updated:
                    case UpdateStatus.Initialized:
                        {
                            if (updateFile is not null)
                            {
                                updateFile.updateDownload.Visibility = Visibility.Visible;

                                updateFile.updatePercentage.Visibility = Visibility.Collapsed;
                                updateFile.updateInstall.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                LabelUpdate.Text = Properties.Resources.SettingsPage_UpToDate;
                                LabelUpdateDate.Text = Properties.Resources.SettingsPage_LastChecked + MainWindow.updateManager.GetTime();

                                LabelUpdateDate.Visibility = Visibility.Visible;
                                GridUpdateSymbol.Visibility = Visibility.Visible;
                                ProgressBarUpdate.Visibility = Visibility.Collapsed;
                                B_CheckUpdate.IsEnabled = true;
                            }
                        }
                        break;

                    case UpdateStatus.Checking:
                        {
                            LabelUpdate.Text = Properties.Resources.SettingsPage_UpdateCheck;

                            GridUpdateSymbol.Visibility = Visibility.Collapsed;
                            LabelUpdateDate.Visibility = Visibility.Collapsed;
                            ProgressBarUpdate.Visibility = Visibility.Visible;
                            B_CheckUpdate.IsEnabled = false;
                        }
                        break;

                    case UpdateStatus.Ready:
                        {
                            ProgressBarUpdate.Visibility = Visibility.Collapsed;

                            Dictionary<string, UpdateFile> updateFiles = (Dictionary<string, UpdateFile>)value;
                            LabelUpdate.Text = Properties.Resources.SettingsPage_UpdateAvailable;

                            foreach (UpdateFile update in updateFiles.Values)
                            {
                                var border = update.Draw();

                                // Set download button action
                                update.updateDownload.Click += (sender, e) =>
                                {
                                    MainWindow.updateManager.DownloadUpdateFile(update);
                                };

                                // Set button action
                                update.updateInstall.Click += (sender, e) =>
                                {
                                    MainWindow.updateManager.InstallUpdate(update);
                                };

                                CurrentUpdates.Children.Add(border);
                            }
                        }
                        break;

                    case UpdateStatus.Changelog:
                        {
                            CurrentChangelog.Visibility = Visibility.Visible;
                            CurrentChangelog.AppendText((string)value);
                        }
                        break;

                    case UpdateStatus.Download:
                        {
                            updateFile.updateDownload.Visibility = Visibility.Collapsed;
                            updateFile.updatePercentage.Visibility = Visibility.Visible;
                        }
                        break;

                    case UpdateStatus.Downloading:
                        {
                            int progress = (int)value;
                            updateFile.updatePercentage.Text = Properties.Resources.SettingsPage_DownloadingPercentage + $"{value} %";
                        }
                        break;

                    case UpdateStatus.Downloaded:
                        {
                            updateFile.updateInstall.Visibility = Visibility.Visible;

                            updateFile.updateDownload.Visibility = Visibility.Collapsed;
                            updateFile.updatePercentage.Visibility = Visibility.Collapsed;
                        }
                        break;
                }
            });
        }

        private void B_CheckUpdate_Click(object? sender, System.Windows.RoutedEventArgs? e)
        {
            new Thread(() =>
            {
                MainWindow.updateManager.StartProcess();
            }).Start();
        }

        private void Toggle_ServiceShutdown_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("HaltServiceWithCompanion", Toggle_ServiceShutdown.IsOn);
        }

        private void Toggle_ServiceStartup_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("StartServiceWithCompanion", Toggle_ServiceStartup.IsOn);
        }

        private void cB_Language_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            CultureInfo culture = (CultureInfo)cB_Language.SelectedItem;

            if (culture is null)
                return;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("CurrentCulture", culture.Name);

            // prevent message from being displayed again...
            if (culture.Name == CultureInfo.CurrentCulture.Name)
                return;

            _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_AppLanguageWarning}",
                Properties.Resources.SettingsPage_AppLanguageWarningDesc,
                ContentDialogButton.Primary, string.Empty, $"{Properties.Resources.ProfilesPage_OK}");
        }

        private void Toggle_Notification_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("ToastEnable", Toggle_Notification.IsOn);
        }

        private void cB_Theme_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (cB_Theme.SelectedIndex == -1)
                return;

            ThemeManager.Current.ApplicationTheme = (ApplicationTheme)cB_Theme.SelectedIndex;

            // update default style
            MainWindow.GetCurrent().UpdateDefaultStyle();
            MainWindow.overlayquickTools.UpdateDefaultStyle();

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("MainWindowTheme", cB_Theme.SelectedIndex);
        }

        private void cB_Backdrop_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (cB_Backdrop.SelectedIndex == -1)
                return;

            switch (cB_Backdrop.SelectedIndex)
            {
                case 0: // "None":
                    WindowHelper.SetSystemBackdropType(MainWindow.GetCurrent(), BackdropType.None);
                    WindowHelper.SetUseAcrylicBackdrop(MainWindow.GetCurrent(), false);
                    WindowHelper.SetUseAeroBackdrop(MainWindow.GetCurrent(), false);
                    break;
                case 1: // "Mica":
                    WindowHelper.SetSystemBackdropType(MainWindow.GetCurrent(), BackdropType.Mica);
                    WindowHelper.SetUseAcrylicBackdrop(MainWindow.GetCurrent(), false);
                    WindowHelper.SetUseAeroBackdrop(MainWindow.GetCurrent(), false);
                    break;
                case 2: // "Tabbed":
                    WindowHelper.SetSystemBackdropType(MainWindow.GetCurrent(), BackdropType.Tabbed);
                    WindowHelper.SetUseAcrylicBackdrop(MainWindow.GetCurrent(), false);
                    WindowHelper.SetUseAeroBackdrop(MainWindow.GetCurrent(), false);
                    break;
                case 3: // "Acrylic":
                    WindowHelper.SetSystemBackdropType(MainWindow.GetCurrent(), BackdropType.Acrylic);
                    WindowHelper.SetUseAcrylicBackdrop(MainWindow.GetCurrent(), true);
                    WindowHelper.SetUseAeroBackdrop(MainWindow.GetCurrent(), true);
                    break;
            }

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("MainWindowBackdrop", cB_Backdrop.SelectedIndex);
        }

        private async void Toggle_EnergyStar_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            if (Toggle_EnergyStar.IsOn)
            {
                // todo: localize me !
                Task<ContentDialogResult> result = Dialog.ShowAsync(
                    "Warning",
                    "EcoQoS is a new Quality of Service (QoS) level introduced to Windows, leading to better energy efficiency. Use at your own risk.",
                    ContentDialogButton.Primary, "Cancel", Properties.Resources.ProfilesPage_OK);

                await result; // sync call

                switch (result.Result)
                {
                    case ContentDialogResult.Primary:
                        break;
                    default:
                    case ContentDialogResult.None:
                        // restore previous state
                        Toggle_EnergyStar.IsOn = false;
                        return;
                }
            }

            SettingsManager.SetProperty("UseEnergyStar", Toggle_EnergyStar.IsOn);
        }

        private async void Toggle_cTDP_Toggled(object? sender, RoutedEventArgs? e)
        {
            if (!IsLoaded)
                return;

            if (Toggle_cTDP.IsOn)
            {
                // todo: localize me !
                Task<ContentDialogResult> result = Dialog.ShowAsync(
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
            double value = NumberBox_TDPMax.Value;
            if (double.IsNaN(value))
                return;

            NumberBox_TDPMin.Maximum = value;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("ConfigurableTDPOverrideUp", value);
        }

        private void NumberBox_TDPMin_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
        {
            double value = NumberBox_TDPMin.Value;
            if (double.IsNaN(value))
                return;

            NumberBox_TDPMax.Minimum = value;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("ConfigurableTDPOverrideDown", value);
        }

        private void cB_SensorSelection_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (cB_SensorSelection.SelectedIndex == -1)
                return;

            // update dependencies
            Toggle_SensorPlacementUpsideDown.IsEnabled = cB_SensorSelection.SelectedIndex == (int)SensorFamily.SerialUSBIMU ? true : false;
            Grid_SensorPlacementVisualisation.IsEnabled = cB_SensorSelection.SelectedIndex == (int)SensorFamily.SerialUSBIMU ? true : false;

            if (IsLoaded)
                SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.SelectedIndex);
        }

        private void SensorPlacement_Click(object sender, System.Windows.RoutedEventArgs? e)
        {
            int Tag = int.Parse((string)((Button)sender).Tag);

            UpdateUI_SensorPlacement(Tag);

            if (IsLoaded)
                SettingsManager.SetProperty("SensorPlacement", Tag);
        }

        private void UpdateUI_SensorPlacement(int? SensorPlacement)
        {
            foreach (SimpleStackPanel panel in Grid_SensorPlacementVisualisation.Children)
            {
                foreach (Button button in panel.Children)
                {
                    if (int.Parse((string)button.Tag) == SensorPlacement)
                        button.Background = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
                    else
                        button.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltBaseLowBrush"];
                }
            }
        }
        private void Toggle_SensorPlacementUpsideDown_Toggled(object? sender, System.Windows.RoutedEventArgs? e)
        {
            bool isUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

            if (IsLoaded)
                SettingsManager.SetProperty("SensorPlacementUpsideDown", isUpsideDown);
        }

        #region serviceManager
        private void OnServiceUpdate(ServiceControllerStatus status, int mode)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                    case ServiceControllerStatus.Running:
                    case ServiceControllerStatus.ContinuePending:
                    case ServiceControllerStatus.PausePending:
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.StopPending:
                        cB_StartupType.IsEnabled = true;
                        break;
                    default:
                        cB_StartupType.IsEnabled = false;
                        break;
                }

                if (mode != -1)
                {
                    ServiceStartMode serviceMode = (ServiceStartMode)mode;
                    switch (serviceMode)
                    {
                        case ServiceStartMode.Automatic:
                            cB_StartupType.SelectedIndex = 0;
                            break;
                        default:
                        case ServiceStartMode.Manual:
                            cB_StartupType.SelectedIndex = 1;
                            break;
                        case ServiceStartMode.Disabled:
                            cB_StartupType.SelectedIndex = 2;
                            break;
                    }

                    // only allow users to set those options when service mode is set to Manual
                    Toggle_ServiceStartup.IsEnabled = (serviceMode != ServiceStartMode.Automatic);
                    Toggle_ServiceShutdown.IsEnabled = (serviceMode != ServiceStartMode.Automatic);
                }
            });
        }
        #endregion

        private async void Toggle_FanControl_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            if (Toggle_FanControl.IsOn)
            {
                // todo: localize me !
                Task<ContentDialogResult> result = Dialog.ShowAsync(
                    "Warning",
                    "Altering fan duty cycle might cause instabilities and overheating. It might also trigger anti cheat systems and get you banned. Product warranties may not apply if you operate your device beyond its specifications. Use at your own risk.",
                    ContentDialogButton.Primary, "Cancel", Properties.Resources.ProfilesPage_OK);

                await result; // sync call

                switch (result.Result)
                {
                    case ContentDialogResult.Primary:
                        break;
                    default:
                    case ContentDialogResult.None:
                        // restore previous state
                        Toggle_FanControl.IsOn = false;
                        return;
                }
            }

            SettingsManager.SetProperty("QuietModeEnabled", Toggle_FanControl.IsOn);
        }

        private void FanDutyCycle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = FanDutyCycle.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuietModeDuty", value);
        }
    }
}
