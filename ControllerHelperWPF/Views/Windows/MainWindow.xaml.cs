using ControllerCommon;
using ControllerHelperWPF.Views.Pages;
using Microsoft.Extensions.Logging;
using ModernWpf.Controls;
using ModernWpf.Media.Animation;
using ModernWpf.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Page = System.Windows.Controls.Page;

namespace ControllerHelperWPF.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger microsoftLogger;
        private StartupEventArgs arguments;

        // page vars
        private Dictionary<string, Page> _pages = new();
        private string preNavItemTag;

        public ControllerPage controllerPage;
        public ProfilesPage profilesPage;

        // touchscroll vars
        Point scrollPoint = new Point();
        double scrollOffset = 1;

        // connectivity vars
        public PipeClient pipeClient;
        public PipeServer pipeServer;

        public CmdParser cmdParser;
        public MouseHook mouseHook;
        public ToastManager toastManager;

        public ServiceManager serviceManager;
        public ProfileManager profileManager;

        private WindowState prevWindowState;

        public string CurrentExe, CurrentPath, CurrentPathService, CurrentPathProfiles, CurrentPathLogs;
        private bool IsElevated, FirstStart, appClosing, ToastEnable;

        public MainWindow(StartupEventArgs arguments, ILogger microsoftLogger)
        {
            InitializeComponent();

            string ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
            navView.PaneTitle = ManufacturerName;

            this.microsoftLogger = microsoftLogger;
            this.arguments = arguments;

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;

            // initialize log
            microsoftLogger.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");
            CurrentPathLogs = Path.Combine(CurrentPath, "Logs");

            // settings
            IsElevated = Utils.IsAdministrator();

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathService))
            {
                microsoftLogger.LogCritical("Controller Service executable is missing");
                throw new InvalidOperationException();
            }

            // initialize pipe client
            pipeClient = new PipeClient("ControllerService", microsoftLogger);
            pipeClient.ServerMessage += OnServerMessage;

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
            toastManager.Enabled = Properties.Settings.Default.ToastEnable;

            // initialize Service Manager
            serviceManager = new ServiceManager("ControllerService", strings.ServiceName, strings.ServiceDescription, microsoftLogger);
            serviceManager.Updated += OnServiceUpdate;

            // initialize pages
            controllerPage = new ControllerPage("controller", this, microsoftLogger);
            profilesPage = new ProfilesPage("profiles", this, microsoftLogger);

            _pages.Add("ControllerPage", controllerPage);
            _pages.Add("ProfilesPage", profilesPage);
            _pages.Add("AboutPage", profilesPage); // todo
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
                // disable service control if not elevated
                status = IsElevated ? status : ServiceControllerStatus.ContinuePending;

                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                        b_ServiceInstall.Visibility = Visibility.Collapsed;
                        b_ServiceStop.Visibility = Visibility.Collapsed;

                        b_ServiceDelete.Visibility = Visibility.Visible;
                        b_ServiceStart.Visibility = Visibility.Visible;

                        b_ServiceDelete.IsEnabled = true;
                        b_ServiceStart.IsEnabled = true;
                        break;
                    case ServiceControllerStatus.Running:
                        b_ServiceInstall.Visibility = Visibility.Collapsed;
                        b_ServiceDelete.Visibility = Visibility.Collapsed;
                        b_ServiceStart.Visibility = Visibility.Collapsed;

                        b_ServiceStop.Visibility = Visibility.Visible;

                        b_ServiceStop.IsEnabled = true;
                        break;
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

                        b_ServiceInstall.Visibility = IsElevated ? Visibility.Visible : Visibility.Collapsed;

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

                switch(navItemTag)
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
                ContentFrame.Navigate(_page);
            }
        }

        private void navView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            scrollPoint = e.GetPosition(scrollViewer);
            scrollOffset = scrollViewer.VerticalOffset;
        }

        private void ScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (scrollPoint == new Point())
                return;
            
            scrollViewer.ScrollToVerticalOffset(scrollOffset + (scrollPoint.Y - e.GetPosition(scrollViewer).Y));
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

                navView.SelectedItem = navView.MenuItems
                    .OfType<NavigationViewItem>()
                    .First(n => n.Tag.Equals(preNavPageName));

                navView.Header =
                    ((NavigationViewItem)navView.SelectedItem)?.Content?.ToString();
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            scrollPoint = new Point();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
        }

        private void ScrollViewerEx_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }
        #endregion
    }
}
