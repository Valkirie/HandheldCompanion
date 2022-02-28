using ControllerCommon;
using ControllerHelperWPF.Views.Pages;
using Microsoft.Extensions.Logging;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Windows.System.Diagnostics;
using WPFUI.Tray;
using Page = System.Windows.Controls.Page;
using Timer = System.Timers.Timer;

namespace ControllerHelperWPF.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger microsoftLogger;
        private StartupEventArgs arguments;

        // process vars
        private Timer MonitorTimer;
        private uint CurrentProcess;
        private object updateLock = new();

        // page vars
        private Dictionary<string, Page> _pages = new();
        private string preNavItemTag;

        public ControllerPage controllerPage;
        public ProfilesPage profilesPage;
        public SettingsPage settingsPage;

        // touchscroll vars
        Point scrollPoint = new Point();
        double scrollOffset = 1;
        public static bool scrollLock = false;

        // connectivity vars
        public PipeClient pipeClient;
        public PipeServer pipeServer;

        public CmdParser cmdParser;
        public MouseHook mouseHook;
        public ToastManager toastManager;

        public ServiceManager serviceManager;
        public ProfileManager profileManager;

        private WindowState prevWindowState;
        private NotifyIcon notifyIcon;

        public string CurrentExe, CurrentPath, CurrentPathService, CurrentPathProfiles, CurrentPathLogs;
        private bool IsElevated, FirstStart, appClosing;

        public MainWindow(StartupEventArgs arguments, ILogger microsoftLogger)
        {
            InitializeComponent();

            this.microsoftLogger = microsoftLogger;
            this.arguments = arguments;

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;

            // initialize log
            microsoftLogger.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

            // initialize notifyIcon
            notifyIcon = new()
            {
                Parent = this,
                Tooltip = "Tooltip test",
                // ContextMenu = NotifyIconMenu,
                Icon = this.Icon,
                // Click = icon => { RaiseEvent(new RoutedEventArgs(NotifyIconClickEvent, this)); },
                DoubleClick = icon => { NotifyIconDoubleClick(this); }
            };

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");
            CurrentPathLogs = Path.Combine(CurrentPath, "Logs");

            // settings
            IsElevated = Utils.IsAdministrator();

            // initialize title
            this.Title += $" ({fileVersionInfo.FileVersion})";
            navView.PaneTitle = IsElevated ? Properties.Resources.Administrator : Properties.Resources.User;

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathService))
            {
                microsoftLogger.LogCritical("Controller Service executable is missing");
                throw new InvalidOperationException();
            }

            // initialize pipe client
            pipeClient = new PipeClient("ControllerService", microsoftLogger);
            pipeClient.ServerMessage += OnServerMessage;
            pipeClient.Connected += OnClientConnected;

            // initialize pipe server
            pipeServer = new PipeServer("ControllerHelper", microsoftLogger);
            pipeServer.ClientMessage += OnClientMessage;

            // initialize Profile Manager
            profileManager = new ProfileManager(CurrentPathProfiles, microsoftLogger, pipeClient);

            // initialize command parser
            cmdParser = new CmdParser(pipeClient, this, microsoftLogger);

            // initialize mouse hook
            mouseHook = new MouseHook(pipeClient, microsoftLogger);

            // initialize toast manager
            toastManager = new ToastManager("ControllerService");

            // initialize Service Manager
            serviceManager = new ServiceManager("ControllerService", Properties.Resources.ServiceName, Properties.Resources.ServiceDescription, microsoftLogger);
            serviceManager.Updated += OnServiceUpdate;

            // initialize pages
            controllerPage = new ControllerPage("controller", this, microsoftLogger);
            profilesPage = new ProfilesPage("profiles", this, microsoftLogger);
            settingsPage = new SettingsPage("settings", this, microsoftLogger);

            _pages.Add("ControllerPage", controllerPage);
            _pages.Add("ProfilesPage", profilesPage);
            _pages.Add("AboutPage", profilesPage); // todo

            if (!IsElevated)
            {
                foreach (NavigationViewItem item in navView.FooterMenuItems)
                    item.ToolTip = Properties.Resources.WarningElevated;
            }
        }

        private void OnClientConnected(object sender)
        {
            // send default profile to Service
            pipeClient.SendMessage(new PipeClientProfile() { profile = profileManager.GetDefault() });

            // start processes monitor
            MonitorTimer = new Timer(1000) { Enabled = true, AutoReset = true };
            MonitorTimer.Elapsed += MonitorHelper;
        }

        private void MonitorHelper(object? sender, System.Timers.ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                uint processId;
                string name = string.Empty;
                string exec = string.Empty;
                string path = string.Empty;

                ProcessDiagnosticInfo process = new FindHostedProcess().Process;
                if (process == null)
                    return;

                processId = process.ProcessId;

                if (processId != CurrentProcess)
                {
                    Process proc = Process.GetProcessById((int)processId);
                    path = Utils.GetPathToApp(proc);
                    exec = process.ExecutableFileName;

                    if (process.IsPackaged)
                    {
                        var apps = process.GetAppDiagnosticInfos();
                        if (apps.Count > 0)
                            name = apps.First().AppInfo.DisplayInfo.DisplayName;
                        else
                            name = Path.GetFileNameWithoutExtension(exec);
                    }
                    else
                        name = Path.GetFileNameWithoutExtension(exec);

                    UpdateProcess((int)processId, path, name);

                    CurrentProcess = processId;
                }
            }
        }

        public void UpdateProcess(int ProcessId, string ProcessPath, string ProcessName)
        {
            try
            {
                string ProcessExec = Path.GetFileNameWithoutExtension(ProcessPath);

                if (profileManager.profiles.ContainsKey(ProcessExec))
                {
                    Profile profile = profileManager.profiles[ProcessExec];
                    profile.fullpath = ProcessPath;

                    profileManager.UpdateProfile(profile);

                    pipeClient.SendMessage(new PipeClientProfile { profile = profile });

                    microsoftLogger.LogInformation("Profile {0} applied", profile.name);
                }
                else
                    pipeClient.SendMessage(new PipeClientProfile());
            }
            catch (Exception) { }
        }

        private void NotifyIconDoubleClick(MainWindow mainWindow)
        {
            WindowState = prevWindowState;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // update Position and Size
            this.Height = (int)Math.Max(this.MinHeight, Properties.Settings.Default.MainWindowHeight);
            this.Width = (int)Math.Max(this.MinWidth, Properties.Settings.Default.MainWindowWidth);

            this.Left = Math.Max(0, Properties.Settings.Default.MainWindowLeft);
            this.Top = Math.Max(0, Properties.Settings.Default.MainWindowTop);

            // improve me
            WindowState = settingsPage.s_StartMinimized ? WindowState.Minimized : (WindowState)Properties.Settings.Default.MainWindowState;

            // start Service Manager
            serviceManager.Start();

            // start pipe client and server
            pipeClient.Start();
            pipeServer.Start();

            // execute args
            cmdParser.ParseArgs(arguments.Args);
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            foreach (KeyValuePair<string, string> pair in args)
            {
                string name = pair.Key;
                string property = pair.Value;

                switch (name)
                {
                    case "DSUEnabled":
                        break;
                    case "DSUip":
                        break;
                    case "DSUport":
                        break;
                }
            }
        }

        #region cmdParser
        internal void UpdateCloak(bool cloak)
        {
            // implement me
        }

        internal void UpdateHID(HIDmode mode)
        {
            // implement me
        }
        #endregion

        #region pipeClient
        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_TOAST:
                    PipeServerToast toast = (PipeServerToast)message;
                    toastManager.SendToast(toast.title, toast.content, toast.image);
                    break;

                case PipeCode.SERVER_SETTINGS:
                    PipeServerSettings settings = (PipeServerSettings)message;
                    UpdateSettings(settings.settings);
                    break;
            }
        }
        #endregion

        #region serviceManager
        private void OnServiceUpdate(ServiceControllerStatus status, ServiceStartMode mode)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                        b_ServiceInstall.Visibility = Visibility.Collapsed;
                        b_ServiceStop.Visibility = Visibility.Collapsed;
                        b_ServiceDelete.Visibility = Visibility.Visible;
                        b_ServiceStart.Visibility = Visibility.Visible;

                        b_ServiceDelete.IsEnabled = IsElevated;
                        b_ServiceStart.IsEnabled = IsElevated;
                        break;
                    case ServiceControllerStatus.Running:
                        b_ServiceInstall.Visibility = Visibility.Collapsed;
                        b_ServiceDelete.Visibility = Visibility.Collapsed;
                        b_ServiceStart.Visibility = Visibility.Collapsed;
                        b_ServiceStop.Visibility = Visibility.Visible;

                        b_ServiceStop.IsEnabled = IsElevated;
                        break;
                    case ServiceControllerStatus.ContinuePending:
                    case ServiceControllerStatus.PausePending:
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.StopPending:
                        b_ServiceInstall.IsEnabled = false;
                        b_ServiceDelete.IsEnabled = false;
                        b_ServiceStart.IsEnabled = false;
                        b_ServiceStop.IsEnabled = false;
                        break;
                    default:
                        b_ServiceDelete.Visibility = Visibility.Collapsed;
                        b_ServiceStart.Visibility = Visibility.Collapsed;
                        b_ServiceStop.Visibility = Visibility.Collapsed;
                        b_ServiceInstall.Visibility = Visibility.Visible;

                        b_ServiceInstall.IsEnabled = IsElevated;
                        break;
                }
            });
        }
        #endregion

        #region profileManager
        private void ProfileUpdated(Profile profile)
        {
            // implement me
        }

        private void ProfileDeleted(Profile profile)
        {
            // implement me
        }
        #endregion

        #region pipeServer
        private void OnClientMessage(object sender, PipeMessage e)
        {
            PipeConsoleArgs console = (PipeConsoleArgs)e;

            if (console.args.Length == 0)
                WindowState = prevWindowState;
            else
                cmdParser.ParseArgs(console.args);

            pipeServer.SendMessage(new PipeShutdown());
        }
        #endregion

        #region UI

        private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                NavView_Navigate("Settings");
            }
            else if (args.InvokedItemContainer != null)
            {
                NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
                string navItemTag = (string)navItem.Tag;

                switch (navItemTag)
                {
                    case "ServiceStart":
                        serviceManager.StartService();
                        break;
                    case "ServiceStop":
                        serviceManager.StopService();
                        break;
                    case "ServiceInstall":
                        serviceManager.CreateService(CurrentPathService);
                        break;
                    case "ServiceDelete":
                        serviceManager.DeleteService();
                        break;
                    default:
                        preNavItemTag = navItemTag;
                        break;
                }

                NavView_Navigate(preNavItemTag);
            }
        }

        public void NavView_Navigate(string navItemTag)
        {
            Page _page = null;
            if (navItemTag == "Settings")
            {
                _page = new SettingsPage();
            }
            else
            {
                var item = _pages.FirstOrDefault(p => p.Key.Equals(navItemTag));
                _page = item.Value;
            }
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            var preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (!(_page is null) && !Type.Equals(preNavPageType, _page))
            {
                NavView_Navigate(_page);
            }
        }

        public void NavView_Navigate(Page _page)
        {
            ContentFrame.Navigate(_page);
        }

        private void navView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            scrollPoint = e.GetPosition(scrollViewer);
            scrollOffset = scrollViewer.VerticalOffset;
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (scrollPoint == new Point())
                return;

            if (MainWindow.scrollLock)
                return;

            scrollViewer.ScrollToVerticalOffset(scrollOffset + (scrollPoint.Y - e.GetPosition(scrollViewer).Y));
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            serviceManager.Stop();

            if (pipeClient.connected)
                pipeClient.Stop();

            if (pipeServer.connected)
                pipeServer.Stop();

            profileManager.Stop();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // position and size settings
            switch (WindowState)
            {
                case WindowState.Normal:
                    Properties.Settings.Default.MainWindowLeft = this.Left;
                    Properties.Settings.Default.MainWindowTop = this.Top;

                    Properties.Settings.Default.MainWindowWidth = this.Width;
                    Properties.Settings.Default.MainWindowHeight = this.Height;
                    break;
            }
            Properties.Settings.Default.MainWindowState = (int)WindowState;

            if (settingsPage.s_CloseMinimises && !appClosing)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
            }

            Properties.Settings.Default.Save();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    notifyIcon.Show();
                    ShowInTaskbar = false;
                    break;
                case WindowState.Normal:
                case WindowState.Maximized:
                    notifyIcon.Destroy();
                    ShowInTaskbar = true;
                    prevWindowState = WindowState;
                    break;
            }
        }

        private void navView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            navView.SelectedItem = navView.MenuItems[0];

            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the home page.
            NavView_Navigate("ControllerPage");
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (navView.IsPaneOpen &&
                (navView.DisplayMode == NavigationViewDisplayMode.Compact ||
                 navView.DisplayMode == NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            navView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                // SettingsItem is not part of NavView.MenuItems, and doesn't have a Tag.
                navView.SelectedItem = (NavigationViewItem)navView.SettingsItem;
                navView.Header = "Settings";
            }
            else if (ContentFrame.SourcePageType != null)
            {
                var preNavPageType = ContentFrame.CurrentSourcePageType;
                var preNavPageName = preNavPageType.Name;

                var NavViewItem = navView.MenuItems
                    .OfType<NavigationViewItem>()
                    .Where(n => n.Tag.Equals(preNavPageName)).FirstOrDefault();

                if (!(NavViewItem is null))
                {
                    navView.SelectedItem = NavViewItem;
                    navView.Header = (string)NavViewItem.Content;
                }
                else
                {
                    navView.Header = ((Page)e.Content).Title;
                }
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollPoint = new Point();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
        }

        private void ScrollViewerEx_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }
        #endregion
    }
}
