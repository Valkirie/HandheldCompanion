using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.UI;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shell;
using Windows.UI.ViewManagement;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = System.Windows.Controls.Page;
using RadioButton = System.Windows.Controls.RadioButton;

namespace HandheldCompanion.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : GamepadWindow
{
    // devices vars
    private static IDevice CurrentDevice;

    // page vars
    private static readonly Dictionary<string, Page> _pages = [];

    public static ControllerPage controllerPage;
    public static DevicePage devicePage;
    public static PerformancePage performancePage;
    public static ProfilesPage profilesPage;
    public static SettingsPage settingsPage;
    public static AboutPage aboutPage;
    public static OverlayPage overlayPage;
    public static HotkeysPage hotkeysPage;
    public static LayoutPage layoutPage;
    public static NotificationsPage notificationsPage;

    // overlay(s) vars
    public static OverlayModel overlayModel;
    public static OverlayTrackpad overlayTrackpad;
    public static OverlayQuickTools overlayquickTools;

    public static string CurrentExe, CurrentPath;

    private static MainWindow CurrentWindow;
    public static FileVersionInfo fileVersionInfo;

    public static string CurrentPageName = string.Empty;

    private bool appClosing;
    private readonly NotifyIcon notifyIcon;
    private bool NotifyInTaskbar;
    public string prevNavItemTag;

    private WindowState prevWindowState;
    public static SplashScreen SplashScreen;

    public static UISettings uiSettings;

    private const int WM_QUERYENDSESSION = 0x0011;
    private const int WM_DISPLAYCHANGE = 0x007e;
    private const int WM_DEVICECHANGE = 0x0219;

    public MainWindow(FileVersionInfo _fileVersionInfo, Assembly CurrentAssembly)
    {
        // initialize splash screen
        SplashScreen = new SplashScreen();

        InitializeComponent();
        this.Tag = "MainWindow";

        fileVersionInfo = _fileVersionInfo;
        CurrentWindow = this;

        // get last version
        Version LastVersion = Version.Parse(ManagerFactory.settingsManager.GetString("LastVersion"));
        bool FirstStart = LastVersion == Version.Parse("0.0.0.0");
        bool NewUpdate = LastVersion != Version.Parse(fileVersionInfo.FileVersion);
#if !DEBUG
        if (NewUpdate) SplashScreen.Show();
#endif

        // used by system manager, controller manager
        uiSettings = new UISettings();

        // define current directory
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        // initialize XInputWrapper
        XInputPlus.ExtractXInputPlusLibraries();

        // initialize notifyIcon
        notifyIcon = new NotifyIcon
        {
            Text = Title,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
            Visible = false,
            ContextMenuStrip = new ContextMenuStrip()
        };

        notifyIcon.DoubleClick += (sender, e) => { SwapWindowState(); };

        AddNotifyIconItem(Properties.Resources.MainWindow_MainWindow);
        AddNotifyIconItem(Properties.Resources.MainWindow_QuickTools);

        AddNotifyIconSeparator();

        AddNotifyIconItem(Properties.Resources.MainWindow_Exit);

        // paths
        Process process = Process.GetCurrentProcess();
        CurrentExe = process.MainModule.FileName;
        CurrentPath = AppDomain.CurrentDomain.BaseDirectory;

        // initialize HidHide
        HidHide.RegisterApplication(CurrentExe);

        // collect details from MotherboardInfo
        MotherboardInfo.Collect();

        // initialize title
        Title += $" ({fileVersionInfo.FileVersion})";

        // initialize device
        CurrentDevice = IDevice.GetCurrent();
        CurrentDevice.PullSensors();

        if (FirstStart)
        {
            if (CurrentDevice is SteamDeck steamDeck)
            {
                // do something
            }
            else if (CurrentDevice is AYANEOFlipDS flipDS)
            {
                // set Quicktools to Maximize on bottom screen
                ManagerFactory.settingsManager.SetProperty("QuickToolsLocation", 2);
                ManagerFactory.settingsManager.SetProperty("QuickToolsDeviceName", "AYANEOQHD");
            }

            ManagerFactory.settingsManager.SetProperty("FirstStart", false);
        }

        // initialize UI sounds board
        UISounds uiSounds = new UISounds();

        // load window(s)
        loadWindows();

        // load page(s)
        overlayquickTools.loadPages();
        loadPages();

        // manage events
        SystemManager.SystemStatusChanged += OnSystemStatusChanged;
        ManagerFactory.deviceManager.UsbDeviceArrived += GenericDeviceUpdated;
        ManagerFactory.deviceManager.UsbDeviceRemoved += GenericDeviceUpdated;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

        // prepare toast manager
        ToastManager.Start();

        // start non-static managers
        foreach (IManager manager in ManagerFactory.Managers)
            Task.Run(() => manager.Start());

        // start static managers
        // todo: make them non-static
        List<Task> tasks = new List<Task>
        {
            Task.Run(() => OSDManager.Start()),
            Task.Run(() => SystemManager.Start()),
            Task.Run(() => DynamicLightingManager.Start()),
            Task.Run(() => VirtualManager.Start()),
            Task.Run(() => SensorsManager.Start()),
            Task.Run(() => ControllerManager.Start()),
            Task.Run(() => PlatformManager.Start()),
            Task.Run(() => TaskManager.Start(CurrentExe)),
            Task.Run(() => PerformanceManager.Start()),
            Task.Run(() => UpdateManager.Start())
        };

        // start non-threaded managers
        InputsManager.Start();
        TimerManager.Start();
        ManagerFactory.settingsManager.Start();

        // Load MVVM pages after the Models / data have been created.
        overlayquickTools.LoadPages_MVVM();
        LoadPages_MVVM();

        // update Position and Size
        Height = (int)Math.Max(MinHeight, ManagerFactory.settingsManager.GetDouble("MainWindowHeight"));
        Width = (int)Math.Max(MinWidth, ManagerFactory.settingsManager.GetDouble("MainWindowWidth"));
        Left = Math.Min(SystemParameters.PrimaryScreenWidth - MinWidth, ManagerFactory.settingsManager.GetDouble("MainWindowLeft"));
        Top = Math.Min(SystemParameters.PrimaryScreenHeight - MinHeight, ManagerFactory.settingsManager.GetDouble("MainWindowTop"));
        navView.IsPaneOpen = ManagerFactory.settingsManager.GetBoolean("MainWindowIsPaneOpen");

        // update LastVersion
        ManagerFactory.settingsManager.SetProperty("LastVersion", fileVersionInfo.FileVersion);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_DISPLAYCHANGE:
            case WM_DEVICECHANGE:
                ManagerFactory.deviceManager.RefreshDisplayAdapters();
                break;
            case WM_QUERYENDSESSION:
                break;
        }

