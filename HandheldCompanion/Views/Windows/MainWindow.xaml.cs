using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.Windows;
using ModernWpf.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using static ControllerCommon.Managers.PowerManager;
using static HandheldCompanion.Managers.InputsHotkey;
using Application = System.Windows.Application;
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.Managers.ServiceControllerStatus;

namespace HandheldCompanion.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // devices vars
        public static IDevice CurrentDevice;

        // page vars
        private static Dictionary<string, Page> _pages = new();
        private string preNavItemTag;

        public static ControllerPage controllerPage;
        public static ProfilesPage profilesPage;
        public static SettingsPage settingsPage;
        public static AboutPage aboutPage;
        public static OverlayPage overlayPage;
        public static HotkeysPage hotkeysPage;
        public static LayoutPage layoutPage;

        // overlay(s) vars
        public static OverlayModel overlayModel;
        public static OverlayTrackpad overlayTrackpad;
        public static OverlayQuickTools overlayquickTools;

        // manager(s) vars
        private static List<Manager> _managers = new();
        public static ServiceManager serviceManager;
        public static TaskManager taskManager;
        public static PerformanceManager performanceManager;
        public static UpdateManager updateManager;

        private WindowState prevWindowState;
        private NotifyIcon notifyIcon;
        private bool NotifyInTaskbar;

        public static string CurrentExe, CurrentPath, CurrentPathService;
        private bool appClosing;
        private bool IsReady;

        private static MainWindow CurrentWindow;
        public static FileVersionInfo fileVersionInfo;

        public static string InstallPath;
        public static string SettingsPath;

        public MainWindow(FileVersionInfo _fileVersionInfo, Assembly CurrentAssembly)
        {
            InitializeComponent();

            fileVersionInfo = _fileVersionInfo;
            CurrentWindow = this;

            // get process
            Process process = Process.GetCurrentProcess();

            // initialize splash screen on first start only
            bool IsFirstStart = SettingsManager.GetBoolean("FirstStart");
#if !DEBUG
            if (IsFirstStart)
            {
                SplashScreen splashScreen = new SplashScreen(CurrentAssembly, "Resources/icon.png");
                splashScreen.Show(true, true);
            }
#endif

            // fix touch support
            var tablets = Tablet.TabletDevices;

            // define current directory
            InstallPath = AppDomain.CurrentDomain.BaseDirectory;
            SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion");

            // initialiaze path
            if (!Directory.Exists(SettingsPath))
                Directory.CreateDirectory(SettingsPath);

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            // initialize notifyIcon
            notifyIcon = new()
            {
                Text = Title,
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Visible = false,
                ContextMenuStrip = new()
            };

            notifyIcon.DoubleClick += (sender, e) => { SwapWindowState(); };

            AddNotifyIconItem("Main Window");
            AddNotifyIconItem("Quick Tools");
            AddNotifyIconSeparator();

            foreach (NavigationViewItem item in navView.FooterMenuItems)
                AddNotifyIconItem(item.Content.ToString(), item.Tag);

            AddNotifyIconSeparator();
            AddNotifyIconItem("Exit");

            // paths
            CurrentExe = process.MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathService))
            {
                LogManager.LogCritical("Controller Service executable is missing");
                throw new InvalidOperationException();
            }

            // initialize HidHide
            HidHide.RegisterApplication(CurrentExe);

            // initialize title
            this.Title += $" ({fileVersionInfo.FileVersion})";

            // initialize device
            CurrentDevice = IDevice.GetDefault();
            CurrentDevice.PullSensors();
            CurrentDevice.Open();

            // load manager(s)
            loadManagers();

            // load window(s)
            loadWindows();

            // load page(s)
            loadPages();

            // start static managers in sequence
            // managers that has to be stopped/started when session status changes shouldn't be put here

            ToastManager.Start();
            ToastManager.IsEnabled = SettingsManager.GetBoolean("ToastEnable");

            ProfileManager.Start();
            ControllerManager.Start();
            HotkeysManager.Start();

            DeviceManager.UsbDeviceArrived += GenericDeviceUpdated;
            DeviceManager.UsbDeviceRemoved += GenericDeviceUpdated;
            DeviceManager.Start();

            PlatformManager.Start();
            OSDManager.Start();
            LayoutManager.Start();
            ProcessManager.Start();
            EnergyManager.Start();

            PowerManager.SystemStatusChanged += OnSystemStatusChanged;
            PowerManager.Start();

            SystemManager.Start();

            // start managers asynchroneously
            foreach (Manager manager in _managers)
                new Thread(manager.Start).Start();

            // start setting last
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            SettingsManager.Start();

            PipeClient.ServerMessage += OnServerMessage;
            PipeClient.Connected += OnClientConnected;
            PipeClient.Disconnected += OnClientDisconnected;
            PipeClient.Open();

            // update Position and Size
            Height = (int)Math.Max(MinHeight, SettingsManager.GetDouble("MainWindowHeight"));
            Width = (int)Math.Max(MinWidth, SettingsManager.GetDouble("MainWindowWidth"));
            Left = Math.Min(SystemParameters.PrimaryScreenWidth - MinWidth, SettingsManager.GetDouble("MainWindowLeft"));
            Top = Math.Min(SystemParameters.PrimaryScreenHeight - MinHeight, SettingsManager.GetDouble("MainWindowTop"));
            navView.IsPaneOpen = SettingsManager.GetBoolean("MainWindowIsPaneOpen");

            // update FirstStart
            if (IsFirstStart)
                SettingsManager.SetProperty("FirstStart", false);
        }

        private void AddNotifyIconItem(string name, object tag = null)
        {
            tag ??= string.Concat(name.Where(c => !char.IsWhiteSpace(c)));

            ToolStripMenuItem menuItemMainWindow = new ToolStripMenuItem(name);
            menuItemMainWindow.Tag = tag;
            menuItemMainWindow.Click += MenuItem_Click;
            notifyIcon.ContextMenuStrip.Items.Add(menuItemMainWindow);
        }

        private void AddNotifyIconSeparator()
        {
            ToolStripSeparator separator = new ToolStripSeparator();
            notifyIcon.ContextMenuStrip.Items.Add(separator);
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "ToastEnable":
                    ToastManager.IsEnabled = Convert.ToBoolean(value);
                    break;
                case "DesktopProfileOnStart":
                    if (SettingsManager.IsInitialized)
                        break;

                    bool DesktopLayout = Convert.ToBoolean(value);
                    SettingsManager.SetProperty("DesktopLayoutEnabled", DesktopLayout, false, true);
                    break;
            }

            if (PipeClient.IsConnected)
            {
                PipeClientSettings settings = new PipeClientSettings(name, value);
                PipeClient.SendMessage(settings);
            }
        }

        public void SwapWindowState()
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (WindowState)
                {
                    case WindowState.Normal:
                    case WindowState.Maximized:
                        WindowState = WindowState.Minimized;
                        break;
                    case WindowState.Minimized:
                        WindowState = prevWindowState;
                        break;
                }
            });
        }

        public static MainWindow GetCurrent()
        {
            return CurrentWindow;
        }

        private void loadPages()
        {
            // initialize pages
            controllerPage = new ControllerPage("controller");
            controllerPage.Loaded += ControllerPage_Loaded;

            profilesPage = new ProfilesPage("profiles");
            settingsPage = new SettingsPage("settings");
            aboutPage = new AboutPage("about");
            overlayPage = new OverlayPage("overlay");
            hotkeysPage = new HotkeysPage("hotkeys");
            layoutPage = new LayoutPage("layout");

            // store pages
            _pages.Add("ControllerPage", controllerPage);
            _pages.Add("ProfilesPage", profilesPage);
            _pages.Add("AboutPage", aboutPage);
            _pages.Add("OverlayPage", overlayPage);
            _pages.Add("SettingsPage", settingsPage);
            _pages.Add("HotkeysPage", hotkeysPage);
            _pages.Add("LayoutPage", layoutPage);

            // handle controllerPage events
            controllerPage.HIDchanged += (HID) =>
            {
                overlayModel.UpdateHIDMode(HID);
            };
        }

        private void loadWindows()
        {
            // initialize overlay
            overlayModel = new OverlayModel();
            overlayTrackpad = new OverlayTrackpad();
            overlayquickTools = new OverlayQuickTools();
        }

        private void loadManagers()
        {
            // initialize managers
            serviceManager = new ServiceManager("ControllerService", Properties.Resources.ServiceName, Properties.Resources.ServiceDescription);
            taskManager = new TaskManager("HandheldCompanion", CurrentExe);
            performanceManager = new();
            updateManager = new();

            // store managers
            _managers.Add(serviceManager);
            _managers.Add(taskManager);
            _managers.Add(performanceManager);
            _managers.Add(updateManager);

            serviceManager.Initialized += () =>
            {
                // listen for service update once initialized
                serviceManager.Updated += OnServiceUpdate;

                if (SettingsManager.GetBoolean("StartServiceWithCompanion"))
                {
                    if (!serviceManager.Exists())
                        serviceManager.CreateService(CurrentPathService);

                    _ = serviceManager.StartServiceAsync();
                }
            };
            serviceManager.StartFailed += (status, message) =>
            {
                _ = Dialog.ShowAsync($"{Properties.Resources.MainWindow_ServiceManager}", $"{Properties.Resources.MainWindow_ServiceManagerStartIssue}\n\n{message}", ContentDialogButton.Primary, null, $"{Properties.Resources.MainWindow_OK}");
            };
            serviceManager.StopFailed += (status) =>
            {
                _ = Dialog.ShowAsync($"{Properties.Resources.MainWindow_ServiceManager}", $"{Properties.Resources.MainWindow_ServiceManagerStopIssue}", ContentDialogButton.Primary, null, $"{Properties.Resources.MainWindow_OK}");
            };
        }

        private void GenericDeviceUpdated(PnPDevice device, DeviceEventArgs obj)
        {
            CurrentDevice.PullSensors();

            aboutPage.UpdateDevice(device);
            settingsPage.UpdateDevice(device);
        }

        private void InputsManager_TriggerRaised(string listener, InputsChord input, InputsHotkeyType type, bool IsKeyDown, bool IsKeyUp)
        {
            switch (listener)
            {
                case "quickTools":
                    overlayquickTools.UpdateVisibility();
                    break;
                case "overlayGamepad":
                    overlayModel.UpdateVisibility();
                    break;
                case "overlayTrackpads":
                    overlayTrackpad.UpdateVisibility();
                    break;
                case "shortcutMainwindow":
                    SwapWindowState();
                    break;
            }
        }

        private void MenuItem_Click(object? sender, EventArgs e)
        {
            switch (((ToolStripMenuItem)sender).Tag)
            {
                case "MainWindow":
                    SwapWindowState();
                    break;
                case "QuickTools":
                    overlayquickTools.UpdateVisibility();
                    break;
                case "ServiceStart":
                    _ = serviceManager.StartServiceAsync();
                    break;
                case "ServiceStop":
                    _ = serviceManager.StopServiceAsync();
                    break;
                case "ServiceInstall":
                    serviceManager.CreateService(CurrentPathService);
                    break;
                case "ServiceDelete":
                    serviceManager.DeleteService();
                    break;
                case "Exit":
                    appClosing = true;
                    this.Close();
                    break;
            }
        }

        private void OnClientConnected()
        {
            // (re)send all local settings to server at once
            PipeClientSettings settings = new PipeClientSettings();

            foreach (KeyValuePair<string, object> values in SettingsManager.GetProperties())
                settings.settings.Add(values.Key, values.Value);

            PipeClient.SendMessage(settings);
        }

        private void OnClientDisconnected()
        {
            // do something
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void ControllerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsReady)
                return;

            // home page has loaded, display main window
            WindowState = SettingsManager.GetBoolean("StartMinimized") ? WindowState.Minimized : (WindowState)SettingsManager.GetInt("MainWindowState");
            prevWindowState = (WindowState)SettingsManager.GetInt("MainWindowPrevState");

            IsReady = true;
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

        #region PipeServer
        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_TOAST:
                    PipeServerToast toast = (PipeServerToast)message;
                    ToastManager.SendToast(toast.title, toast.content, toast.image);
                    break;

                case PipeCode.SERVER_SETTINGS:
                    PipeServerSettings settings = (PipeServerSettings)message;
                    UpdateSettings(settings.settings);
                    break;
            }
        }
        #endregion

        #region serviceManager

        /*
         * Stop
         * Start
         * Deploy
         * Remove
         */

        private void OnServiceUpdate(ServiceControllerStatus status, int mode)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch ((ServiceStartMode)mode)
                {
                    case ServiceStartMode.Disabled:
                        b_ServiceStop.IsEnabled = false;
                        b_ServiceStart.IsEnabled = false;
                        b_ServiceInstall.IsEnabled = true;
                        b_ServiceDelete.IsEnabled = false;

                        if (notifyIcon.ContextMenuStrip is not null)
                        {
                            notifyIcon.ContextMenuStrip.Items[3].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[4].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[5].Enabled = true;
                            notifyIcon.ContextMenuStrip.Items[6].Enabled = false;
                        }
                        break;

                    default:
                        {
                            switch (status)
                            {
                                case ServiceControllerStatus.Paused:
                                case ServiceControllerStatus.Stopped:
                                    b_ServiceStop.IsEnabled = false;
                                    b_ServiceStart.IsEnabled = true;
                                    b_ServiceInstall.IsEnabled = false;
                                    b_ServiceDelete.IsEnabled = true;

                                    if (notifyIcon.ContextMenuStrip is not null)
                                    {
                                        notifyIcon.ContextMenuStrip.Items[3].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[4].Enabled = true;
                                        notifyIcon.ContextMenuStrip.Items[5].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[6].Enabled = true;
                                    }
                                    break;
                                case ServiceControllerStatus.Running:
                                    b_ServiceStop.IsEnabled = true;
                                    b_ServiceStart.IsEnabled = false;
                                    b_ServiceInstall.IsEnabled = false;
                                    b_ServiceDelete.IsEnabled = false;

                                    if (notifyIcon.ContextMenuStrip is not null)
                                    {
                                        notifyIcon.ContextMenuStrip.Items[3].Enabled = true;
                                        notifyIcon.ContextMenuStrip.Items[4].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[5].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[6].Enabled = false;
                                    }
                                    break;
                                case ServiceControllerStatus.ContinuePending:
                                case ServiceControllerStatus.PausePending:
                                case ServiceControllerStatus.StartPending:
                                case ServiceControllerStatus.StopPending:
                                    b_ServiceStop.IsEnabled = false;
                                    b_ServiceStart.IsEnabled = false;
                                    b_ServiceInstall.IsEnabled = false;
                                    b_ServiceDelete.IsEnabled = false;

                                    if (notifyIcon.ContextMenuStrip is not null)
                                    {
                                        notifyIcon.ContextMenuStrip.Items[3].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[4].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[5].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[6].Enabled = false;
                                    }
                                    break;
                                default:
                                    b_ServiceStop.IsEnabled = false;
                                    b_ServiceStart.IsEnabled = false;
                                    b_ServiceInstall.IsEnabled = true;
                                    b_ServiceDelete.IsEnabled = false;

                                    if (notifyIcon.ContextMenuStrip is not null)
                                    {
                                        notifyIcon.ContextMenuStrip.Items[3].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[4].Enabled = false;
                                        notifyIcon.ContextMenuStrip.Items[5].Enabled = true;
                                        notifyIcon.ContextMenuStrip.Items[6].Enabled = false;
                                    }
                                    break;
                            }
                        }
                        break;
                }
            });
        }
        #endregion

        #region UI
        private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is not null)
            {
                NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
                string navItemTag = (string)navItem.Tag;

                switch (navItemTag)
                {
                    case "ServiceStart":
                        _ = serviceManager.StartServiceAsync();
                        return;
                    case "ServiceStop":
                        _ = serviceManager.StopServiceAsync();
                        return;
                    case "ServiceInstall":
                        serviceManager.CreateService(CurrentPathService);
                        return;
                    case "ServiceDelete":
                        serviceManager.DeleteService();
                        return;
                    default:
                        preNavItemTag = navItemTag;
                        break;
                }

                NavView_Navigate(preNavItemTag);
            }
        }

        public void NavView_Navigate(string navItemTag)
        {
            var item = _pages.FirstOrDefault(p => p.Key.Equals(navItemTag));
            Page _page = item.Value;

            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            var preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (!(_page is null) && !Type.Equals(preNavPageType, _page))
            {
                NavView_Navigate(_page);
            }
        }

        public static void NavView_Navigate(Page _page)
        {
            CurrentWindow.ContentFrame.Navigate(_page);
        }

        private void navView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            CurrentDevice.Close();

            serviceManager.Stop();
            performanceManager.Stop();

            notifyIcon.Visible = false;
            notifyIcon.Dispose();

            overlayModel.Close();
            overlayTrackpad.Close();
            overlayquickTools.Close(true);

            if (PipeClient.IsConnected)
                PipeClient.Close();

            ControllerManager.Stop();
            InputsManager.Stop();
            DeviceManager.Stop();
            PlatformManager.Stop();
            OSDManager.Stop();
            ProfileManager.Stop();
            LayoutManager.Stop();
            ProcessManager.Stop();
            EnergyManager.Stop();
            PowerManager.Stop();
            ToastManager.Stop();

            // closing page(s)
            controllerPage.Page_Closed();
            profilesPage.Page_Closed();
            settingsPage.Page_Closed();
            overlayPage.Page_Closed();
            hotkeysPage.Page_Closed();
            layoutPage.Page_Closed();

            // force kill application
            Environment.Exit(0);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // position and size settings
            switch (WindowState)
            {
                case WindowState.Normal:
                    SettingsManager.SetProperty("MainWindowLeft", Left);
                    SettingsManager.SetProperty("MainWindowTop", Top);
                    SettingsManager.SetProperty("MainWindowWidth", ActualWidth);
                    SettingsManager.SetProperty("MainWindowHeight", ActualHeight);
                    break;
                case WindowState.Maximized:
                    SettingsManager.SetProperty("MainWindowLeft", 0);
                    SettingsManager.SetProperty("MainWindowTop", 0);
                    SettingsManager.SetProperty("MainWindowWidth", SystemParameters.MaximizedPrimaryScreenWidth);
                    SettingsManager.SetProperty("MainWindowHeight", SystemParameters.MaximizedPrimaryScreenHeight);

                    break;
            }

            SettingsManager.SetProperty("MainWindowState", (int)WindowState);
            SettingsManager.SetProperty("MainWindowPrevState", (int)prevWindowState);

            SettingsManager.SetProperty("MainWindowIsPaneOpen", navView.IsPaneOpen);

            if (SettingsManager.GetBoolean("CloseMinimises") && !appClosing)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                return;
            }

            // stop service with companion
            if (SettingsManager.GetBoolean("HaltServiceWithCompanion"))
            {
                // only halt process if start mode isn't set to "Automatic"
                if (serviceManager.type != ServiceStartMode.Automatic)
                    _ = serviceManager.StopServiceAsync();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    notifyIcon.Visible = true;
                    ShowInTaskbar = false;

                    if (!NotifyInTaskbar)
                    {
                        ToastManager.SendToast(Title, "is running in the background");
                        NotifyInTaskbar = true;
                    }
                    break;
                case WindowState.Normal:
                case WindowState.Maximized:
                    notifyIcon.Visible = false;
                    ShowInTaskbar = true;
                    this.Activate();

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
            preNavItemTag = "ControllerPage";
            NavView_Navigate(preNavItemTag);
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

            if (ContentFrame.SourcePageType is not null)
            {
                var preNavPageType = ContentFrame.CurrentSourcePageType;
                var preNavPageName = preNavPageType.Name;
                PipeClient.SendMessage(new PipeNavigation((string)preNavPageName));

                var NavViewItem = navView.MenuItems
                    .OfType<NavigationViewItem>()
                    .Where(n => n.Tag.Equals(preNavPageName)).FirstOrDefault();

                if (!(NavViewItem is null))
                    navView.SelectedItem = NavViewItem;

                navView.Header = new TextBlock() { Text = (string)((Page)e.Content).Title };
            }
        }
        #endregion

        private async void OnSystemStatusChanged(PowerManager.SystemStatus status, SystemStatus prevStatus)
        {
            if (status == prevStatus)
                return;

            switch (status)
            {
                case PowerManager.SystemStatus.SystemReady:
                    {
                        switch (prevStatus)
                        {
                            case SystemStatus.SystemBooting:
                                // cold boot
                                break;
                            case SystemStatus.SystemPending:
                                // resume from sleep
                                Thread.Sleep(2000);
                                break;
                        }

                        // restore inputs manager
                        InputsManager.TriggerRaised += InputsManager_TriggerRaised;
                        InputsManager.Start();

                        // start timer manager
                        TimerManager.Start();
                    }
                    break;
                case PowerManager.SystemStatus.SystemPending:
                    {
                        // stop timer manager
                        TimerManager.Stop();

                        // clear pipes
                        PipeClient.ClearQueue();

                        // pause inputs manager
                        InputsManager.TriggerRaised -= InputsManager_TriggerRaised;
                        InputsManager.Stop();
                    }
                    break;
            }
        }
    }
}
