using ControllerCommon;
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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Foundation;
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
                cB_StartupType.Items.Add(EnumUtils.GetDescriptionFromEnumValue(mode));

            cB_Language.Items.Add(new CultureInfo("en-US"));
            cB_Language.Items.Add(new CultureInfo("fr-FR"));
            cB_Language.Items.Add(new CultureInfo("zh-CN"));
            cB_Language.Items.Add(new CultureInfo("zh-Hant"));

            // call function
            UpdateDevice(null);

            // initialize manager(s)
            MainWindow.serviceManager.Updated += OnServiceUpdate;
            MainWindow.updateManager.Updated += UpdateManager_Updated;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        public SettingsPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            this.Dispatcher.Invoke(() =>
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
                        cB_SensorSelection.SelectedIndex = Convert.ToInt32(value);
                        cB_SensorSelection_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
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
                }
            });
        }

        public void UpdateDevice(PnPDevice device)
        {
            this.Dispatcher.Invoke(() =>
            {
                SensorInternal.IsEnabled = MainWindow.handheldDevice.hasInternal;
                SensorExternal.IsEnabled = MainWindow.handheldDevice.hasExternal;
            });
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
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("RunAtStartup", Toggle_AutoStart.IsOn);
        }

        private void Toggle_Background_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("StartMinimized", Toggle_Background.IsOn);
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

            SettingsManager.SetProperty("ServiceStartMode", cB_StartupType.SelectedIndex);
        }

        private void Toggle_CloseMinimizes_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("CloseMinimises", Toggle_CloseMinimizes.IsOn);
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
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("HaltServiceWithCompanion", Toggle_ServiceShutdown.IsOn);
        }

        private void Toggle_ServiceStartup_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("StartServiceWithCompanion", Toggle_ServiceStartup.IsOn);
        }

        private void cB_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CultureInfo culture = (CultureInfo)cB_Language.SelectedItem;

            if (culture is null)
                return;

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("CurrentCulture", culture.Name);

            // prevent message from being displayed again...
            if (culture.Name == CultureInfo.CurrentCulture.Name)
                return;

            _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_AppLanguageWarning}",
                Properties.Resources.SettingsPage_AppLanguageWarningDesc,
                ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
        }

        private void Toggle_Notification_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("ToastEnable", Toggle_Notification.IsOn);
        }

        private void cB_Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_Theme.SelectedIndex == -1)
                return;

            ThemeManager.Current.ApplicationTheme = (ApplicationTheme)cB_Theme.SelectedIndex;

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("MainWindowTheme", cB_Theme.SelectedIndex);
        }

        private void cB_Backdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("MainWindowBackdrop", cB_Backdrop.SelectedIndex);
        }

        private async void Toggle_cTDP_Toggled(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("ConfigurableTDPOverride", Toggle_cTDP.IsOn);
            SettingsManager.SetProperty("ConfigurableTDPOverrideUp", NumberBox_TDPMax.Value);
            SettingsManager.SetProperty("ConfigurableTDPOverrideDown", NumberBox_TDPMin.Value);

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
        }

        private void NumberBox_TDPMax_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            double value = NumberBox_TDPMax.Value;
            if (double.IsNaN(value))
                return;

            NumberBox_TDPMin.Maximum = value;

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("ConfigurableTDPOverrideUp", value);
        }

        private void NumberBox_TDPMin_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            double value = NumberBox_TDPMin.Value;
            if (double.IsNaN(value))
                return;

            NumberBox_TDPMax.Minimum = value;

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("ConfigurableTDPOverrideDown", value);
        }

        private void cB_SensorSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // update dependencies
            Toggle_SensorPlacementUpsideDown.IsEnabled = cB_SensorSelection.SelectedIndex == 1 ? true : false;
            SensorPlacementVisualisation.IsEnabled = cB_SensorSelection.SelectedIndex == 1 ? true : false;

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorSelection", cB_SensorSelection.SelectedIndex);
            MainWindow.pipeClient.SendMessage(settings);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.SelectedIndex);
        }

        private void SensorPlacement_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            int Tag = int.Parse((string)((Button)sender).Tag);

            UpdateUI_SensorPlacement(Tag);

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorPlacement", Tag);
            MainWindow.pipeClient.SendMessage(settings);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("SensorPlacement", Tag);
        }

        private void UpdateUI_SensorPlacement(int SensorPlacement)
        {
            foreach (SimpleStackPanel panel in SensorPlacementVisualisation.Children)
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
        private void Toggle_SensorPlacementUpsideDown_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            bool isUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorPlacementUpsideDown", isUpsideDown);
            MainWindow.pipeClient?.SendMessage(settings);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("SensorPlacementUpsideDown", isUpsideDown);
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
