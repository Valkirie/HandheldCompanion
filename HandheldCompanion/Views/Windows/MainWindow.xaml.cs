using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.Windows;
using Microsoft.Win32;
using ModernWpf.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Navigation;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.Managers.ServiceControllerStatus;

namespace HandheldCompanion.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static FileVersionInfo fileVersionInfo;
        private static Stopwatch stopwatch = new();

        // devices vars
        public static Device handheldDevice;

        // page vars
        private static Dictionary<string, Page> _pages = new();
        private string preNavItemTag;

        public static ControllerPage controllerPage;
        public static ProfilesPage profilesPage;
        public static SettingsPage settingsPage;
        public static AboutPage aboutPage;
        public static OverlayPage overlayPage;
        public static HotkeysPage hotkeysPage;

        // overlay(s) vars
        public static OverlayModel overlayModel;
        public static OverlayTrackpad overlayTrackpad;
        public static OverlayQuickTools overlayquickTools;

        // connectivity vars
        public static PipeClient pipeClient;

        // Hidder vars
        public static HidHide Hidder;

        // manager(s) vars
        private static List<Manager> _managers = new(); 
        public static InputsManager inputsManager;
        public static ToastManager toastManager;
        public static ProcessManager processManager;
        public static ServiceManager serviceManager;
        public static ProfileManager profileManager;
        public static TaskManager taskManager;
        public static CheatManager cheatManager;
        public static SystemManager systemManager;
        public static PowerManager powerManager;
        public static UpdateManager updateManager;

        private WindowState prevWindowState;
        private NotifyIcon notifyIcon;

        public static string CurrentExe, CurrentPath, CurrentPathService;
        private bool appClosing;

        private static MainWindow mainWindow;
        public MainWindow()
        {
            InitializeComponent();
            mainWindow = this;

            // fix touch support
            var tablets = Tablet.TabletDevices;

            // define current directory
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            // initialize log manager
            LogManager.Initialize("HandheldCompanion");

            // listen to system events
            SystemEvents.PowerModeChanged += OnPowerChangeAsync;

            // initialize log
            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            LogManager.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

            // initialize notifyIcon
            notifyIcon = new()
            {
                Text = Title,
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Visible = false,
                ContextMenuStrip = new()
            };

            notifyIcon.DoubleClick += (sender, e) =>
            {
                WindowState = prevWindowState;
            };

            foreach (NavigationViewItem item in navView.FooterMenuItems)
            {
                ToolStripMenuItem menuItem = new ToolStripMenuItem(item.Content.ToString());
                menuItem.Tag = item.Tag;
                menuItem.Click += MenuItem_Click;

                notifyIcon.ContextMenuStrip.Items.Add(menuItem);
            }

            ToolStripSeparator separator = new ToolStripSeparator();
            notifyIcon.ContextMenuStrip.Items.Add(separator);

            ToolStripMenuItem menuItemExit = new ToolStripMenuItem("Exit"); // todo: localize me
            menuItemExit.Tag = menuItemExit.Text;
            menuItemExit.Click += MenuItem_Click;
            notifyIcon.ContextMenuStrip.Items.Add(menuItemExit);

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathService))
            {
                LogManager.LogCritical("Controller Service executable is missing");
                throw new InvalidOperationException();
            }

            // initialize HidHide
            Hidder = new HidHide();
            Hidder.RegisterApplication(CurrentExe);

            // initialize title
            this.Title += $" ({fileVersionInfo.FileVersion})";

            // initialize device
            handheldDevice = Device.GetDefault();
            handheldDevice.PullSensors();

            // initialize pipe client
            pipeClient = new PipeClient("ControllerService");
            pipeClient.ServerMessage += OnServerMessage;
            pipeClient.Connected += OnClientConnected;
            pipeClient.Disconnected += OnClientDisconnected;
            pipeClient.Open();

            // load manager(s)
            loadManagers();

            // load window(s)
            loadWindows();

            // load page(s)
            loadPages();

            // start manager(s)
            foreach (Manager manager in _managers)
            {
                if (manager.GetType() == typeof(InputsManager))
                    manager.Start();
                else
                    {
                    new Thread(() => {
                        Thread.CurrentThread.IsBackground = true;
                        manager.Start();
                    }).Start();
                }
            }

            // update Position and Size
            this.Height = (int)Math.Max(this.MinHeight, Properties.Settings.Default.MainWindowHeight);
            this.Width = (int)Math.Max(this.MinWidth, Properties.Settings.Default.MainWindowWidth);

            this.Left = Math.Min(SystemParameters.PrimaryScreenWidth - this.MinWidth, Properties.Settings.Default.MainWindowLeft);
            this.Top = Math.Min(SystemParameters.PrimaryScreenHeight - this.MinHeight, Properties.Settings.Default.MainWindowTop);

            // pull settings
            WindowState = Properties.Settings.Default.StartMinimized ? WindowState.Minimized : (WindowState)Properties.Settings.Default.MainWindowState;
            prevWindowState = (WindowState)Properties.Settings.Default.MainWindowPrevState;
        }

        private void loadPages()
        {
            stopwatch.Restart();
            LogManager.LogDebug("Loading pages...");

            // initialize pages
            controllerPage = new ControllerPage("controller");
            profilesPage = new ProfilesPage("profiles");
            settingsPage = new SettingsPage("settings");
            aboutPage = new AboutPage("about");
            overlayPage = new OverlayPage("overlay");
            hotkeysPage = new HotkeysPage("hotkeys");

            // store pages
            _pages.Add("ControllerPage", controllerPage);
            _pages.Add("ProfilesPage", profilesPage);
            _pages.Add("AboutPage", aboutPage);
            _pages.Add("OverlayPage", overlayPage);
            _pages.Add("SettingsPage", settingsPage);
            _pages.Add("HotkeysPage", hotkeysPage);

            // handle settingsPage events
            settingsPage.SettingValueChanged += (name, value) =>
            {
                // todo : create a settings manager
                profilesPage.SettingsPage_SettingValueChanged(name, value);
                overlayquickTools.performancePage.SettingsPage_SettingValueChanged(name, value);
                overlayquickTools.profilesPage.SettingsPage_SettingValueChanged(name, value);

                switch (name)
                {
                    case "toast_notification":
                        toastManager.Enabled = (bool)value;
                        break;
                    case "autostart":
                        taskManager.UpdateTask((bool)value);
                        break;
                    case "service_startup_type":
                        serviceManager.SetStartType((ServiceStartMode)value);
                        break;
                }
            };

            // handle controllerPage events
            controllerPage.HIDchanged += (HID) =>
            {
                overlayModel.UpdateHIDMode(HID);
            };
            controllerPage.ControllerChanged += (Controller) =>
            {
                cheatManager.UpdateController(Controller); // update me
                inputsManager.UpdateController(Controller);
            };

            stopwatch.Stop();
            LogManager.LogDebug("Loaded in {0}", stopwatch.Elapsed);
        }

        private void loadWindows()
        {
            stopwatch.Restart();
            LogManager.LogDebug("Loading windows...");

            // initialize overlay
            overlayModel = new OverlayModel();
            overlayTrackpad = new OverlayTrackpad();
            overlayquickTools = new OverlayQuickTools();

            stopwatch.Stop();
            LogManager.LogDebug("Loaded in {0}", stopwatch.Elapsed);
        }

        private void loadManagers()
        {
            stopwatch.Restart();
            LogManager.LogDebug("Loading managers...");

            // initialize managers
            toastManager = new ToastManager("HandheldCompanion");
            toastManager.Enabled = Properties.Settings.Default.ToastEnable;

            processManager = new();
            profileManager = new();
            inputsManager = new();
            serviceManager = new ServiceManager("ControllerService", Properties.Resources.ServiceName, Properties.Resources.ServiceDescription);
            taskManager = new TaskManager("HandheldCompanion", CurrentExe);
            cheatManager = new();
            systemManager = new();
            powerManager = new();
            updateManager = new();

            // store managers
            _managers.Add(toastManager);
            _managers.Add(processManager);
            _managers.Add(profileManager);
            _managers.Add(inputsManager);
            _managers.Add(serviceManager);
            _managers.Add(taskManager);
            _managers.Add(cheatManager);
            _managers.Add(systemManager);
            _managers.Add(powerManager);
            _managers.Add(updateManager);

            // hook into managers events
            inputsManager.TriggerRaised += InputsManager_TriggerRaised;

            serviceManager.Updated += OnServiceUpdate;
            serviceManager.Ready += () =>
            {
                if (Properties.Settings.Default.StartServiceWithCompanion)
                {
                    if (!serviceManager.Exists())
                        serviceManager.CreateService(CurrentPathService);

                    _ = serviceManager.StartServiceAsync();
                }
            };
            serviceManager.StartFailed += (status) =>
            {
                _ = Dialog.ShowAsync($"{Properties.Resources.MainWindow_ServiceManager}", $"{Properties.Resources.MainWindow_ServiceManagerStartIssue}", ContentDialogButton.Primary, null, $"{Properties.Resources.MainWindow_OK}");
            };
            serviceManager.StopFailed += (status) =>
            {
                _ = Dialog.ShowAsync($"{Properties.Resources.MainWindow_ServiceManager}", $"{Properties.Resources.MainWindow_ServiceManagerStopIssue}", ContentDialogButton.Primary, null, $"{Properties.Resources.MainWindow_OK}");
            };

            taskManager.UpdateTask(Properties.Settings.Default.RunAtStartup);

            cheatManager.Cheated += (cheat) =>
            {
                switch (cheat)
                {
                    case "OverlayControllerFisherPrice":
                        overlayPage?.UnlockToyController();
                        break;
                }
            };

            systemManager.SerialArrived += SystemManager_Updated;
            systemManager.SerialRemoved += SystemManager_Updated;

            stopwatch.Stop();
            LogManager.LogDebug("Loaded in {0}", stopwatch.Elapsed);
        }

        private void SystemManager_Updated(PnPDevice device)
        {
            handheldDevice.PullSensors();

            aboutPage.UpdateDevice(device);
            settingsPage.UpdateDevice(device);
        }

        private void InputsManager_TriggerRaised(string listener, TriggerInputs input)
        {
            this.Dispatcher.Invoke(() =>
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
                }
            });
        }

        private void MenuItem_Click(object? sender, EventArgs e)
        {
            switch (((ToolStripMenuItem)sender).Tag)
            {
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

        private void OnClientConnected(object sender)
        {
            // send all local settings to server ?
            PipeClientSettings settings = new PipeClientSettings();
            foreach (SettingsProperty currentProperty in Properties.Settings.Default.Properties)
                settings.settings.Add(currentProperty.Name, Properties.Settings.Default[currentProperty.Name]);
            pipeClient?.SendMessage(settings);
        }

        private void OnClientDisconnected(object sender)
        {
            // do something
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // do something
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

        /*
         * Stop
         * Start
         * Deploy
         * Remove
         */

        private void OnServiceUpdate(ServiceControllerStatus status, int mode)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                        b_ServiceStop.IsEnabled = false;
                        b_ServiceStart.IsEnabled = true;
                        b_ServiceInstall.IsEnabled = false;
                        b_ServiceDelete.IsEnabled = true;

                        if (notifyIcon.ContextMenuStrip != null)
                        {
                            notifyIcon.ContextMenuStrip.Items[0].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[1].Enabled = true;
                            notifyIcon.ContextMenuStrip.Items[2].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[3].Enabled = true;
                        }
                        break;
                    case ServiceControllerStatus.Running:
                        b_ServiceStop.IsEnabled = true;
                        b_ServiceStart.IsEnabled = false;
                        b_ServiceInstall.IsEnabled = false;
                        b_ServiceDelete.IsEnabled = false;

                        if (notifyIcon.ContextMenuStrip != null)
                        {
                            notifyIcon.ContextMenuStrip.Items[0].Enabled = true;
                            notifyIcon.ContextMenuStrip.Items[1].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[2].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[3].Enabled = false;
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

                        if (notifyIcon.ContextMenuStrip != null)
                        {
                            notifyIcon.ContextMenuStrip.Items[0].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[1].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[2].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[3].Enabled = false;
                        }
                        break;
                    default:
                        b_ServiceStop.IsEnabled = false;
                        b_ServiceStart.IsEnabled = false;
                        b_ServiceInstall.IsEnabled = true;
                        b_ServiceDelete.IsEnabled = false;

                        if (notifyIcon.ContextMenuStrip != null)
                        {
                            notifyIcon.ContextMenuStrip.Items[0].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[1].Enabled = false;
                            notifyIcon.ContextMenuStrip.Items[2].Enabled = true;
                            notifyIcon.ContextMenuStrip.Items[3].Enabled = false;
                        }
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

        #region UI
        private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
                string navItemTag = (string)navItem.Tag;

                switch (navItemTag)
                {
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
            mainWindow.ContentFrame.Navigate(_page);
        }

        private void navView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            processManager.Stop();
            serviceManager.Stop();
            profileManager.Stop();
            systemManager.Stop();
            powerManager.Stop();
            toastManager.Stop();

            notifyIcon.Visible = false;
            notifyIcon.Dispose();

            overlayModel.Close();
            overlayTrackpad.Close();
            overlayquickTools.Close(true);

            if (pipeClient.connected)
                pipeClient.Close();

            cheatManager.Stop();
            inputsManager.Stop();

            // stop listening to system events
            SystemEvents.PowerModeChanged += OnPowerChangeAsync;

            // closing page(s)
            controllerPage.Page_Closed();
            profilesPage.Page_Closed();
            settingsPage.Page_Closed();
            overlayPage.Page_Closed();
            hotkeysPage.Page_Closed();

            // force kill application
            Environment.Exit(0);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // position and size settings
            switch (WindowState)
            {
                case WindowState.Normal:
                    Properties.Settings.Default.MainWindowLeft = this.Left;
                    Properties.Settings.Default.MainWindowTop = this.Top;

                    Properties.Settings.Default.MainWindowWidth = this.ActualWidth;
                    Properties.Settings.Default.MainWindowHeight = this.ActualHeight;
                    break;
                case WindowState.Maximized:
                    Properties.Settings.Default.MainWindowLeft = 0;
                    Properties.Settings.Default.MainWindowTop = 0;

                    Properties.Settings.Default.MainWindowWidth = SystemParameters.MaximizedPrimaryScreenWidth;
                    Properties.Settings.Default.MainWindowHeight = SystemParameters.MaximizedPrimaryScreenHeight;

                    break;
            }

            Properties.Settings.Default.MainWindowState = (int)WindowState;
            Properties.Settings.Default.MainWindowPrevState = (int)prevWindowState;

            if (Properties.Settings.Default.CloseMinimises && !appClosing)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                return;
            }

            // stop service with companion
            if (Properties.Settings.Default.HaltServiceWithCompanion)
                _ = serviceManager.StopServiceAsync();

            Properties.Settings.Default.Save();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    notifyIcon.Visible = true;
                    ShowInTaskbar = false;
                    toastManager.SendToast(Title, "The application is running in the background.");
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

            if (ContentFrame.SourcePageType != null)
            {
                var preNavPageType = ContentFrame.CurrentSourcePageType;
                var preNavPageName = preNavPageType.Name;
                pipeClient.SendMessage(new PipeNavigation((string)preNavPageName));

                var NavViewItem = navView.MenuItems
                    .OfType<NavigationViewItem>()
                    .Where(n => n.Tag.Equals(preNavPageName)).FirstOrDefault();

                if (!(NavViewItem is null))
                    navView.SelectedItem = NavViewItem;

                navView.Header = new TextBlock() { Text = (string)((Page)e.Content).Title };
            }
        }
        #endregion

        private async void OnPowerChangeAsync(object s, PowerModeChangedEventArgs e)
        {
            LogManager.LogInformation("Device power mode set to {0}", e.Mode);

            switch (e.Mode)
            {
                default:
                case PowerModes.StatusChange:
                    break;
                case PowerModes.Suspend:
                    {
                        //pause inputs manager
                        inputsManager.Stop();
                    }
                    break;
                case PowerModes.Resume:
                    {
                        // restore inputs manager
                        inputsManager.Start();
                    }
                    break;
            }
        }
    }
}
