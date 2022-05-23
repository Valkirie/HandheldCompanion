using ControllerCommon;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using ModernWpf;
using ModernWpf.Controls;
using System;
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
        public bool ToastEnable, RunAtStartup, StartMinimized, CloseMinimises, StartServiceWithCompanion, HaltServiceWithCompanion, SensorPlacementUpsideDown;
        public int ApplicationTheme, ServiceStartup, SensorSelection;

        private UpdateManager updateManager;

        public event ToastChangedEventHandler ToastChanged;
        public delegate void ToastChangedEventHandler(bool value);

        public event AutoStartChangedEventHandler AutoStartChanged;
        public delegate void AutoStartChangedEventHandler(bool value);

        public event ServiceChangedEventHandler ServiceChanged;
        public delegate void ServiceChangedEventHandler(ServiceStartMode value);

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

            cB_SensorSelection.SelectedIndex = SensorSelection = Properties.Settings.Default.SensorSelection;
            var SensorPlacement = Properties.Settings.Default.SensorPlacement;
            UpdateUI_SensorPlacement(SensorPlacement);
            Toggle_SensorPlacementUpsideDown.IsOn = SensorPlacementUpsideDown = Properties.Settings.Default.SensorPlacementUpsideDown;

            // initialize update manager
            updateManager = new UpdateManager();
            updateManager.Updated += (status, value) =>
            {
                switch (status)
                {
                    case UpdateStatus.Failed: // lazy ?
                    case UpdateStatus.Updated:
                    case UpdateStatus.Initialized:
                        LabelUpdate.Content = Properties.Resources.SettingsPage_UpToDate;
                        LabelUpdateDate.Content = Properties.Resources.SettingsPage_LastChecked + updateManager.GetTime();

                        LabelUpdateDate.Visibility = System.Windows.Visibility.Visible;
                        GridUpdateSymbol.Visibility = System.Windows.Visibility.Visible;
                        ProgressBarUpdate.Visibility = System.Windows.Visibility.Collapsed;
                        B_CheckUpdate.IsEnabled = true;
                        break;

                    case UpdateStatus.CheckingATOM:
                        LabelUpdate.Content = Properties.Resources.SettingsPage_UpdateCheck;

                        GridUpdateSymbol.Visibility = System.Windows.Visibility.Collapsed;
                        LabelUpdateDate.Visibility = System.Windows.Visibility.Collapsed;
                        ProgressBarUpdate.Visibility = System.Windows.Visibility.Visible;
                        B_CheckUpdate.IsEnabled = false;
                        break;

                    case UpdateStatus.Ready:
                        UpdateFile updateFile = (UpdateFile)value;
                        LabelUpdate.Content = Properties.Resources.SettingsPage_UpdateAvailable;
                        LabelUpdateName.Text = updateFile.filename;

                        CurrentUpdate.Visibility = System.Windows.Visibility.Visible;
                        break;

                    case UpdateStatus.Downloading:
                        LabelUpdatePercentage.Text = Properties.Resources.SettingsPage_DownloadingPercentage + $"{value} %";
                        break;

                    case UpdateStatus.Downloaded:
                        ProgressBarUpdate.Visibility = System.Windows.Visibility.Collapsed;
                        LabelUpdatePercentage.Visibility = System.Windows.Visibility.Hidden;
                        ButtonInstall.Visibility = System.Windows.Visibility.Visible;
                        break;
                }
            };
        }

        public SettingsPage(string Tag, MainWindow mainWindow, ILogger logger) : this()
        {
            this.Tag = Tag;
            this.mainWindow = mainWindow;
            this.logger = logger;

            this.pipeClient = mainWindow.pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;

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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            updateManager.Start();
        }

        public void Page_Closed()
        {
            pipeClient.ServerMessage -= OnServerMessage;
            serviceManager.Updated -= OnServiceUpdate;
        }

        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_CONTROLLER:
                    PipeServerHandheld handheldDevice = (PipeServerHandheld)message;
                    this.Dispatcher.Invoke(() =>
                    {
                        SensorInternal.IsEnabled = handheldDevice.hasInternal;
                        SensorExternal.IsEnabled = handheldDevice.hasExternal;
                        Toggle_SensorPlacementUpsideDown.IsEnabled = handheldDevice.hasExternal;
                        
                        foreach (SimpleStackPanel panel in SensorPlacementVisualisation.Children)
                            foreach (Button button in panel.Children)
                            {
                                button.IsEnabled = handheldDevice.hasExternal;
                            }
                    });
                    break;
            }
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

        private void B_CheckUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            updateManager.StartProcess();
        }

        private void B_InstallUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            updateManager.InstallUpdate();
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

        private void cB_SensorSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_SensorSelection.SelectedIndex == -1)
                return;

            // skip if setting is identical to current
            if (cB_SensorSelection.SelectedIndex == Properties.Settings.Default.SensorSelection)
                return;

            // save settings
            Properties.Settings.Default.SensorSelection = cB_SensorSelection.SelectedIndex;
            Properties.Settings.Default.Save();

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorSelection", cB_SensorSelection.SelectedIndex);
            pipeClient?.SendMessage(settings);

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
            pipeClient?.SendMessage(settings);
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
            Properties.Settings.Default.SensorPlacementUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;
            Properties.Settings.Default.Save();

            SensorPlacementUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

            // inform service
            PipeClientSettings settings = new PipeClientSettings("SensorPlacementUpsideDown", SensorPlacementUpsideDown);
            pipeClient?.SendMessage(settings);
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
