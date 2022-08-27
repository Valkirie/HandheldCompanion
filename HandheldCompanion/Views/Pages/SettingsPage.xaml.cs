using ControllerCommon;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using ModernWpf;
using ModernWpf.Controls;
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
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.Managers.ServiceControllerStatus;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private bool Initialized;
        public Dictionary<string, string> Settings = new Dictionary<string, string>();

        public event SettingValueChangedEventHandler SettingValueChanged;
        public delegate void SettingValueChangedEventHandler(string name, object value);

        public SettingsPage()
        {
            InitializeComponent();

            // initialize components
            foreach (ServiceStartMode mode in ((ServiceStartMode[])Enum.GetValues(typeof(ServiceStartMode))).Where(mode => mode >= ServiceStartMode.Automatic))
                cB_StartupType.Items.Add(EnumUtils.GetDescriptionFromEnumValue(mode));

            cB_Language.Items.Add(new CultureInfo("en-US"));
            cB_Language.Items.Add(new CultureInfo("fr-FR"));
            cB_Language.Items.Add(new CultureInfo("zh-CN"));
            cB_Language.Items.Add(new CultureInfo("zh-Hant"));

            // pull settings
            string CurrentCulture = Thread.CurrentThread.CurrentCulture.Name;
            switch (CurrentCulture)
            {
                // unsupported languages
                default:
                    cB_Language.SelectedItem = new CultureInfo("en-US");
                    break;
                // supported languages
                case "fr-FR":
                case "en-US":
                case "zh-CN":
                case "zh-Hant":
                    cB_Language.SelectedItem = new CultureInfo(CurrentCulture);
                    break;
            }

            cB_Theme.SelectedIndex = Properties.Settings.Default.MainWindowTheme;
            cB_SensorSelection.SelectedIndex = Properties.Settings.Default.SensorSelection;

            Toggle_AutoStart.IsOn = Properties.Settings.Default.RunAtStartup;
            Toggle_Background.IsOn = Properties.Settings.Default.StartMinimized;
            Toggle_CloseMinimizes.IsOn = Properties.Settings.Default.CloseMinimises;
            Toggle_Notification.IsOn = Properties.Settings.Default.ToastEnable;
            Toggle_ServiceStartup.IsOn = Properties.Settings.Default.StartServiceWithCompanion;
            Toggle_ServiceShutdown.IsOn = Properties.Settings.Default.HaltServiceWithCompanion;
            Toggle_SensorPlacementUpsideDown.IsOn = Properties.Settings.Default.SensorPlacementUpsideDown;
            Toggle_cTDP.IsOn = Properties.Settings.Default.ConfigurableTDPOverride;

            // define slider(s) min and max values based on device specifications
            var cTDPdown = Properties.Settings.Default.ConfigurableTDPOverride ? Properties.Settings.Default.ConfigurableTDPOverrideDown : MainWindow.handheldDevice.cTDP[0];
            var cTDPup = Properties.Settings.Default.ConfigurableTDPOverride ? Properties.Settings.Default.ConfigurableTDPOverrideUp : MainWindow.handheldDevice.cTDP[1];

            NumberBox_TDPMin.Value = cTDPdown;
            NumberBox_TDPMax.Value = cTDPup;

            // call function
            ApplyTheme((ApplicationTheme)cB_Theme.SelectedIndex);
            UpdateUI_SensorPlacement(Properties.Settings.Default.SensorPlacement);
            UpdateDevice(null);

            // we are ready !
            Initialized = true;

            // initialize manager(s)
            MainWindow.serviceManager.Updated += OnServiceUpdate;
            MainWindow.updateManager.Updated += UpdateManager_Updated;
        }

        public SettingsPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        public void UpdateDevice(PnPDevice device)
        {
            this.Dispatcher.Invoke(() =>
            {
                SensorInternal.IsEnabled = MainWindow.handheldDevice.hasInternal;
                SensorExternal.IsEnabled = MainWindow.handheldDevice.hasExternal;
            });

            cB_SensorSelection_SelectionChanged(null, null);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow.updateManager.Start();
        }

        public void Page_Closed()
        {
            MainWindow.serviceManager.Updated -= OnServiceUpdate;
        }

        private void Toggle_AutoStart_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("autostart", Toggle_AutoStart.IsOn);

            if (!Initialized)
                return;

            Properties.Settings.Default.RunAtStartup = Toggle_AutoStart.IsOn;
            Properties.Settings.Default.Save();
        }

        private void Toggle_Background_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("start_minimized", Toggle_Background.IsOn);

            if (!Initialized)
                return;

            Properties.Settings.Default.StartMinimized = Toggle_Background.IsOn;
            Properties.Settings.Default.Save();
        }

        private void cB_StartupType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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

            // only allow users to set those options when service mode is set to Manual
            Toggle_ServiceStartup.IsEnabled = (mode != ServiceStartMode.Automatic);
            Toggle_ServiceShutdown.IsEnabled = (mode != ServiceStartMode.Automatic);

            // service was not found
            if (!cB_StartupType.IsEnabled)
                return;

            // raise event
            SettingValueChanged?.Invoke("service_startup_type", mode);
        }

        private void Toggle_CloseMinimizes_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("close_minimizes", Toggle_CloseMinimizes.IsOn);

            if (!Initialized)
                return;

            Properties.Settings.Default.CloseMinimises = Toggle_CloseMinimizes.IsOn;
            Properties.Settings.Default.Save();
        }

        private void UpdateManager_Updated(UpdateStatus status, UpdateFile updateFile, object value)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case UpdateStatus.Failed: // lazy ?
                    case UpdateStatus.Updated:
                    case UpdateStatus.Initialized:
                        {
                            if (updateFile != null)
                            {
                                updateFile.updateDownload.Visibility = Visibility.Visible;

                                updateFile.updatePercentage.Visibility = Visibility.Collapsed;
                                updateFile.updateInstall.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                LabelUpdate.Content = Properties.Resources.SettingsPage_UpToDate;
                                LabelUpdateDate.Content = Properties.Resources.SettingsPage_LastChecked + MainWindow.updateManager.GetTime();

                                LabelUpdateDate.Visibility = Visibility.Visible;
                                GridUpdateSymbol.Visibility = Visibility.Visible;
                                ProgressBarUpdate.Visibility = Visibility.Collapsed;
                                B_CheckUpdate.IsEnabled = true;
                            }
                        }
                        break;

                    case UpdateStatus.CheckingATOM:
                        {
                            LabelUpdate.Content = Properties.Resources.SettingsPage_UpdateCheck;

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
                            LabelUpdate.Content = Properties.Resources.SettingsPage_UpdateAvailable;

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

        private void B_CheckUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            MainWindow.updateManager.StartProcess();
        }

        private void Toggle_ServiceShutdown_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("haltservice_onclose", Toggle_ServiceShutdown.IsOn);

            if (!Initialized)
                return;

            Properties.Settings.Default.HaltServiceWithCompanion = Toggle_ServiceShutdown.IsOn;
            Properties.Settings.Default.Save();
        }

        private void Toggle_ServiceStartup_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("startservice_onstart", Toggle_ServiceStartup.IsOn);

            if (!Initialized)
                return;

            Properties.Settings.Default.StartServiceWithCompanion = Toggle_ServiceStartup.IsOn;
            Properties.Settings.Default.Save();
        }

        private void cB_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CultureInfo culture = (CultureInfo)cB_Language.SelectedItem;

            // raise event
            SettingValueChanged?.Invoke("language", culture.Name);

            if (!Initialized)
                return;

            if (cB_Language.SelectedItem == null)
                return;

            // skip if setting is identical to current
            if (culture.Name == Properties.Settings.Default.CurrentCulture)
                return;

            Properties.Settings.Default.CurrentCulture = culture.Name;
            Properties.Settings.Default.Save();

            _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_AppLanguageWarning}",
                Properties.Resources.SettingsPage_AppLanguageWarningDesc,
                ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
        }

        private void Toggle_Notification_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("toast_notification", Toggle_Notification.IsOn);

            if (!Initialized)
                return;

            Properties.Settings.Default.ToastEnable = Toggle_Notification.IsOn;
            Properties.Settings.Default.Save();
        }

        private void cB_Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("theme", cB_Theme.SelectedIndex);

            if (!Initialized)
                return;

            if (cB_Theme.SelectedIndex == -1)
                return;

            // skip if setting is identical to current
            if (cB_Theme.SelectedIndex == Properties.Settings.Default.MainWindowTheme)
                return;

            Properties.Settings.Default.MainWindowTheme = cB_Theme.SelectedIndex;
            Properties.Settings.Default.Save();

            ApplyTheme((ApplicationTheme)cB_Theme.SelectedIndex);
        }

        private async void Toggle_cTDP_Toggled(object sender, RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("configurabletdp_override", Toggle_cTDP.IsOn);

            if (!Initialized)
                return;

            if (Toggle_cTDP.IsOn)
            {
                // todo: localize me !
                Task<ContentDialogResult> result = Dialog.ShowAsync(
                    "Warning",
                    "Altering minimum and maximum CPU power values might cause instabilities. Product warranties may not apply if the processor is operated beyond its specifications.",
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

            Properties.Settings.Default.ConfigurableTDPOverride = Toggle_cTDP.IsOn;
            Properties.Settings.Default.Save();
        }

        private void NumberBox_TDPMax_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            double value = NumberBox_TDPMax.Value;
            if (double.IsNaN(value))
                return;

            // raise event
            SettingValueChanged?.Invoke("configurabletdp_up", value);

            if (!Initialized)
                return;

            Properties.Settings.Default.ConfigurableTDPOverrideUp = value;
            Properties.Settings.Default.Save();
        }

        private void NumberBox_TDPMin_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            double value = NumberBox_TDPMin.Value;
            if (double.IsNaN(value))
                return;

            // raise event
            SettingValueChanged?.Invoke("configurabletdp_down", value);

            if (!Initialized)
                return;

            Properties.Settings.Default.ConfigurableTDPOverrideDown = value;
            Properties.Settings.Default.Save();
        }

        public void ApplyTheme(ApplicationTheme Theme)
        {
            ThemeManager.Current.ApplicationTheme = Theme;
        }

        private void cB_SensorSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("sensor_selection", cB_SensorSelection.SelectedIndex);

            if (cB_SensorSelection.SelectedIndex == -1)
                return;

            Toggle_SensorPlacementUpsideDown.IsEnabled = cB_SensorSelection.SelectedIndex == 1 ? true : false;
            SensorPlacementVisualisation.IsEnabled = cB_SensorSelection.SelectedIndex == 1 ? true : false;

            if (!Initialized)
                return;

            // skip if setting is identical to current
            if (cB_SensorSelection.SelectedIndex == Properties.Settings.Default.SensorSelection)
                return;

            // save settings
            Properties.Settings.Default.SensorSelection = cB_SensorSelection.SelectedIndex;
            Properties.Settings.Default.Save();

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorSelection", cB_SensorSelection.SelectedIndex);
            MainWindow.pipeClient.SendMessage(settings);
        }

        private void SensorPlacement_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            int Tag = int.Parse((string)((Button)sender).Tag);

            // raise event
            SettingValueChanged?.Invoke("sensor_placement", Tag);

            UpdateUI_SensorPlacement(Tag);

            // save settings
            Properties.Settings.Default.SensorPlacement = Tag;
            Properties.Settings.Default.Save();

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorPlacement", Tag);
            MainWindow.pipeClient.SendMessage(settings);
        }

        private void UpdateUI_SensorPlacement(int SensorPlacement)
        {
            foreach (SimpleStackPanel panel in SensorPlacementVisualisation.Children)
                foreach (Button button in panel.Children)
                {
                    if (int.Parse((string)button.Tag) == SensorPlacement)
                        button.Background = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
                    else
                        button.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltBaseLowBrush"];
                }

        }
        private void Toggle_SensorPlacementUpsideDown_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            // raise event
            SettingValueChanged?.Invoke("sensor_upsidedown", Toggle_SensorPlacementUpsideDown.IsOn);

            if (!Initialized)
                return;

            Properties.Settings.Default.SensorPlacementUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;
            Properties.Settings.Default.Save();

            bool isUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorPlacementUpsideDown", isUpsideDown);
            MainWindow.pipeClient?.SendMessage(settings);
        }

        #region serviceManager
        private void OnServiceUpdate(ServiceControllerStatus status, int mode)
        {
            this.Dispatcher.Invoke(() =>
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
                }
            });
        }
        #endregion
    }
}
