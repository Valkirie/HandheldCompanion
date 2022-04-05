using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Utils;
using HandheldCompanion.Models;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.Windows;
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
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Navigation;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.ServiceControllerStatus;

namespace HandheldCompanion.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger logger;
        private StartupEventArgs arguments;
        public FileVersionInfo fileVersionInfo;
        public static new string Name;

        // page vars
        private Dictionary<string, Page> _pages = new();
        private string preNavItemTag;

        public ControllerPage controllerPage;
        public ProfilesPage profilesPage;
        public SettingsPage settingsPage;
        public AboutPage aboutPage;

        // overlay vars
        private Overlay overlay;

        // touchscroll vars
        Point scrollPoint = new Point();
        double scrollOffset = 1;
        public static bool scrollLock = false;
        public static bool IsElevated = false;

        // connectivity vars
        public PipeClient pipeClient;
        public PipeServer pipeServer;

        // Hidder vars
        public HidHide Hidder;

        // Command parser vars
        public CmdParser cmdParser;

        // Handheld devices vars
        private Device handheldDevice;
        private Model handheldModels;

        // manager(s) vars
        public ToastManager toastManager;
        public ProcessManager processManager;
        public ServiceManager serviceManager;
        public ProfileManager profileManager;
        public TaskManager taskManager;

        private WindowState prevWindowState;
        private NotifyIcon notifyIcon;

        public string CurrentExe, CurrentPath, CurrentPathService, CurrentPathProfiles, CurrentPathLogs;
        private bool FirstStart, appClosing;

        public MainWindow(StartupEventArgs arguments, ILogger microsoftLogger)
        {
            InitializeComponent();
            Name = this.Title;

            this.logger = microsoftLogger;
            this.arguments = arguments;

            // get the actual handheld device
            var ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
            var ProductName = MotherboardInfo.Product;

            // pull me from service ?
            switch (ProductName)
            {
                case "AYANEO 2021":
                case "AYANEO 2021 Pro":
                case "AYANEO 2021 Pro Retro Power":
                    handheldDevice = new AYANEO2021(ManufacturerName, ProductName);
                    handheldModels = new ModelAYANEO2021();
                    break;
                case "NEXT Pro":
                case "NEXT Advance":
                case "NEXT":
                    handheldDevice = new AYANEONEXT(ManufacturerName, ProductName);
                    handheldModels = new ModelXBOX360(); // temp
                    break;
                case "ONE XPLAYER":
                    handheldDevice = new OXPAMDMini(ManufacturerName, ProductName);
                    handheldModels = new ModelXBOX360(); // temp
                    break;
                default:
                    handheldDevice = new DefaultDevice(ManufacturerName, ProductName);
                    handheldModels = new ModelXBOX360();
                    break;
            }

            logger.LogInformation("{0} ({1})", ManufacturerName, ProductName);

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;
            fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);

            // initialize log
            logger.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

            // initialize notifyIcon
            ToolStripMenuItem notifyMenuItem = new("Exit");
            ContextMenu notifyMenu = new();
            notifyMenu.Items.Add(notifyMenuItem);

            notifyIcon = new()
            {
                Text = Name,
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Visible = false,
                ContextMenuStrip = new(),
            };
            notifyIcon.DoubleClick += NotifyIconDoubleClick;

            notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Exit");
            notifyIcon.ContextMenuStrip.Items[0].Click += (o, e) =>
            {
                appClosing = true;
                this.Close();
            };

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");
            CurrentPathLogs = Path.Combine(CurrentPath, "Logs");

            // initialize HidHide
            Hidder = new HidHide(logger);
            Hidder.RegisterApplication(CurrentExe);

            // settings
            IsElevated = CommonUtils.IsAdministrator();

            // initialize title
            this.Title += $" ({fileVersionInfo.FileVersion}) ({(IsElevated ? Properties.Resources.Administrator : Properties.Resources.User)})";

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathService))
            {
                logger.LogCritical("Controller Service executable is missing");
                throw new InvalidOperationException();
            }

            // initialize pipe client
            pipeClient = new PipeClient("ControllerService", logger);
            pipeClient.ServerMessage += OnServerMessage;
            pipeClient.Connected += OnClientConnected;
            pipeClient.Disconnected += OnClientDisconnected;

            // initialize pipe server
            pipeServer = new PipeServer("HandheldCompanion", logger);
            pipeServer.ClientMessage += OnClientMessage;

            // initialize Profile Manager
            profileManager = new ProfileManager(logger, pipeClient);

            // initialize toast manager
            toastManager = new ToastManager("ControllerService");

            // initialize overlay
            overlay = new Overlay(logger, pipeClient);
            overlay.SetHandheldModel(handheldModels);

            // initialize process manager
            processManager = new ProcessManager();
            processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            processManager.ProcessStarted += ProcessManager_ProcessStarted;
            processManager.ProcessStopped += ProcessManager_ProcessStopped;

            // initialize service manager
            serviceManager = new ServiceManager("ControllerService", Properties.Resources.ServiceName, Properties.Resources.ServiceDescription, logger);
            serviceManager.Updated += OnServiceUpdate;
            serviceManager.StartFailed += (status) =>
            {
                // todo: implement localized strings
                Dialog.ShowAsync("Service manager", "Oups. There was an issue while we tried to start the service.", ContentDialogButton.Primary, null, "OK");
            };
            serviceManager.StopFailed += (status) =>
            {
                // todo: implement localized strings
                Dialog.ShowAsync("Service manager", "Oups. There was an issue while we tried to stop the service.", ContentDialogButton.Primary, null, "OK");
            };

            // initialize task manager
            taskManager = new TaskManager("ControllerService", CurrentExe);

            // initialize pages
            controllerPage = new ControllerPage("controller", this, logger);
            controllerPage.Updated += (controllerMode) =>
            {
                if (handheldModels.ModelLocked)
                    return;

                this.Dispatcher.Invoke(() =>
                {
                    switch (controllerMode)
                    {
                        default:
                        case HIDmode.DualShock4Controller: // implement me
                        case HIDmode.Xbox360Controller:
                            handheldModels = new ModelXBOX360();
                            break;
                    }

                    overlay.SetHandheldModel(handheldModels);
                });
            };

            profilesPage = new ProfilesPage("profiles", this, logger);
            settingsPage = new SettingsPage("settings", this, logger);
            aboutPage = new AboutPage("about", this, logger, handheldDevice);

            // initialize command parser
            cmdParser = new CmdParser(pipeClient, this, logger);
            cmdParser.ParseArgs(arguments.Args, true);

            // initialize pages events
            settingsPage.ToastChanged += (value) =>
            {
                toastManager.Enabled = value;
            };
            settingsPage.AutoStartChanged += (value) =>
            {
                taskManager.UpdateTask(value);
            };
            settingsPage.ServiceChanged += (value) =>
            {
                serviceManager.SetStartType(value);
            };

            _pages.Add("ControllerPage", controllerPage);
            _pages.Add("ProfilesPage", profilesPage);
            _pages.Add("AboutPage", aboutPage);

            if (!IsElevated)
            {
                foreach (NavigationViewItem item in navView.FooterMenuItems)
                    item.ToolTip = Properties.Resources.WarningElevated;
            }
        }

        private void ProcessManager_ProcessStopped(uint processid, string path, string exec)
        {
            try
            {
                Profile currentProfile = profileManager.GetProfileFromExec(exec);

                if (currentProfile == null)
                    return;

                currentProfile.fullpath = path;
                currentProfile.IsRunning = false;

                // update profile and inform settings page
                profileManager.UpdateOrCreateProfile(currentProfile);
            }
            catch (Exception) { }
        }

        private void ProcessManager_ProcessStarted(uint processid, string path, string exec)
        {
            try
            {
                Profile currentProfile = profileManager.GetProfileFromExec(exec);

                if (currentProfile == null)
                    return;

                currentProfile.fullpath = path;
                currentProfile.IsRunning = true;

                // update profile and inform settings page
                profileManager.UpdateOrCreateProfile(currentProfile);
            }
            catch (Exception) { }
        }

        private void ProcessManager_ForegroundChanged(uint processid, string path, string exec)
        {
            try
            {
                Profile currentProfile = profileManager.GetProfileFromExec(exec);

                if (currentProfile == null)
                    currentProfile = profileManager.GetDefault();

                if (!currentProfile.enabled)
                    return;

                currentProfile.fullpath = path;
                currentProfile.IsRunning = true;

                // update profile and inform settings page
                profileManager.UpdateOrCreateProfile(currentProfile);

                // inform service & mouseHook
                pipeClient.SendMessage(new PipeClientProfile { profile = currentProfile });

                // change overlay hook
                /* this.Dispatcher.Invoke(() =>
                {
                    // hide overlay on profile switch
                    overlay.UnHook();

                    if (!currentProfile.IsDefault)
                        overlay.HookInto(processid);
                }); */

                logger.LogDebug("Profile {0} applied", currentProfile.name);
            }
            catch (Exception) { }
        }

        private void NotifyIconDoubleClick(object? sender, EventArgs e)
        {
            WindowState = prevWindowState;
        }

        private void OnClientConnected(object sender)
        {
            if (IsElevated)
            {
                // start process manager
                processManager.Start();
            }
        }

        private void OnClientDisconnected(object sender)
        {
            // stop process manager
            processManager.Stop();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // update Position and Size
            this.Height = (int)Math.Max(this.MinHeight, Properties.Settings.Default.MainWindowHeight);
            this.Width = (int)Math.Max(this.MinWidth, Properties.Settings.Default.MainWindowWidth);

            this.Left = Math.Max(0, Properties.Settings.Default.MainWindowLeft);
            this.Top = Math.Max(0, Properties.Settings.Default.MainWindowTop);

            // pull settings
            WindowState = settingsPage.StartMinimized ? WindowState.Minimized : (WindowState)Properties.Settings.Default.MainWindowState;
            toastManager.Enabled = settingsPage.ToastEnable;

            // start Service Manager
            serviceManager.Start();

            // start Profile Manager
            profileManager.Start();

            // start pipe client and server
            pipeClient.Start();
            pipeServer.Start();

            if (IsElevated)
            {
                // start service with companion
                if (settingsPage.StartServiceWithCompanion)
                    serviceManager.StartServiceAsync();
            }
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
                    case "HIDmode":
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
        private void OnServiceUpdate(ServiceControllerStatus status, int mode)
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

                switch ((ServiceStartMode)mode)
                {
                    case ServiceStartMode.Disabled:
                        b_ServiceStart.IsEnabled = false;
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
                        serviceManager.StartServiceAsync();
                        break;
                    case "ServiceStop":
                        serviceManager.StopServiceAsync();
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
                _page = settingsPage;
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
            processManager.Stop();

            notifyIcon.Visible = false;
            notifyIcon = null;

            overlay.Close();
            overlay = null;

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

            if (settingsPage.CloseMinimises && !appClosing)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                return;
            }

            if (IsElevated)
            {
                // stop service with companion
                if (settingsPage.HaltServiceWithCompanion)
                    serviceManager.StopServiceAsync();
            }

            Properties.Settings.Default.Save();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    notifyIcon.Visible = true;
                    ShowInTaskbar = false;
                    toastManager.SendToast(Name, "The application is running in the background.");
                    break;
                case WindowState.Normal:
                case WindowState.Maximized:
                    notifyIcon.Visible = false;
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
                this.pipeClient.SendMessage(new PipeNavigation((string)preNavPageName));

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
        #endregion
    }
}
