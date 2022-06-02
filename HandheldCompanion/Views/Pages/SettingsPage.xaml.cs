using ControllerCommon;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using ModernWpf;
using ModernWpf.Controls;
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
using ServiceControllerStatus = ControllerCommon.ServiceControllerStatus;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private MainWindow mainWindow;
        private ILogger logger;
        private PipeClient pipeClient;
        private ServiceManager serviceManager;

        // settings vars
        public bool ToastEnable, RunAtStartup, StartMinimized, CloseMinimises, StartServiceWithCompanion, HaltServiceWithCompanion;
        public int ApplicationTheme, ServiceStartup;

        private UpdateManager updateManager;

        public event ToastChangedEventHandler ToastChanged;
        public delegate void ToastChangedEventHandler(bool value);

        public event AutoStartChangedEventHandler AutoStartChanged;
        public delegate void AutoStartChangedEventHandler(bool value);

        public event ServiceChangedEventHandler ServiceChanged;
        public delegate void ServiceChangedEventHandler(ServiceStartMode value);

        private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            updateManager.Start();
        }

        public SettingsPage()
        {
            InitializeComponent();

            Toggle_AutoStart.IsOn = RunAtStartup = Properties.Settings.Default.RunAtStartup;
            Toggle_Background.IsOn = StartMinimized = Properties.Settings.Default.StartMinimized;
            Toggle_CloseMinimizes.IsOn = CloseMinimises = Properties.Settings.Default.CloseMinimises;

            cB_Theme.SelectedIndex = ApplicationTheme = Properties.Settings.Default.MainWindowTheme;

            Toggle_Notification.IsOn = ToastEnable = Properties.Settings.Default.ToastEnable;

            Toggle_ServiceStartup.IsOn = StartServiceWithCompanion = Properties.Settings.Default.StartServiceWithCompanion;
            Toggle_ServiceShutdown.IsOn = HaltServiceWithCompanion = Properties.Settings.Default.HaltServiceWithCompanion;

            // initialize update manager
            updateManager = new UpdateManager();
            updateManager.Updated += UpdateManager_Updated;
        }

        public SettingsPage(string Tag, MainWindow mainWindow, ILogger logger) : this()
        {
            this.Tag = Tag;
            this.mainWindow = mainWindow;
            this.logger = logger;

            this.pipeClient = mainWindow.pipeClient;
            this.serviceManager = mainWindow.serviceManager;
            this.serviceManager.Updated += OnServiceUpdate;

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
        }

        private void Toggle_AutoStart_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.RunAtStartup = Toggle_AutoStart.IsOn;
            Properties.Settings.Default.Save();

            RunAtStartup = Toggle_AutoStart.IsOn;
            AutoStartChanged?.Invoke(RunAtStartup);
        }

        private void Toggle_Background_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.StartMinimized = Toggle_Background.IsOn;
            Properties.Settings.Default.Save();

            StartMinimized = Toggle_Background.IsOn;
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
            Toggle_ServiceStartup.IsEnabled = (mode == ServiceStartMode.Manual);
            Toggle_ServiceShutdown.IsEnabled = (mode == ServiceStartMode.Manual);

            ServiceChanged?.Invoke(mode);
        }

        private void Toggle_CloseMinimizes_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.CloseMinimises = Toggle_CloseMinimizes.IsOn;
            Properties.Settings.Default.Save();

            CloseMinimises = Toggle_CloseMinimizes.IsOn;
        }

        private Grid updateGrid;
        private TextBlock updateFilename;
        private TextBlock updatePercentage;
        private Button updateDownload;
        private Button updateInstall;

        private void UpdateManager_Updated(UpdateStatus status, UpdateFile updateFile, object value)
        {
            if (updateFile != null)
            {
                updateGrid = GetUpdateGrid(updateFile);

                if (updateGrid != null)
                {
                    updateFilename = (TextBlock)updateGrid.Children[0];
                    updatePercentage = (TextBlock)updateGrid.Children[1];
                    updateDownload = (Button)updateGrid.Children[2];
                    updateInstall = (Button)updateGrid.Children[3];
                }
            }

            switch (status)
            {
                case UpdateStatus.Failed: // lazy ?
                case UpdateStatus.Updated:
                case UpdateStatus.Initialized:
                    {
                        if (updateFile != null)
                        {
                            updateDownload.Visibility = Visibility.Visible;

                            updatePercentage.Visibility = Visibility.Collapsed;
                            updateInstall.Visibility = Visibility.Collapsed;
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
                            SetUpdateGrid(update);
                    }
                    break;

                case UpdateStatus.Download:
                    {
                        updateDownload.Visibility = Visibility.Collapsed;
                        updatePercentage.Visibility = Visibility.Visible;
                    }
                    break;

                case UpdateStatus.Downloading:
                    {
                        int progress = (int)value;
                        updatePercentage.Text = Properties.Resources.SettingsPage_DownloadingPercentage + $"{value} %";
                    }
                    break;

                case UpdateStatus.Downloaded:
                    {
                        updateInstall.Visibility = Visibility.Visible;

                        updateDownload.Visibility = Visibility.Collapsed;
                        updatePercentage.Visibility = Visibility.Collapsed;
                    }
                    break;
            }
        }

        private void SetUpdateGrid(UpdateFile update)
        {
            Border updateBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Background = (Brush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
                Tag = update.filename
            };

            // Create Grid
            updateGrid = new();

            // Define the Columns
            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(5, GridUnitType.Star),
                MinWidth = 200
            };
            updateGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(3, GridUnitType.Star),
                MinWidth = 120
            };
            updateGrid.ColumnDefinitions.Add(colDef2);

            // Create TextBlock
            updateFilename = new TextBlock()
            {
                FontSize = 14,
                Text = update.filename,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(updateFilename, 0);
            updateGrid.Children.Add(updateFilename);

            // Create TextBlock
            updatePercentage = new TextBlock()
            {
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(updatePercentage, 1);
            updateGrid.Children.Add(updatePercentage);

            // Create Download Button
            updateDownload = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_Download,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Set download button action
            updateDownload.Click += (sender, e) =>
            {
                updateManager.DownloadUpdateFile(update);
            };

            Grid.SetColumn(updateDownload, 1);
            updateGrid.Children.Add(updateDownload);

            // Create Install Button
            updateInstall = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_InstallNow,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };

            // Set button action
            updateInstall.Click += (sender, e) =>
            {
                updateManager.InstallUpdate(update);
            };

            Grid.SetColumn(updateInstall, 1);
            updateGrid.Children.Add(updateInstall);

            updateBorder.Child = updateGrid;
            CurrentUpdates.Children.Add(updateBorder);
        }

        private Grid GetUpdateGrid(UpdateFile update)
        {
            Border updateBorder = (Border)CurrentUpdates.Children[update.idx];
            return (Grid)updateBorder.Child;
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
            Properties.Settings.Default.HaltServiceWithCompanion = Toggle_ServiceShutdown.IsOn;
            Properties.Settings.Default.Save();

            HaltServiceWithCompanion = Toggle_ServiceShutdown.IsOn;
        }

        private void Toggle_ServiceStartup_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.StartServiceWithCompanion = Toggle_ServiceStartup.IsOn;
            Properties.Settings.Default.Save();

            StartServiceWithCompanion = Toggle_ServiceStartup.IsOn;
        }

        private void cB_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
        }

        private void Toggle_Notification_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.ToastEnable = Toggle_Notification.IsOn;
            Properties.Settings.Default.Save();

            ToastEnable = Toggle_Notification.IsOn;
            ToastChanged?.Invoke(ToastEnable);
        }

        private void cB_Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_Theme.SelectedIndex == -1)
                return;

            // skip if setting is identical to current
            if (cB_Theme.SelectedIndex == Properties.Settings.Default.MainWindowTheme)
                return;

            Properties.Settings.Default.MainWindowTheme = cB_Theme.SelectedIndex;
            Properties.Settings.Default.Save();

            ApplyTheme((ApplicationTheme)cB_Theme.SelectedIndex);
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
