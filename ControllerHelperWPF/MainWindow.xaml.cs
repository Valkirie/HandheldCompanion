using ControllerCommon;
using ControllerHelperWPF.Pages;
using Microsoft.Extensions.Logging;
using ModernWpf.Controls;
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
using Windows.UI.ViewManagement;
using Page = System.Windows.Controls.Page;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger microsoftLogger;
        private StartupEventArgs arguments;

        // page vars
        public ControllerPage controllerPage;
        public ProfilesPage profilesPage;

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

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");
            CurrentPathLogs = Path.Combine(CurrentPath, "Logs");

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
            controllerPage = new ControllerPage(this, microsoftLogger);
            profilesPage = new ProfilesPage(this, microsoftLogger);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationViewItem item = navView.MenuItems.OfType<NavigationViewItem>().First();
            navView.SelectedItem = item;
            Navigate(item);

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
            // implement me
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
            if (args.IsSettingsInvoked)
                Navigate(typeof(SettingsPage)); // temp
            else
                Navigate((NavigationViewItem)args.InvokedItemContainer);
        }

        private void navView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        Point scrollPoint = new Point();
        double scrollOffset = 1;

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

        private void ContentFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            NavigationViewItem menuItem;
            if (e.SourcePageType() == typeof(SettingsPage))
                menuItem = (NavigationViewItem)navView.SettingsItem;
            else
                menuItem = navView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag.ToString() == e.SourcePageType().Name);

            if (menuItem == null)
                return;

            navView.SelectedItem = menuItem;
            navView.Header = menuItem.Content;
        }

        private void Navigate(Type type)
        {
            ContentFrame.Navigate(type);
        }

        private void Navigate(NavigationViewItem menuItem)
        {
            Page page = GetPage(menuItem);
            ContentFrame.Navigate(page);
        }

        private Page GetPage(NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "ControllerPage":
                    return (Page)controllerPage;
                case "ProfilesPage":
                    return (Page)profilesPage;
                case "AboutPage":
                    return (Page)profilesPage;
                case "SettingsPage":
                    return new SettingsPage(); // temp
            }
            return null;
        }
        #endregion
    }
}
