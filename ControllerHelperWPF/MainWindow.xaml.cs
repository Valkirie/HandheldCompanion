using ModernWpf.Controls;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using ControllerCommon;
using System;
using System.IO;
using System.ServiceProcess;

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
        static Devices devicesPage;
        static Profiles profilesPage;

        // static vars
        public PipeClient pipeClient;
        public PipeServer pipeServer;
        public CmdParser cmdParser;
        public MouseHook mouseHook;
        public ToastManager toastManager;

        public ProfileManager profileManager;
        public ServiceManager serviceManager;

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

            // initialize pages
            devicesPage = new Devices(this);
            profilesPage = new Profiles(this);

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
            pipeClient.Connected += OnClientConnected;
            pipeClient.Disconnected += OnClientDisconnected;
            pipeClient.ServerMessage += OnServerMessage;

            // initialize pipe server
            pipeServer = new PipeServer("ControllerHelper", microsoftLogger);
            pipeServer.ClientMessage += OnClientMessage;

            // initialize Profile Manager
            profileManager = new ProfileManager(CurrentPathProfiles, microsoftLogger, pipeClient);
            profileManager.Deleted += ProfileDeleted;
            profileManager.Updated += ProfileUpdated;

            // initialize command parser
            cmdParser = new CmdParser(pipeClient, this, microsoftLogger);

            // initialize mouse hook
            mouseHook = new MouseHook(pipeClient, microsoftLogger);

            // initialize toast manager
            toastManager = new ToastManager("ControllerService");

            // initialize Service Manager
            serviceManager = new ServiceManager("ControllerService", strings.ServiceName, strings.ServiceDescription, microsoftLogger);
            serviceManager.Updated += UpdateService;
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
        private void OnServerMessage(object sender, PipeMessage e)
        {
            // implement me
        }

        private void OnClientDisconnected(object sender)
        {
            // implement me
        }

        private void OnClientConnected(object sender)
        {
            // implement me
        }
        #endregion

        #region serviceManager
        private void UpdateService(ServiceControllerStatus status, ServiceStartMode mode)
        {
            // update pages
            devicesPage.UpdateServivce(status, mode);
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

        private void navView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                navView.Header = "Settings";
            }
            else
            {
                NavigationViewItem item = args.SelectedItem as NavigationViewItem;
                navView.Header = item.Content;

                switch (item.Content)
                {
                    case "Devices":
                        ContentFrame.Navigate(devicesPage);
                        break;
                    case "Profiles":
                        ContentFrame.Navigate(profilesPage);
                        break;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            navView.SelectedItem = navView.MenuItems[0];

            // start Service Manager
            serviceManager.Start();

            // start pipe client and server
            pipeClient.Start();
            pipeServer.Start();

            // start Profile Manager
            profileManager.Start();

            // execute args
            cmdParser.ParseArgs(arguments.Args);
        }
    }
}