        return IntPtr.Zero;
    }

    private void ControllerManager_ControllerSelected(IController Controller)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            GamepadUISelectIcon.Glyph = Controller.GetGlyph(ButtonFlags.B1);
            GamepadUISelectIcon.Foreground = new SolidColorBrush(Controller.GetGlyphColor(ButtonFlags.B1));

            GamepadUIBackIcon.Glyph = Controller.GetGlyph(ButtonFlags.B2);
            GamepadUIBackIcon.Foreground = new SolidColorBrush(Controller.GetGlyphColor(ButtonFlags.B2));

            GamepadUIToggleIcon.Glyph = Controller.GetGlyph(ButtonFlags.B4);
            GamepadUIToggleIcon.Foreground = new SolidColorBrush(Controller.GetGlyphColor(ButtonFlags.B4));
        });
    }

    private void GamepadFocusManagerOnFocused(Control control)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // todo : localize me
            string controlType = control.GetType().Name;
            switch (controlType)
            {
                default:
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUIBack.Visibility = Visibility.Visible;
                        GamepadUIToggle.Visibility = Visibility.Collapsed;

                        GamepadUISelectDesc.Text = Properties.Resources.MainWindow_Select;
                        GamepadUIBackDesc.Text = Properties.Resources.MainWindow_Back;
                    }
                    break;

                case "Button":
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUIBack.Visibility = Visibility.Visible;

                        GamepadUISelectDesc.Text = Properties.Resources.MainWindow_Select;
                        GamepadUIBackDesc.Text = Properties.Resources.MainWindow_Back;

                        // To get the first RadioButton in the list, if any
                        RadioButton firstRadioButton = WPFUtils.FindChildren(control).FirstOrDefault(c => c is RadioButton) as RadioButton;
                        if (firstRadioButton is not null)
                        {
                            GamepadUIToggle.Visibility = Visibility.Visible;
                            GamepadUIToggleDesc.Text = Properties.Resources.MainWindow_Toggle;
                        }
                    }
                    break;

                case "Slider":
                    {
                        GamepadUISelect.Visibility = Visibility.Collapsed;
                        GamepadUIBack.Visibility = Visibility.Visible;
                        GamepadUIToggle.Visibility = Visibility.Collapsed;
                    }
                    break;

                case "NavigationViewItem":
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUIBack.Visibility = Visibility.Collapsed;
                        GamepadUIToggle.Visibility = Visibility.Collapsed;

                        GamepadUISelectDesc.Text = Properties.Resources.MainWindow_Navigate;
                    }
                    break;
            }
        });
    }

    private void AddNotifyIconItem(string name, object tag = null)
    {
        tag ??= string.Concat(name.Where(c => !char.IsWhiteSpace(c)));

        var menuItemMainWindow = new ToolStripMenuItem(name)
        {
            Tag = tag
        };
        menuItemMainWindow.Click += MenuItem_Click;
        notifyIcon.ContextMenuStrip.Items.Add(menuItemMainWindow);
    }

    private void AddNotifyIconSeparator()
    {
        var separator = new ToolStripSeparator();
        notifyIcon.ContextMenuStrip.Items.Add(separator);
    }

    public void SwapWindowState()
    {
        // UI thread
        UIHelper.TryInvoke(() =>
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

    public void UpdateTaskbarState(TaskbarItemProgressState state)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            this.TaskbarItem.ProgressState = state;
        });
    }

    public void UpdateTaskbarProgress(double value)
    {
        if (value < 0 || value > 1) return;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            this.TaskbarItem.ProgressValue = value;
        });
    }

    private void loadPages()
    {
        // initialize pages
        controllerPage = new ControllerPage("controller");
        devicePage = new DevicePage("device");
        profilesPage = new ProfilesPage("profiles");
        settingsPage = new SettingsPage("settings");
        overlayPage = new OverlayPage("overlay");
        hotkeysPage = new HotkeysPage("hotkeys");
        notificationsPage = new NotificationsPage("notifications");

        // manage events
        controllerPage.Loaded += ControllerPage_Loaded;
        notificationsPage.StatusChanged += NotificationsPage_LayoutUpdated;

        // store pages
        _pages.Add("ControllerPage", controllerPage);
        _pages.Add("DevicePage", devicePage);
        _pages.Add("ProfilesPage", profilesPage);
        _pages.Add("OverlayPage", overlayPage);
        _pages.Add("SettingsPage", settingsPage);
        _pages.Add("HotkeysPage", hotkeysPage);
        _pages.Add("NotificationsPage", notificationsPage);
    }

    private void LoadPages_MVVM()
    {
        layoutPage = new LayoutPage("layout", navView);
        performancePage = new PerformancePage();
        aboutPage = new AboutPage();

        layoutPage.Initialize();

        // storage pages
        _pages.Add("LayoutPage", layoutPage);
        _pages.Add("PerformancePage", performancePage);
        _pages.Add("AboutPage", aboutPage);
    }

    private void loadWindows()
    {
        // initialize overlay
        overlayModel = new OverlayModel();
        overlayTrackpad = new OverlayTrackpad();
        overlayquickTools = new OverlayQuickTools();
    }

    private void GenericDeviceUpdated(PnPDevice device, Guid IntefaceGuid)
    {
        // todo: improve me
        CurrentDevice.PullSensors();
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
            case "Exit":
                appClosing = true;
                Close();
                break;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // hide splashscreen
        SplashScreen?.Close();

        // load gamepad navigation manager
        gamepadFocusManager = new(this, ContentFrame);

        HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
        source.AddHook(WndProc); // Hook into the window's message loop

        // restore window state
        WindowState = ManagerFactory.settingsManager.GetBoolean("StartMinimized") ? WindowState.Minimized : (WindowState)ManagerFactory.settingsManager.GetInt("MainWindowState");
        prevWindowState = (WindowState)ManagerFactory.settingsManager.GetInt("MainWindowPrevState");
    }

    private void ControllerPage_Loaded(object sender, RoutedEventArgs e)
    {
        // home page is ready, display main window
        this.Visibility = Visibility.Visible;

        string TelemetryApproved = ManagerFactory.settingsManager.GetString("TelemetryApproved");
        if (string.IsNullOrEmpty(TelemetryApproved))
        {
            string Title = Properties.Resources.MainWindow_TelemetryTitle;
            string Content = Properties.Resources.MainWindow_TelemetryText;

            MessageBoxResult result = MessageBox.Show(Content, Title, MessageBoxButton.YesNo);
            ManagerFactory.settingsManager.SetProperty("TelemetryApproved", result == MessageBoxResult.Yes ? "True" : "False");
            ManagerFactory.settingsManager.SetProperty("TelemetryEnabled", result == MessageBoxResult.Yes);
        }
    }

    private void NotificationsPage_LayoutUpdated(int notifications)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            HasNotifications.Visibility = notifications != 0 ? Visibility.Visible : Visibility.Collapsed;
            HasNotifications.Value = notifications;
        });
    }

    private DateTime pendingTime = DateTime.Now;
    private DateTime resumeTime = DateTime.Now;
    private async void OnSystemStatusChanged(SystemManager.SystemStatus status, SystemManager.SystemStatus prevStatus)
    {
        if (status == prevStatus)
            return;

        switch (status)
        {
            case SystemManager.SystemStatus.SystemReady:
                {
                    if (prevStatus == SystemManager.SystemStatus.SystemPending)
                    {
                        // when device resumes from sleep
                        resumeTime = DateTime.Now;

                        // use device-specific delay
                        await Task.Delay(CurrentDevice.ResumeDelay); // Captures synchronization context

                        // resume manager(s)
                        InputsManager.Start();
                        TimerManager.Start();
                        SensorsManager.Resume(true);
                        ManagerFactory.gpuManager.Start();
                        PerformanceManager.Resume(true);

                        // resume platform(s)
                        PlatformManager.LibreHardwareMonitor.Start();

                        // wait a bit more if device went to sleep for at least 30 minutes (arbitrary)
                        TimeSpan sleepDuration = resumeTime - pendingTime;
                        if (sleepDuration.TotalMinutes > 30)
                            await Task.Delay(CurrentDevice.ResumeDelay); // Captures synchronization context

                        VirtualManager.Resume(true);
                        ControllerManager.Resume(true);
                    }

                    // open device, when ready
                    new Thread(() =>
                    {
                        // wait for all HIDs to be ready
                        while (!CurrentDevice.IsReady())
                            Thread.Sleep(1000);

                        // open current device (threaded to avoid device to hang)
                        CurrentDevice.Open();
                    }).Start();
                }
                break;

            case SystemManager.SystemStatus.SystemPending:
                {
                    if (prevStatus == SystemManager.SystemStatus.SystemReady)
                    {
                        SystemManager.SetThreadExecutionState(SystemManager.ES_CONTINUOUS | SystemManager.ES_SYSTEM_REQUIRED);
                        LogManager.LogInformation("System is about to suspend. Performing tasks.");

                        // when device goes to sleep
                        pendingTime = DateTime.Now;

                        // suspend manager(s)
                        ManagerFactory.gpuManager.Stop();
                        VirtualManager.Suspend(true);
                        ControllerManager.Suspend(true);
                        TimerManager.Stop();
                        SensorsManager.Stop();
                        InputsManager.Stop(false);

                        // suspend platform(s)
                        PlatformManager.LibreHardwareMonitor.Stop();

                        // close current device
                        CurrentDevice.Close();

                        // Allow system to sleep
                        SystemManager.SetThreadExecutionState(SystemManager.ES_CONTINUOUS);
                        LogManager.LogInformation("Tasks completed. System can now suspend.");
                    }
                }
                break;
        }
    }

    #region UI

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not null)
        {
            NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
            string navItemTag = (string)navItem.Tag;

            switch (navItemTag)
            {
                default:
                    prevNavItemTag = navItemTag;
                    break;
            }

            NavView_Navigate(prevNavItemTag);
        }
    }

    private void NavView_Navigate(string navItemTag)
    {
        KeyValuePair<string, Page> item = _pages.FirstOrDefault(p => p.Key.Equals(navItemTag));
        Page? _page = item.Value;

        // Get the page type before navigation so you can prevent duplicate
        // entries in the backstack.
        Type preNavPageType = ContentFrame.CurrentSourcePageType;

        // Only navigate if the selected page isn't currently loaded.
        if (!(_page is null) && !Equals(preNavPageType, _page))
            NavView_Navigate(_page);
    }

    public void NavigateToPage(string navItemTag)
    {
        // Update previous navigation item
        prevNavItemTag = navItemTag;

        // Find and select the matching menu item
        navView.SelectedItem = navView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == navItemTag);

        // Navigate to the specified page
        NavView_Navigate(navItemTag);
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

    private async void Window_Closed(object sender, EventArgs e)
    {
        LogManager.LogInformation("Closing {0}", Title);

        // wait until all managers have initialized
        while (ManagerFactory.Managers.Any(manager => manager.Status.HasFlag(ManagerStatus.Initializing)))
            await Task.Delay(250).ConfigureAwait(false);

        CurrentDevice.Close();

        notifyIcon.Visible = false;
        notifyIcon.Dispose();

        // manage events
        SystemManager.SystemStatusChanged -= OnSystemStatusChanged;
        ManagerFactory.deviceManager.UsbDeviceArrived -= GenericDeviceUpdated;
        ManagerFactory.deviceManager.UsbDeviceRemoved -= GenericDeviceUpdated;
        ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // stop windows
            overlayModel.Close(true);
            overlayTrackpad.Close();
            overlayquickTools.Close(true);

            // stop pages
            controllerPage.Page_Closed();
            profilesPage.Page_Closed();
            settingsPage.Page_Closed();
            overlayPage.Page_Closed();
            hotkeysPage.Page_Closed();
            layoutPage.Page_Closed();
            notificationsPage.Page_Closed();
        });

        // remove all automation event handlers
        // Automation.RemoveAllEventHandlers();

        foreach (IManager manager in ManagerFactory.Managers)
            manager.Stop();

        // stop managers
        VirtualManager.Stop();
        MotionManager.Stop();
        SensorsManager.Stop();
        ControllerManager.Stop();
        InputsManager.Stop(true);
        PlatformManager.Stop();
        OSDManager.Stop();
        SystemManager.Stop();
        DynamicLightingManager.Stop();
        ToastManager.Stop();
        TaskManager.Stop();
        PerformanceManager.Stop();
        UpdateManager.Stop();
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        // position and size settings
        switch (WindowState)
        {
            case WindowState.Normal:
                ManagerFactory.settingsManager.SetProperty("MainWindowLeft", Left);
                ManagerFactory.settingsManager.SetProperty("MainWindowTop", Top);
                ManagerFactory.settingsManager.SetProperty("MainWindowWidth", ActualWidth);
                ManagerFactory.settingsManager.SetProperty("MainWindowHeight", ActualHeight);
                break;
            case WindowState.Maximized:
                ManagerFactory.settingsManager.SetProperty("MainWindowLeft", 0);
                ManagerFactory.settingsManager.SetProperty("MainWindowTop", 0);
                // ManagerFactory.settingsManager.SetProperty("MainWindowWidth", SystemParameters.MaximizedPrimaryScreenWidth);
                // ManagerFactory.settingsManager.SetProperty("MainWindowHeight", SystemParameters.MaximizedPrimaryScreenHeight);
                break;
        }

        ManagerFactory.settingsManager.SetProperty("MainWindowState", (int)WindowState);
        ManagerFactory.settingsManager.SetProperty("MainWindowPrevState", (int)prevWindowState);

        ManagerFactory.settingsManager.SetProperty("MainWindowIsPaneOpen", navView.IsPaneOpen);

        if (ManagerFactory.settingsManager.GetBoolean("CloseMinimises") && !appClosing)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        switch (WindowState)
        {
            case WindowState.Minimized:
                {
                    if (notifyIcon is not null)
                        notifyIcon.Visible = true;
                    ShowInTaskbar = false;

                    if (!NotifyInTaskbar)
                    {
                        ToastManager.SendToast(Title, "is running in the background");
                        NotifyInTaskbar = true;
                    }
                }
                break;
            case WindowState.Normal:
            case WindowState.Maximized:
                {
                    if (notifyIcon is not null)
                        notifyIcon.Visible = false;
                    ShowInTaskbar = true;

                    Activate();
                    Topmost = true;  // important
                    Topmost = false; // important
                    Focus();

                    prevWindowState = WindowState;
                }
                break;
        }
    }

    private void navView_Loaded(object sender, RoutedEventArgs e)
    {
        // Add handler for ContentFrame navigation.
        ContentFrame.Navigated += On_Navigated;

        // navigate
        NavigateToPage("ControllerPage");
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

    private void navView_PaneOpened(NavigationView sender, object args)
    {
        // todo: localize me
        PaneText.Text = "Close navigation";
    }

    private void navView_PaneClosed(NavigationView sender, object args)
    {
        // todo: localize me
        PaneText.Text = "Open navigation";
    }

    private void On_Navigated(object sender, NavigationEventArgs e)
    {
        navView.IsBackEnabled = ContentFrame.CanGoBack;

        if (ContentFrame.SourcePageType is not null)
        {
            CurrentPageName = ContentFrame.CurrentSourcePageType.Name;

            var NavViewItem = navView.MenuItems
                .OfType<NavigationViewItem>()
                .Where(n => n.Tag.Equals(CurrentPageName)).FirstOrDefault();

            if (!(NavViewItem is null))
                navView.SelectedItem = NavViewItem;

            navView.Header = new TextBlock() { Text = ((Page)e.Content).Title };
        }
    }

    #endregion
}
