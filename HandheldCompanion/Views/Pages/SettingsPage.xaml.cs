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

        // settings vars
        public bool ToastEnable, RunAtStartup, StartMinimized, CloseMinimises, StartServiceWithCompanion, HaltServiceWithCompanion, SensorPlacementUpsideDown;
        public int ApplicationTheme, ServiceStartup, SensorSelection;

        private UpdateManager updateManager;

        public event SettingValueChangedEventHandler SettingValueChanged;
        public delegate void SettingValueChangedEventHandler(string name, object value);

        public SettingsPage()
        {
            InitializeComponent();
            Initialized = true;

            Toggle_AutoStart.IsOn = RunAtStartup = Properties.Settings.Default.RunAtStartup;
            Toggle_Background.IsOn = StartMinimized = Properties.Settings.Default.StartMinimized;
            Toggle_CloseMinimizes.IsOn = CloseMinimises = Properties.Settings.Default.CloseMinimises;

            cB_Theme.SelectedIndex = ApplicationTheme = Properties.Settings.Default.MainWindowTheme;

            Toggle_Notification.IsOn = ToastEnable = Properties.Settings.Default.ToastEnable;

            Toggle_ServiceStartup.IsOn = StartServiceWithCompanion = Properties.Settings.Default.StartServiceWithCompanion;
            Toggle_ServiceShutdown.IsOn = HaltServiceWithCompanion = Properties.Settings.Default.HaltServiceWithCompanion;

            cB_SensorSelection.SelectedIndex = SensorSelection = Properties.Settings.Default.SensorSelection;
            var SensorPlacement = Properties.Settings.Default.SensorPlacement;
            UpdateUI_SensorPlacement(SensorPlacement);
            Toggle_SensorPlacementUpsideDown.IsOn = SensorPlacementUpsideDown = Properties.Settings.Default.SensorPlacementUpsideDown;

            // initialize update manager
            updateManager = new UpdateManager();
            updateManager.Updated += UpdateManager_Updated;
        }

        public SettingsPage(string Tag) : this()
        {
            this.Tag = Tag;

            // initialize manager(s)
            MainWindow.serviceManager.Updated += OnServiceUpdate;

            foreach (ServiceStartMode mode in ((ServiceStartMode[])Enum.GetValues(typeof(ServiceStartMode))).Where(mode => mode >= ServiceStartMode.Automatic))
                cB_StartupType.Items.Add(EnumUtils.GetDescriptionFromEnumValue(mode));

            // supported languages
            cB_Language.Items.Add(new CultureInfo("en-US"));
            cB_Language.Items.Add(new CultureInfo("fr-FR"));
            cB_Language.Items.Add(new CultureInfo("zh-CN"));
            cB_Language.Items.Add(new CultureInfo("zh-Hant"));

            string CurrentCulture = Thread.CurrentThread.CurrentCulture.Name;
            switch (CurrentCulture)
            {
                default:
                    cB_Language.SelectedItem = new CultureInfo("en-US");
                    break;
                case "fr-FR":
                case "en-US":
                case "zh-CN":
                case "zh-Hant":
                    cB_Language.SelectedItem = new CultureInfo(CurrentCulture);
                    break;
            }
            cB_Language_SelectionChanged(null, null);

            cB_Theme.SelectedIndex = Properties.Settings.Default.MainWindowTheme;
            ApplyTheme((ApplicationTheme)cB_Theme.SelectedIndex);

            // call functions
            UpdateDevice(null);
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
            updateManager.Start();
        }

        public void Page_Closed()
        {
            MainWindow.serviceManager.Updated -= OnServiceUpdate;
        }

        private void Toggle_AutoStart_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            Properties.Settings.Default.RunAtStartup = Toggle_AutoStart.IsOn;
            Properties.Settings.Default.Save();

            RunAtStartup = Toggle_AutoStart.IsOn;

            // warn setting has changed
            SettingValueChanged?.Invoke("autostart", Properties.Settings.Default.RunAtStartup);
        }

        private void Toggle_Background_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            Properties.Settings.Default.StartMinimized = Toggle_Background.IsOn;
            Properties.Settings.Default.Save();

            StartMinimized = Toggle_Background.IsOn;

            // warn setting has changed
            SettingValueChanged?.Invoke("start_minimized", Properties.Settings.Default.StartMinimized);
        }

        private void cB_StartupType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Initialized)
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

            // only allow users to set those options when service mode is set to Manual
            Toggle_ServiceStartup.IsEnabled = (mode == ServiceStartMode.Manual);
            Toggle_ServiceShutdown.IsEnabled = (mode == ServiceStartMode.Manual);

            // warn setting has changed
            SettingValueChanged?.Invoke("service_startup_type", mode);
        }

        private void Toggle_CloseMinimizes_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            Properties.Settings.Default.CloseMinimises = Toggle_CloseMinimizes.IsOn;
            Properties.Settings.Default.Save();

            CloseMinimises = Toggle_CloseMinimizes.IsOn;

            // warn setting has changed
            SettingValueChanged?.Invoke("close_minimizes", Properties.Settings.Default.CloseMinimises);
        }

        private void UpdateManager_Updated(UpdateStatus status, UpdateFile updateFile, object value)
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
                            LabelUpdateDate.Content = Properties.Resources.SettingsPage_LastChecked + updateManager.GetTime();

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
                                updateManager.DownloadUpdateFile(update);
                            };

                            // Set button action
                            update.updateInstall.Click += (sender, e) =>
                            {
                                updateManager.InstallUpdate(update);
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
        }

        private void B_CheckUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            updateManager.StartProcess();
        }

        private void B_InstallUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            updateManager.InstallUpdate(null);
        }

        private void Toggle_ServiceShutdown_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            Properties.Settings.Default.HaltServiceWithCompanion = Toggle_ServiceShutdown.IsOn;
            Properties.Settings.Default.Save();

            HaltServiceWithCompanion = Toggle_ServiceShutdown.IsOn;

            // warn setting has changed
            SettingValueChanged?.Invoke("haltservice_onclose", Properties.Settings.Default.HaltServiceWithCompanion);
        }

        private void Toggle_ServiceStartup_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            Properties.Settings.Default.StartServiceWithCompanion = Toggle_ServiceStartup.IsOn;
            Properties.Settings.Default.Save();

            StartServiceWithCompanion = Toggle_ServiceStartup.IsOn;

            // warn setting has changed
            SettingValueChanged?.Invoke("startservice_onstart", Properties.Settings.Default.StartServiceWithCompanion);
        }

        private void cB_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Initialized)
                return;

            if (cB_Language.SelectedItem == null)
                return;

            // skip if setting is identical to current
            CultureInfo culture = (CultureInfo)cB_Language.SelectedItem;
            if (culture.Name == Properties.Settings.Default.CurrentCulture)
                return;

            Properties.Settings.Default.CurrentCulture = culture.Name;
            Properties.Settings.Default.Save();

            Dialog.ShowAsync($"{Properties.Resources.SettingsPage_AppLanguageWarning}",
                Properties.Resources.SettingsPage_AppLanguageWarningDesc,
                ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");

            // warn setting has changed
            SettingValueChanged?.Invoke("language", Properties.Settings.Default.CurrentCulture);
        }

        private void Toggle_Notification_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            Properties.Settings.Default.ToastEnable = Toggle_Notification.IsOn;
            Properties.Settings.Default.Save();

            ToastEnable = Toggle_Notification.IsOn;

            // warn setting has changed
            SettingValueChanged?.Invoke("toast_notification", Properties.Settings.Default.ToastEnable);
        }

        private void cB_Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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

            // warn setting has changed
            SettingValueChanged?.Invoke("theme", Properties.Settings.Default.MainWindowTheme);
        }

        public void ApplyTheme(ApplicationTheme Theme)
        {
            ThemeManager.Current.ApplicationTheme = Theme;
        }

        private void Scrolllock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = false;
        }

        private void cB_SensorSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Initialized)
                return;

            if (cB_SensorSelection.SelectedIndex == -1)
                return;

            Toggle_SensorPlacementUpsideDown.IsEnabled = cB_SensorSelection.SelectedIndex == 1 ? true : false;

            foreach (SimpleStackPanel panel in SensorPlacementVisualisation.Children)
                foreach (Button button in panel.Children)
                    button.IsEnabled = cB_SensorSelection.SelectedIndex == 1 ? true : false;

            // skip if setting is identical to current, but do perform enabling of buttons above
            if (cB_SensorSelection.SelectedIndex == Properties.Settings.Default.SensorSelection)
                return;

            // save settings
            Properties.Settings.Default.SensorSelection = cB_SensorSelection.SelectedIndex;
            Properties.Settings.Default.Save();

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorSelection", cB_SensorSelection.SelectedIndex);
            MainWindow.pipeClient?.SendMessage(settings);

            Dialog.ShowAsync($"{Properties.Resources.SettingsPage_AppLanguageWarning}",
                Properties.Resources.SettingsPage_AppLanguageWarningDesc,
                ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");

            // warn setting has changed
            SettingValueChanged?.Invoke("sensor_selection", Properties.Settings.Default.SensorSelection);

        }
        private void SensorPlacement_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            int Tag = int.Parse((string)((Button)sender).Tag);

            UpdateUI_SensorPlacement(Tag);

            // save settings
            Properties.Settings.Default.SensorPlacement = Tag;
            Properties.Settings.Default.Save();

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorPlacement", Tag);
            MainWindow.pipeClient?.SendMessage(settings);

            // warn setting has changed
            SettingValueChanged?.Invoke("sensor_placement", Properties.Settings.Default.SensorPlacement);
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
            if (!Initialized)
                return;

            Properties.Settings.Default.SensorPlacementUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;
            Properties.Settings.Default.Save();

            SensorPlacementUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorPlacementUpsideDown", SensorPlacementUpsideDown);
            MainWindow.pipeClient?.SendMessage(settings);

            // warn setting has changed
            SettingValueChanged?.Invoke("sensor_upsidedown", Properties.Settings.Default.SensorPlacementUpsideDown);
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
