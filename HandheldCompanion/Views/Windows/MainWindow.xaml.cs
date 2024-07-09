using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
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
using System.Windows.Navigation;
using System.Windows.Threading;
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

    public static string InstallPath = string.Empty;
    public static string SettingsPath = string.Empty;
    public static string CurrentPageName = string.Empty;

    private bool appClosing;
    private readonly NotifyIcon notifyIcon;
    private bool NotifyInTaskbar;
    private string preNavItemTag;

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

        // get first start
        bool FirstStart = SettingsManager.GetBoolean("FirstStart");

        if (FirstStart)
        {
#if !DEBUG
            SplashScreen.Show();
#endif
        }

        SplashScreen.LoadingSequence.Text = "Preparing UI...";

        InitializeComponent();
        this.Tag = "MainWindow";

        fileVersionInfo = _fileVersionInfo;
        CurrentWindow = this;

        // used by system manager, controller manager
        uiSettings = new UISettings();

        // fix touch support
        TabletDeviceCollection tabletDevices = Tablet.TabletDevices;

        // define current directory
        InstallPath = AppDomain.CurrentDomain.BaseDirectory;
        SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HandheldCompanion");

        // initialiaze path
        if (!Directory.Exists(SettingsPath))
            Directory.CreateDirectory(SettingsPath);

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
        SplashScreen.LoadingSequence.Text = "Initializing device...";
        CurrentDevice = IDevice.GetCurrent();
        CurrentDevice.PullSensors();

        if (FirstStart)
        {
            string currentDeviceType = CurrentDevice.GetType().Name;
            switch (currentDeviceType)
            {
                case "SteamDeck":
                    {
                        // prevent Steam Deck controller from being hidden by default
                        if (FirstStart)
                            SettingsManager.SetProperty("HIDcloakonconnect", false);
                    }
                    break;
            }

            SettingsManager.SetProperty("FirstStart", false);
        }

        // initialize UI sounds board
        UISounds uiSounds = new UISounds();

        // load window(s)
        SplashScreen.LoadingSequence.Text = "Drawing windows...";
        Dispatcher.Invoke(new Action(() =>
        {
            loadWindows();
        }), DispatcherPriority.Background); // Lower priority

        // load page(s)
        SplashScreen.LoadingSequence.Text = "Drawing pages...";
        Dispatcher.Invoke(new Action(() =>
        {
            loadPages();
        }), DispatcherPriority.Background); // Lower priority

        // manage events
        SystemManager.SystemStatusChanged += OnSystemStatusChanged;
        DeviceManager.UsbDeviceArrived += GenericDeviceUpdated;
        DeviceManager.UsbDeviceRemoved += GenericDeviceUpdated;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

        ToastManager.Start();
        ToastManager.IsEnabled = SettingsManager.GetBoolean("ToastEnable");

        // start static managers in sequence
        SplashScreen.LoadingSequence.Text = "Initializing managers...";
        GPUManager.Start();
        PowerProfileManager.Start();
        ProfileManager.Start();
        ControllerManager.Start();
        HotkeysManager.Start();
        DeviceManager.Start();
        OSDManager.Start();
        LayoutManager.Start();
        SystemManager.Start();
        DynamicLightingManager.Start();
        MultimediaManager.Start();
        VirtualManager.Start();
        InputsManager.Start();
        SensorsManager.Start();
        TimerManager.Start();

        // todo: improve overall threading logic
        new Thread(() => { PlatformManager.Start(); }).Start();
        new Thread(() => { ProcessManager.Start(); }).Start();
        new Thread(() => { TaskManager.Start(CurrentExe); }).Start();
        new Thread(() => { PerformanceManager.Start(); }).Start();
        new Thread(() => { UpdateManager.Start(); }).Start();

        // start setting last
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        SettingsManager.Start();

        // Load MVVM pages after the Models / data have been created.
        overlayquickTools.LoadPages_MVVM();
        LoadPages_MVVM();

        // update Position and Size
        Height = (int)Math.Max(MinHeight, SettingsManager.GetDouble("MainWindowHeight"));
        Width = (int)Math.Max(MinWidth, SettingsManager.GetDouble("MainWindowWidth"));
        Left = Math.Min(SystemParameters.PrimaryScreenWidth - MinWidth, SettingsManager.GetDouble("MainWindowLeft"));
        Top = Math.Min(SystemParameters.PrimaryScreenHeight - MinHeight, SettingsManager.GetDouble("MainWindowTop"));
        navView.IsPaneOpen = SettingsManager.GetBoolean("MainWindowIsPaneOpen");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_DISPLAYCHANGE:
            case WM_DEVICECHANGE:
                DeviceManager.RefreshDisplayAdapters();
                break;
            case WM_QUERYENDSESSION:
                break;
        }

        return IntPtr.Zero;
    }

    private void ControllerManager_ControllerSelected(IController Controller)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            GamepadUISelectIcon.Glyph = Controller.GetGlyph(ButtonFlags.B1);
            GamepadUISelectIcon.Foreground = Controller.GetGlyphColor(ButtonFlags.B1);

            GamepadUIBackIcon.Glyph = Controller.GetGlyph(ButtonFlags.B2);
            GamepadUIBackIcon.Foreground = Controller.GetGlyphColor(ButtonFlags.B2);

            GamepadUIToggleIcon.Glyph = Controller.GetGlyph(ButtonFlags.B4);
            GamepadUIToggleIcon.Foreground = Controller.GetGlyphColor(ButtonFlags.B4);
        });
    }

    private void GamepadFocusManagerOnFocused(Control control)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
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
            case "TelemetryApproved":

                // If the input is null or empty, return false or handle as needed
                if (string.IsNullOrEmpty(Convert.ToString(value)))
                    break;

                bool test = Convert.ToBoolean(value);
                break;
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

        devicePage = new DevicePage("device");
        profilesPage = new ProfilesPage("profiles");
        settingsPage = new SettingsPage("settings");

        overlayPage = new OverlayPage("overlay");
        hotkeysPage = new HotkeysPage("hotkeys");

        notificationsPage = new NotificationsPage("notifications");
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
        layoutPage.Initialize();

        performancePage = new PerformancePage();
        aboutPage = new AboutPage();

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

    private void GenericDeviceUpdated(PnPDevice device, DeviceEventArgs obj)
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
        if (SplashScreen is not null)
            SplashScreen.Close();

        // load gamepad navigation maanger
        gamepadFocusManager = new(this, ContentFrame);

        HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
        source.AddHook(WndProc); // Hook into the window's message loop

        // restore window state
        WindowState = SettingsManager.GetBoolean("StartMinimized") ? WindowState.Minimized : (WindowState)SettingsManager.GetInt("MainWindowState");
        prevWindowState = (WindowState)SettingsManager.GetInt("MainWindowPrevState");
    }

    private void ControllerPage_Loaded(object sender, RoutedEventArgs e)
    {
        // home page is ready, display main window
        this.Visibility = Visibility.Visible;

        string TelemetryApproved = SettingsManager.GetString("TelemetryApproved");
        if (string.IsNullOrEmpty(TelemetryApproved))
        {
            string Title = Properties.Resources.MainWindow_TelemetryTitle;
            string Content = Properties.Resources.MainWindow_TelemetryText;

            MessageBoxResult result = MessageBox.Show(Content, Title, MessageBoxButton.YesNo);
            SettingsManager.SetProperty("TelemetryApproved", result == MessageBoxResult.Yes ? "True" : "False");
            SettingsManager.SetProperty("TelemetryEnabled", result == MessageBoxResult.Yes ? true : false);
        }
    }

    private void NotificationsPage_LayoutUpdated(int status)
    {
        bool hasNotification = Convert.ToBoolean(status);

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            HasNotifications.Visibility = hasNotification ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    // no code from the cases inside this function will be called on program start
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
                        // use device-specific delay
                        await Task.Delay(CurrentDevice.ResumeDelay);

                        // resume manager(s)
                        InputsManager.Start();
                        TimerManager.Start();
                        VirtualManager.Resume(true);
                        SensorsManager.Resume(true);
                        GPUManager.Start();

                        // resume platform(s)
                        PlatformManager.LibreHardwareMonitor.Start();
                    }

                    // open device, when ready
                    new Thread(() =>
                    {
                        // wait for all HIDs to be ready
                        while (!CurrentDevice.IsReady())
                            Thread.Sleep(100);

                        // open current device (threaded to avoid device to hang)
                        CurrentDevice.Open();
                    }).Start();
                }
                break;

            case SystemManager.SystemStatus.SystemPending:
                {
                    // when device goes to sleep
                    // suspend manager(s)
                    VirtualManager.Suspend(true);
                    TimerManager.Stop();
                    SensorsManager.Stop();
                    InputsManager.Stop();
                    GPUManager.Stop();

                    // suspend platform(s)
                    PlatformManager.LibreHardwareMonitor.Stop();

                    // close current device
                    CurrentDevice.Close();

                    // Allow system to sleep
                    SystemManager.SetThreadExecutionState(SystemManager.ES_CONTINUOUS);
                    LogManager.LogDebug("Tasks completed. System can now suspend if needed.");
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

        notifyIcon.Visible = false;
        notifyIcon.Dispose();

        overlayModel.Close();
        overlayTrackpad.Close();
        overlayquickTools.Close(true);

        VirtualManager.Stop();
        MultimediaManager.Stop();
        GPUManager.Stop();
        MotionManager.Stop();
        SensorsManager.Stop();
        ControllerManager.Stop();
        InputsManager.Stop();
        DeviceManager.Stop();
        PlatformManager.Stop();
        OSDManager.Stop();
        PowerProfileManager.Stop();
        ProfileManager.Stop();
        LayoutManager.Stop();
        SystemManager.Stop();
        ProcessManager.Stop();
        ToastManager.Stop();
        TaskManager.Stop();
        PerformanceManager.Stop();
        UpdateManager.Stop();

        // closing page(s)
        controllerPage.Page_Closed();
        profilesPage.Page_Closed();
        settingsPage.Page_Closed();
        overlayPage.Page_Closed();
        hotkeysPage.Page_Closed();
        layoutPage.Page_Closed();
        notificationsPage.Page_Closed();

        // force kill application
        Environment.Exit(0);
    }

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
