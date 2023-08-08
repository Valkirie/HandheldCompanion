using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.Windows;
using Inkore.UI.WPF.Modern.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static HandheldCompanion.Managers.InputsHotkey;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.Managers.ServiceControllerStatus;

namespace HandheldCompanion.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : GamepadWindow
{
    // devices vars
    public static IDevice CurrentDevice;

    // page vars
    private static readonly Dictionary<string, Page> _pages = new();

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
    private static readonly List<Manager> _managers = new();
    public static ServiceManager serviceManager;
    public static TaskManager taskManager;
    public static PerformanceManager performanceManager;
    public static UpdateManager updateManager;

    public static string CurrentExe, CurrentPath, CurrentPathService;

    private static MainWindow CurrentWindow;
    public static FileVersionInfo fileVersionInfo;

    public static string InstallPath;
    public static string SettingsPath;
    private bool appClosing;
    private bool IsReady;
    private readonly NotifyIcon notifyIcon;
    private bool NotifyInTaskbar;
    private string preNavItemTag;

    private WindowState prevWindowState;
    private SplashScreen splashScreen;

    private const int WM_QUERYENDSESSION = 0x0011;

    public MainWindow(FileVersionInfo _fileVersionInfo, Assembly CurrentAssembly)
    {
        InitializeComponent();

        fileVersionInfo = _fileVersionInfo;
        CurrentWindow = this;

        // used by gamepad navigation
        Tag = "MainWindow";

        // get process
        var process = Process.GetCurrentProcess();

        // fix touch support
        var tablets = Tablet.TabletDevices;

        // define current directory
        InstallPath = AppDomain.CurrentDomain.BaseDirectory;
        SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HandheldCompanion");

        // initialiaze path
        if (!Directory.Exists(SettingsPath))
            Directory.CreateDirectory(SettingsPath);

        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        // initialize notifyIcon
        notifyIcon = new NotifyIcon
        {
            Text = Title,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
            Visible = false,
            ContextMenuStrip = new ContextMenuStrip()
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
        Title += $" ({fileVersionInfo.FileVersion})";

        // initialize device
        CurrentDevice = IDevice.GetDefault();
        CurrentDevice.PullSensors();

        if (SettingsManager.GetBoolean("FirstStart"))
        {
            // initialize splash screen on first start only
            splashScreen = new SplashScreen();
            splashScreen.Show();

            // handle a few edge-cases on first start
            if (CurrentDevice.GetType() == typeof(SteamDeck))
            {
                // we shouldn't hide the steam deck controller by default
                SettingsManager.SetProperty("HIDcloakonconnect", false);
            }

            // update FirstStart
            SettingsManager.SetProperty("FirstStart", false);
        }

        // load manager(s)
        // todo: make me static
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

        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
        ControllerManager.Start();
        HotkeysManager.Start();

        DeviceManager.UsbDeviceArrived += GenericDeviceUpdated;
        DeviceManager.UsbDeviceRemoved += GenericDeviceUpdated;
        DeviceManager.Start();

        PlatformManager.Start();
        OSDManager.Start();
        LayoutManager.Start();

        ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        ProcessManager.Start();

        PowerManager.SystemStatusChanged += OnSystemStatusChanged;
        PowerManager.Start();

        SystemManager.Start();

        // start managers asynchroneously
        foreach (var manager in _managers)
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
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // windows shutting down event
        if (msg == WM_QUERYENDSESSION)
        {
            if (SettingsManager.GetBoolean("VirtualControllerForceOrder"))
            {
                // disable physical controllers when shutting down to ensure we can give the first order to virtual controller on next boot
                foreach (var physicalControllerInstanceId in SettingsManager.GetStringCollection("PhysicalControllerInstanceIds"))
                {
                    PnPUtil.DisableDevice(physicalControllerInstanceId);
                }
            }
        }

        return IntPtr.Zero;
    }

    private void ProcessManager_ForegroundChanged(Controls.ProcessEx processEx, Controls.ProcessEx backgroundEx)
    {
        // unset flag
        SystemPending = false;
    }

    private void ControllerManager_ControllerSelected(IController Controller)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            GamepadUISelect.Glyph = Controller.GetGlyph(ButtonFlags.B1);
            GamepadUISelect.Foreground = Controller.GetGlyphColor(ButtonFlags.B1);
            if (GamepadUISelect.Foreground is null)
                GamepadUISelect.SetResourceReference(ForegroundProperty,
                    "SystemControlForegroundBaseMediumBrush");

            GamepadUIBack.Glyph = Controller.GetGlyph(ButtonFlags.B2);
            GamepadUIBack.Foreground = Controller.GetGlyphColor(ButtonFlags.B2);
            if (GamepadUIBack.Foreground is null)
                GamepadUIBack.SetResourceReference(ForegroundProperty,
                    "SystemControlForegroundBaseMediumBrush");
        });
    }

    private void GamepadFocusManagerOnFocused(Control control)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // todo : localize me
            string controlType = control.GetType().Name;
            switch (controlType)
            {
                default:
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUISelectDesc.Text = "Select";

                        GamepadUIBack.Visibility = Visibility.Visible;
                        GamepadUIBackDesc.Text = "Back";
                    }
                    break;

                case "Slider":
                    {
                        GamepadUISelect.Visibility = Visibility.Collapsed;
                        GamepadUIBack.Visibility = Visibility.Visible;
                    }
                    break;

                case "NavigationViewItem":
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUISelectDesc.Text = "Navigate";

                        GamepadUIBack.Visibility = Visibility.Collapsed;
                    }
                    break;
            }
        });
    }

    private void AddNotifyIconItem(string name, object tag = null)
    {
        tag ??= string.Concat(name.Where(c => !char.IsWhiteSpace(c)));

        var menuItemMainWindow = new ToolStripMenuItem(name);
        menuItemMainWindow.Tag = tag;
        menuItemMainWindow.Click += MenuItem_Click;
        notifyIcon.ContextMenuStrip.Items.Add(menuItemMainWindow);
    }

    private void AddNotifyIconSeparator()
    {
        var separator = new ToolStripSeparator();
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

                var DesktopLayout = Convert.ToBoolean(value);
                SettingsManager.SetProperty("DesktopLayoutEnabled", DesktopLayout, false, true);
                break;
        }

        if (PipeClient.IsConnected)
        {
            var settings = new PipeClientSettings();
            settings.Settings.Add(name, Convert.ToString(value));

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
        layoutPage = new LayoutPage("layout", navView);

        // store pages
        _pages.Add("ControllerPage", controllerPage);
        _pages.Add("ProfilesPage", profilesPage);
        _pages.Add("AboutPage", aboutPage);
        _pages.Add("OverlayPage", overlayPage);
        _pages.Add("SettingsPage", settingsPage);
        _pages.Add("HotkeysPage", hotkeysPage);
        _pages.Add("LayoutPage", layoutPage);

        // handle controllerPage events
        controllerPage.HIDchanged += HID => { overlayModel.UpdateHIDMode(HID); };
        controllerPage.HIDchanged += ControllerPage_HIDchanged;
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
        serviceManager = new ServiceManager("ControllerService", Properties.Resources.ServiceName,
            Properties.Resources.ServiceDescription);
        taskManager = new TaskManager("HandheldCompanion", CurrentExe);
        performanceManager = new PerformanceManager();
        updateManager = new UpdateManager();

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
            _ = Dialog.ShowAsync($"{Properties.Resources.MainWindow_ServiceManager}",
                $"{Properties.Resources.MainWindow_ServiceManagerStartIssue}\n\n{message}",
                ContentDialogButton.Primary, null, $"{Properties.Resources.MainWindow_OK}");
        };
        serviceManager.StopFailed += status =>
        {
            _ = Dialog.ShowAsync($"{Properties.Resources.MainWindow_ServiceManager}",
                $"{Properties.Resources.MainWindow_ServiceManagerStopIssue}", ContentDialogButton.Primary, null,
                $"{Properties.Resources.MainWindow_OK}");
        };
    }

    private void GenericDeviceUpdated(PnPDevice device, DeviceEventArgs obj)
    {
        CurrentDevice.PullSensors();

        aboutPage.UpdateDevice(device);
        settingsPage.UpdateDevice(device);
    }

    private void InputsManager_TriggerRaised(string listener, InputsChord input, InputsHotkeyType type, bool IsKeyDown,
        bool IsKeyUp)
    {
        switch (listener)
        {
            case "quickTools":
                overlayquickTools.ToggleVisibility();
                break;
            case "overlayGamepad":
                overlayModel.ToggleVisibility();
                break;
            case "overlayTrackpads":
                overlayTrackpad.ToggleVisibility();
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
                overlayquickTools.ToggleVisibility();
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
                Close();
                break;
        }
    }

    private void OnClientConnected()
    {
        // (re)send all local settings to server at once
        var settings = new PipeClientSettings();

        foreach (var values in SettingsManager.GetProperties())
            settings.Settings.Add(values.Key, Convert.ToString(values.Value));

        PipeClient.SendMessage(settings);
    }

    private void OnClientDisconnected()
    {
        // do something
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // load gamepad navigation maanger
        gamepadFocusManager = new(this, ContentFrame);

        HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
        source.AddHook(WndProc); // Hook into the window's message loop
    }

    private void ControllerPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsReady)
            return;

        // home page has loaded, display main window
        WindowState = SettingsManager.GetBoolean("StartMinimized")
            ? WindowState.Minimized
            : (WindowState)SettingsManager.GetInt("MainWindowState");
        prevWindowState = (WindowState)SettingsManager.GetInt("MainWindowPrevState");

        IsReady = true;
    }

    private void ControllerPage_HIDchanged(HIDmode controllerMode)
    {
        CurrentDevice.SetKeyPressDelay(controllerMode);
    }

    public void UpdateSettings(Dictionary<string, string> args)
    {
        foreach (var pair in args)
        {
            var name = pair.Key;
            var property = pair.Value;

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
                var toast = (PipeServerToast)message;
                ToastManager.SendToast(toast.title, toast.content, toast.image);
                break;

            case PipeCode.SERVER_SETTINGS:
                var settings = (PipeServerSettings)message;
                UpdateSettings(settings.Settings);
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

    private bool SystemPending;
    private async void OnSystemStatusChanged(PowerManager.SystemStatus status, PowerManager.SystemStatus prevStatus)
    {
        if (status == prevStatus)
            return;

        switch (status)
        {
            case PowerManager.SystemStatus.SystemReady:
                {
                    switch (prevStatus)
                    {
                        case PowerManager.SystemStatus.SystemBooting:
                            // cold boot
                            break;
                        case PowerManager.SystemStatus.SystemPending:
                            // resume from sleep
                            break;
                    }

                    new Thread(() =>
                    {
                        // wait for all HIDs to be ready
                        while (!CurrentDevice.IsReady())
                            Thread.Sleep(500);

                        // open current device (threaded to avoid device to hang)
                        var success = CurrentDevice.Open();
                        if (!success)
                            LogManager.LogError("Failed to open device");

                        // restore device settings
                        CurrentDevice.SetFanControl(SettingsManager.GetBoolean("QuietModeToggled"));
                        CurrentDevice.SetFanDuty(SettingsManager.GetDouble("QuietModeDuty"));

                    }).Start();

                    // restore inputs manager
                    InputsManager.TriggerRaised += InputsManager_TriggerRaised;
                    InputsManager.Start();

                    // start timer manager
                    TimerManager.Start();
                }
                break;
            case PowerManager.SystemStatus.SystemPending:
                {
                    // set flag
                    SystemPending = true;

                    // close current device
                    CurrentDevice.Close();

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

    #region UI

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not null)
        {
            var navItem = (NavigationViewItem)args.InvokedItemContainer;
            var navItemTag = (string)navItem.Tag;

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
        var _page = item.Value;

        // Get the page type before navigation so you can prevent duplicate
        // entries in the backstack.
        var preNavPageType = ContentFrame.CurrentSourcePageType;

        // Only navigate if the selected page isn't currently loaded.
        if (!(_page is null) && !Equals(preNavPageType, _page)) NavView_Navigate(_page);
    }

    public static void NavView_Navigate(Page _page)
    {
        CurrentWindow.ContentFrame.Navigate(_page);
        CurrentWindow.scrollViewer.ScrollToTop();
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
        PowerManager.Stop();
        ProcessManager.Stop();
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

    bool CloseOverride = false;

    private async void Window_Closing(object sender, CancelEventArgs e)
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

        if(SettingsManager.GetBoolean("VirtualControllerForceOrder") && !CloseOverride)
        {
            // we have to cancel closing the window to be able to prompt the user
            e.Cancel = true;

            // warn user when attempting to close HC while using Improve virtual controller detection
            var result = Dialog.ShowAsync(
                Properties.Resources.MainWindow_VirtualControllerForceOrderCloseTitle,
                Properties.Resources.MainWindow_VirtualControllerForceOrderCloseText,
                ContentDialogButton.Primary, null,
                Properties.Resources.MainWindow_VirtualControllerForceOrderClosePrimary,
                Properties.Resources.MainWindow_VirtualControllerForceOrderCloseSecondary);

            await result;

            switch (result.Result)
            {
                case ContentDialogResult.Primary:
                    CloseOverride = true;
                    Close();
                    break;
                case ContentDialogResult.Secondary:
                    return;
            }
        }

        // stop service with companion
        if (SettingsManager.GetBoolean("HaltServiceWithCompanion"))
            // only halt process if start mode isn't set to "Automatic"
            if (serviceManager.type != ServiceStartMode.Automatic)
                _ = serviceManager.StopServiceAsync();
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

                Activate();
                Topmost = true;  // important
                Topmost = false; // important
                Focus();

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

    private void GamepadWindow_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!e.NewFocus.GetType().IsSubclassOf(typeof(Control)))
            return;

        GamepadFocusManagerOnFocused((Control)e.NewFocus);
    }

    private void GamepadWindow_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // do something
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
            PipeClient.SendMessage(new PipeNavigation(preNavPageName));

            var NavViewItem = navView.MenuItems
                .OfType<NavigationViewItem>().FirstOrDefault(n => n.Tag.Equals(preNavPageName));

            if (!(NavViewItem is null))
                navView.SelectedItem = NavViewItem;

            navView.Header = new TextBlock { Text = (string)((Page)e.Content).Title };
        }
    }

    #endregion
}