using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.Shared;
using HandheldCompanion.UI;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shell;
using Windows.UI.ViewManagement;
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
    public static LibraryPage libraryPage;
    public static LayoutItemPage layoutItemPage;

    // overlay(s) vars
    public static OverlayModel overlayModel = new();
    public static OverlayTrackpad overlayTrackpad = new();
    public static OverlayQuickTools overlayquickTools = new();

    public static string CurrentExe = Environment.ProcessPath ?? string.Empty;
    public static string CurrentPath => AppDomain.CurrentDomain.BaseDirectory;

    private static MainWindow? CurrentWindow;
    private static FileVersionInfo? fileVersionInfo;

    public static string CurrentPageName = string.Empty;

    private bool appClosing;
    private readonly NotifyIcon notifyIcon;
    private bool NotifyInTaskbar;
    public string prevNavItemTag = string.Empty;

    // Track tray menu items for liked profiles
    private readonly Dictionary<Guid, ToolStripMenuItem> profileMenuItems = new();
    private ToolStripSeparator profileSeparator;

    private WindowState prevWindowState
    {
        get
        {
            return (WindowState)ManagerFactory.settingsManager.GetInt("MainWindowPrevState");
        }
        set
        {
            ManagerFactory.settingsManager.SetProperty("MainWindowPrevState", (int)value);
        }
    }

    private bool isFullscreen;
    private WindowState preFullscreenWindowState = WindowState.Normal;
    private WindowStyle preFullscreenWindowStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode preFullscreenResizeMode = ResizeMode.CanResize;
    private Rect preFullscreenBounds;
    private FullScreenExperienceMonitor fullScreenExperienceMonitor;

    public static SplashScreen SplashScreen;

    public static UISettings uiSettings;

    private const int WM_QUERYENDSESSION = 0x0011;
    private const int WM_DISPLAYCHANGE = 0x007e;
    private const int WM_DEVICECHANGE = 0x0219;

    public static Version LastVersion => Version.Parse(ManagerFactory.settingsManager.GetString("LastVersion"));
    public static Version CurrentVersion => Version.Parse(fileVersionInfo?.FileVersion ?? "0.0.0.0");

    private static bool StartMinimized => ManagerFactory.settingsManager.GetBoolean("StartMinimized");
    private static bool StartMaximized => ManagerFactory.settingsManager.GetBoolean("StartMaximized");
    private static bool ShowSplashScreen => ManagerFactory.settingsManager.GetBoolean("ShowSplashScreen");

    public MainWindow(FileVersionInfo _fileVersionInfo, Assembly CurrentAssembly)
    {
        // initialize splash screen
        SplashScreen = new SplashScreen();
        DataContext = new MainWindowViewModel();

#if !DEBUG
        if (ShowSplashScreen)
            SplashScreen.Show();
#endif

        // update theme
        ElementTheme currentTheme = (ElementTheme)ManagerFactory.settingsManager.GetInt("MainWindowTheme");
        ThemeManager.SetRequestedTheme(this, currentTheme);

        InitializeComponent();
        this.Tag = "MainWindow";

        // update Position and Size
        Height = (int)Math.Max(MinHeight, ManagerFactory.settingsManager.GetDouble("MainWindowHeight"));
        Width = (int)Math.Max(MinWidth, ManagerFactory.settingsManager.GetDouble("MainWindowWidth"));
        Left = Math.Min(SystemParameters.PrimaryScreenWidth - MinWidth, ManagerFactory.settingsManager.GetDouble("MainWindowLeft"));
        Top = Math.Min(SystemParameters.PrimaryScreenHeight - MinHeight, ManagerFactory.settingsManager.GetDouble("MainWindowTop"));

        ContentDialog.Closed += ContentDialog_Closed;
        ContentDialog.Opened += ContentDialog_Opened;

        fileVersionInfo = _fileVersionInfo;
        CurrentWindow = this;

        // get last version
        bool FirstStart = LastVersion == Version.Parse("0.0.0.0");
        bool NewUpdate = LastVersion != CurrentVersion;

        // used by system manager, controller manager
        uiSettings = new UISettings();

        // define current directory
        Directory.SetCurrentDirectory(CurrentPath);

        // initialize notifyIcon
        notifyIcon = new NotifyIcon
        {
            Text = Title,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
            Visible = false,
            ContextMenuStrip = new ContextMenuStrip()
        };

        notifyIcon.DoubleClick += (sender, e) => { ToggleState(); };

        // Build initial tray menu (will be updated when ProfileManager initializes)
        BuildTrayMenu();

        // HidHide registration can block up to 3 seconds on driver error - run off the UI thread
        Task.Run(() => HidHide.RegisterApplication(CurrentExe));

        // collect details from MotherboardInfo (reads JSON cache - fast on subsequent starts)
        MotherboardInfo.Collect();

        // initialize device singleton synchronously: page constructors call IDevice.GetCurrent()
        // and rely on Capabilities/OEMChords that are set in the device's own constructor
        CurrentDevice = IDevice.GetCurrent();

        // FSE monitor
        fullScreenExperienceMonitor = new FullScreenExperienceMonitor();
        fullScreenExperienceMonitor.FseStateChanged += FullScreenExperienceMonitor_FseStateChanged;
        fullScreenExperienceMonitor.Start();

        // initialize UI sounds board
        UISounds uiSounds = new UISounds();

        // load page(s) BEFORE starting managers (architectural requirement)
        overlayquickTools.loadPages();
        loadPages();

        // start non-threaded managers that don't depend on MVVM pages
        InputsManager.Start();
        TimerManager.Start();
        MotionManager.Start();
        ManagerFactory.settingsManager.Start();

        // Load MVVM pages after the Models / data have been created.
        overlayquickTools.LoadPages_MVVM();
        LoadPages_MVVM();

        // Now that ALL pages are loaded, start background managers
        // PullSensors, device Initialize, and all manager starts move to background;
        // pages are guaranteed to exist before managers finish starting
        Task.Run(() => StartNonUIInit(CurrentExe, FirstStart, NewUpdate));

        // manage events
        SystemManager.SystemStatusChanged += OnSystemStatusChanged;
        ManagerFactory.deviceManager.UsbDeviceArrived += GenericDeviceUpdated;
        ManagerFactory.deviceManager.UsbDeviceRemoved += GenericDeviceUpdated;
        ManagerFactory.notificationManager.Added += NotificationManagerUpdated;
        ManagerFactory.notificationManager.Discarded += NotificationManagerUpdated;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

        // Subscribe to profile manager events to update tray menu
        ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
        ManagerFactory.profileManager.Updated += OnProfileUpdated;
        ManagerFactory.profileManager.Deleted += RemoveProfileFromTrayMenu;

        // prepare toast manager
        ToastManager.Start();
        ToastManager.SendToast(Title, "is starting");

        // update setting(s)
        ManagerFactory.settingsManager.SetProperty("LastVersion", fileVersionInfo.FileVersion);

        // load gamepad navigation manager
        gamepadFocusManager = new(this, ContentFrame);
    }

    /// <summary>
    /// Runs on a background thread: sensor pull, device hardware init, and all manager starts.
    /// The device singleton is already constructed before this is called, so Capabilities and
    /// OEMChords (set in device constructors) are available to page constructors on the UI thread.
    /// </summary>
    private static void StartNonUIInit(string exePath, bool firstStart, bool newUpdate)
    {
        CurrentDevice.PullSensors();
        CurrentDevice.Initialize(firstStart, newUpdate);

        // start non-static managers
        // todo: make them non-static
        foreach (IManager manager in ManagerFactory.Managers)
            Task.Run(() => manager.Start());

        // start static managers
        Task.Run(() => OSDManager.Start());
        Task.Run(() => SystemManager.Start());
        Task.Run(() => DynamicLightingManager.Start());
        Task.Run(() => VirtualManager.Start());
        Task.Run(() => SensorsManager.Start());
        Task.Run(() => ControllerManager.Start());
        Task.Run(() => TaskManager.Start(exePath));
        Task.Run(() => PerformanceManager.Start());
        Task.Run(() => UpdateManager.Start());
    }

    private void ProfileManager_Initialized()
    {
        List<Profile> likedProfiles = ManagerFactory.profileManager?.GetProfiles(true)
            .Where(p => p.IsLiked && !p.Default)
            .OrderBy(p => p.Name)
            .ToList() ?? new List<Profile>();

        foreach (Profile profile in likedProfiles)
            AddProfileToTrayMenu(profile);
    }

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_DISPLAYCHANGE:
            case WM_DEVICECHANGE:
                ManagerFactory.deviceManager.RefreshDisplayAdapters();
                break;
        }

        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    private void ControllerManager_ControllerSelected(IController Controller)
    {
        // UI thread (async to prevent blocking event callers)
        UIHelper.TryBeginInvoke(() =>
        {
            // update glyph(s)
            GamepadUISelectIcon.Glyph = Controller.GetGlyph(ButtonFlags.B1);
            GamepadUIBackIcon.Glyph = Controller.GetGlyph(ButtonFlags.B2);
            GamepadUIToggleIcon.Glyph = Controller.GetGlyph(ButtonFlags.B4);
            GamepadUIMoreIcon.Glyph = Controller.GetGlyph(ButtonFlags.B3);
            GamepadUILikeIcon.Glyph = Controller.GetGlyph(ButtonFlags.Back);

            GamepadUILB.Glyph = Controller.GetGlyph(ButtonFlags.L1);
            GamepadUIRB.Glyph = Controller.GetGlyph(ButtonFlags.R1);

            // update color(s)
            Color? color1 = Controller.GetGlyphColor(ButtonFlags.B1);
            if (color1.HasValue)
                GamepadUISelectIcon.Foreground = new SolidColorBrush(color1.Value);
            else
                GamepadUISelectIcon.SetResourceReference(ForegroundProperty, "SystemControlForegroundBaseHighBrush");

            Color? color2 = Controller.GetGlyphColor(ButtonFlags.B2);
            if (color2.HasValue)
                GamepadUIBackIcon.Foreground = new SolidColorBrush(color2.Value);
            else
                GamepadUIBackIcon.SetResourceReference(ForegroundProperty, "SystemControlForegroundBaseHighBrush");

            Color? color3 = Controller.GetGlyphColor(ButtonFlags.B3);
            if (color3.HasValue)
                GamepadUIMoreIcon.Foreground = new SolidColorBrush(color3.Value);
            else
                GamepadUIMoreIcon.SetResourceReference(ForegroundProperty, "SystemControlForegroundBaseHighBrush");

            Color? color4 = Controller.GetGlyphColor(ButtonFlags.B4);
            if (color4.HasValue)
                GamepadUIToggleIcon.Foreground = new SolidColorBrush(color4.Value);
            else
                GamepadUIToggleIcon.SetResourceReference(ForegroundProperty, "SystemControlForegroundBaseHighBrush");

            Color? colorBack = Controller.GetGlyphColor(ButtonFlags.Back);
            if (colorBack.HasValue)
                GamepadUILikeIcon.Foreground = new SolidColorBrush(colorBack.Value);
            else
                GamepadUILikeIcon.SetResourceReference(ForegroundProperty, "SystemControlForegroundBaseHighBrush");
        });
    }

    private void GamepadFocusManagerOnFocused(Control control)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            GamepadUISelectDesc.Text = Properties.Resources.MainWindow_Select;

            bool canGoBack = gamepadFocusManager.CanGoBack;
            GamepadUIBack.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
            GamepadUIBackDesc.Text = canGoBack ? Properties.Resources.MainWindow_Back : Properties.Resources.MainWindow_Close;

            // todo : localize me
            string controlType = control.GetType().Name;
            switch (controlType)
            {
                default:
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUIToggle.Visibility = Visibility.Collapsed;
                        GamepadUIMore.Visibility = Visibility.Collapsed;
                        GamepadUILike.Visibility = Visibility.Collapsed;
                    }
                    break;

                case "Button":
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUIToggle.Visibility = Visibility.Collapsed;
                        GamepadUIMore.Visibility = Visibility.Collapsed;
                        GamepadUILike.Visibility = Visibility.Collapsed;

                        // To get the first RadioButton in the list, if any
                        RadioButton firstRadioButton = WPFUtils.FindChildren(control).FirstOrDefault(c => c is RadioButton) as RadioButton;
                        if (firstRadioButton is not null)
                        {
                            GamepadUIToggle.Visibility = Visibility.Visible;
                            GamepadUIToggleDesc.Text = Properties.Resources.MainWindow_Toggle;
                        }

                        if (control.Tag is ProfileViewModel profileViewModel)
                        {
                            Profile profile = profileViewModel.Profile;
                            if (!profile.ErrorCode.HasFlag(ProfileErrorCode.MissingExecutable))
                            {
                                GamepadUIToggle.Visibility = Visibility.Visible;
                                GamepadUIToggleDesc.Text = profileViewModel.IsRunning
                                    ? Properties.Resources.ProfilesPage_StopProcess
                                    : Properties.Resources.ProfilesPage_Play;

                                GamepadUIMore.Visibility = Visibility.Visible;
                                GamepadUIMoreDesc.Text = Properties.Resources.MainWindow_Layout;

                                GamepadUILike.Visibility = Visibility.Visible;
                                GamepadUILikeDesc.Text = profile.IsLiked
                                    ? "Remove from favorites"
                                    : "Add to favorites";
                            }
                        }
                    }
                    break;

                case "Slider":
                    {
                        GamepadUISelect.Visibility = Visibility.Collapsed;
                        GamepadUIToggle.Visibility = Visibility.Collapsed;
                        GamepadUIMore.Visibility = Visibility.Collapsed;
                    }
                    break;

                case "NavigationViewItem":
                    {
                        GamepadUISelect.Visibility = Visibility.Visible;
                        GamepadUIToggle.Visibility = Visibility.Collapsed;
                        GamepadUIMore.Visibility = Visibility.Collapsed;

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

    /// <summary>
    /// Initializes the tray icon context menu with standard items.
    /// Called once during initialization.
    /// </summary>
    private void BuildTrayMenu()
    {
        UIHelper.TryInvoke(() =>
        {
            notifyIcon.ContextMenuStrip.Items.Clear();

            // Add separator placeholder (will be shown/hidden based on liked profiles)
            profileSeparator = new ToolStripSeparator { Visible = false };
            notifyIcon.ContextMenuStrip.Items.Add(profileSeparator);

            // Add standard menu items
            AddNotifyIconItem(Properties.Resources.MainWindow_MainWindow, "MainWindow");
            AddNotifyIconItem(Properties.Resources.MainWindow_QuickTools, "QuickTools");
            AddNotifyIconSeparator();
            AddNotifyIconItem(Properties.Resources.MainWindow_Exit, "Exit");
        });
    }

    /// <summary>
    /// Adds a liked profile to the tray menu.
    /// </summary>
    private void AddProfileToTrayMenu(Profile profile)
    {
        if (profile == null || !profile.IsLiked || profile.Default || profileMenuItems.ContainsKey(profile.Guid))
            return;

        UIHelper.TryInvoke(() =>
        {
            // Extract icon from executable
            System.Drawing.Icon profileIcon = null;
            if (!string.IsNullOrEmpty(profile.Path) && File.Exists(profile.Path))
            {
                try
                {
                    profileIcon = System.Drawing.Icon.ExtractAssociatedIcon(profile.Path);
                }
                catch { }
            }

            var menuItem = new ToolStripMenuItem(profile.Name)
            {
                Tag = $"LaunchProfile:{profile.Guid}",
                Image = profileIcon?.ToBitmap()
            };
            menuItem.Click += MenuItem_Click;

            // Insert at the correct alphabetical position
            int insertIndex = 0;
            foreach (var existingGuid in profileMenuItems.Keys.OrderBy(g => profileMenuItems[g].Text))
            {
                if (string.Compare(profile.Name, profileMenuItems[existingGuid].Text, StringComparison.OrdinalIgnoreCase) > 0)
                    insertIndex++;
                else
                    break;
            }

            notifyIcon.ContextMenuStrip.Items.Insert(insertIndex, menuItem);
            profileMenuItems[profile.Guid] = menuItem;

            // Show separator since we have at least one liked profile
            profileSeparator.Visible = true;
        });
    }

    /// <summary>
    /// Removes a profile from the tray menu.
    /// </summary>
    private void RemoveProfileFromTrayMenu(Profile profile)
    {
        if (profile == null || !profileMenuItems.ContainsKey(profile.Guid))
            return;

        UIHelper.TryInvoke(() =>
        {
            if (profileMenuItems.TryGetValue(profile.Guid, out var menuItem))
            {
                notifyIcon.ContextMenuStrip.Items.Remove(menuItem);
                profileMenuItems.Remove(profile.Guid);
                menuItem.Dispose();
            }

            // Hide separator if no liked profiles remain
            profileSeparator.Visible = profileMenuItems.Any();
        });
    }

    /// <summary>
    /// Handles profile updates - adds if newly liked, removes if unliked.
    /// </summary>
    private void OnProfileUpdated(Profile profile, UpdateSource source, bool isCurrent)
    {
        // Tray menu is rebuilt when ProfileManager fires Initialized; skip per-profile work during load.
        if (source == UpdateSource.Serializer)
            return;

        if (profile.IsLiked)
            AddProfileToTrayMenu(profile);
        else
            RemoveProfileFromTrayMenu(profile);
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
        libraryPage = new LibraryPage("library");

        // store pages
        _pages.Add("ControllerPage", controllerPage);
        _pages.Add("DevicePage", devicePage);
        _pages.Add("ProfilesPage", profilesPage);
        _pages.Add("OverlayPage", overlayPage);
        _pages.Add("SettingsPage", settingsPage);
        _pages.Add("HotkeysPage", hotkeysPage);
        _pages.Add("NotificationsPage", notificationsPage);
        _pages.Add("LibraryPage", libraryPage);
    }

    private void LoadPages_MVVM()
    {
        layoutPage = new LayoutPage("layout", navView);
        performancePage = new PerformancePage();
        aboutPage = new AboutPage();
        layoutItemPage = new LayoutItemPage("layoutitem", navView);

        layoutPage.Initialize();
        layoutItemPage.Initialize();

        // storage pages
        _pages.Add("LayoutPage", layoutPage);
        _pages.Add("PerformancePage", performancePage);
        _pages.Add("AboutPage", aboutPage);
        _pages.Add("LayoutItemPage", layoutItemPage);
    }

    private void GenericDeviceUpdated(PnPDevice device, Guid IntefaceGuid)
    {
        // todo: improve me
        CurrentDevice.PullSensors();
    }

    private void MenuItem_Click(object? sender, EventArgs e)
    {
        string tag = ((ToolStripMenuItem)sender)?.Tag?.ToString() ?? string.Empty;

        // Handle profile launch commands
        if (tag.StartsWith("LaunchProfile:"))
        {
            string guidStr = tag.Substring("LaunchProfile:".Length);
            if (Guid.TryParse(guidStr, out Guid profileGuid))
            {
                Profile profile = ManagerFactory.profileManager.GetProfileFromGuid(profileGuid);
                if (profile != null)
                {
                    try
                    {
                        profile.Launch();
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError("Failed to launch profile {0}: {1}", profile.Name, ex.Message);
                    }
                }
            }
            return;
        }

        // Handle standard menu items
        switch (tag)
        {
            case "MainWindow":
                ToggleState();
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
        gamepadFocusManager.Loaded();

        // hook focus changes from all input types (mouse, touch, keyboard, gamepad)
        AddHandler(FocusManager.GotFocusEvent, new RoutedEventHandler(GamepadWindow_PreviewGotFocus));

        HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
        source.AddHook(WndProc); // Hook into the window's message loop

        // restore window state
        WindowState windowState = (WindowState)ManagerFactory.settingsManager.GetInt("MainWindowState");
        SetState(StartMinimized ? WindowState.Minimized : windowState);

        // apply fullscreen at startup (unless starting minimized)
        if (!StartMinimized && StartMaximized)
            EnterFullscreen();
    }

    private bool Homepage_Loaded = false;
    private void HomePage_Loaded()
    {
        // set status
        Homepage_Loaded = true;

        // hide the startup overlay — home page is rendered and ready
        ((MainWindowViewModel)DataContext).IsInitializing = false;

        // home page is ready, display main window
        notifyIcon.Visible = true;

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

    private void NotificationManagerUpdated(Notification notification)
    {
        // UI thread (async)
        UIHelper.TryBeginInvoke(() =>
        {
            HasNotifications.Visibility = ManagerFactory.notificationManager.Any ? Visibility.Visible : Visibility.Collapsed;
            HasNotifications.Value = ManagerFactory.notificationManager.Count;
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

                        // resume UI ?
                        this.WMPaint_Trigger();
                        overlayquickTools.WMPaint_Trigger();

                        // wait a bit more if device went to sleep for at least 30 minutes (arbitrary)
                        TimeSpan sleepDuration = resumeTime - pendingTime;
                        if (sleepDuration.TotalMinutes >= 30)
                            await Task.Delay(3000); // Captures synchronization context

                        // resume manager(s)
                        InputsManager.Start();
                        TimerManager.Start();
                        PerformanceManager.Resume(true);

                        ManagerFactory.Resume();

                        // resume platform(s)
                        PlatformManager.LibreHardware.Start();

                        VirtualManager.Resume(true);
                        ControllerManager.Resume(true);
                        SensorsManager.Resume(true);
                    }

                    // open device, when ready
                    new Task(async () =>
                    {
                        // wait for the current device to be ready (for 10 seconds)
                        Task timeout = Task.Delay(TimeSpan.FromSeconds(10));
                        while (!timeout.IsCompleted && !CurrentDevice.IsReady())
                            await Task.Delay(250).ConfigureAwait(false);

                        if (!CurrentDevice.IsReady())
                            LogManager.LogCritical("Failed to initialize {0} from {1}", CurrentDevice.ProductName, CurrentDevice.ManufacturerName);

                        // open current device (threaded to avoid device to hang)
                        if (CurrentDevice.Open())
                            CurrentDevice.OpenEvents();
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

                        // hide subwindow(s)
                        overlayModel.SetVisibility(Visibility.Collapsed);
                        overlayTrackpad.SetVisibility(Visibility.Collapsed);
                        overlayquickTools.SetVisibility(Visibility.Collapsed);

                        // suspend manager(s)
                        ManagerFactory.Suspend();

                        VirtualManager.Suspend(true);
                        ControllerManager.Suspend(true);
                        TimerManager.Stop();
                        SensorsManager.Suspend(true);
                        InputsManager.Stop(false);

                        // suspend platform(s)
                        PlatformManager.LibreHardware.Stop();

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

            NavView_Navigate(navItemTag);
        }
    }

    private void NavView_Navigate(string navItemTag)
    {
        NavigationViewItem? selectedItem = navView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == navItemTag);

        // is it a footer item ?
        if (selectedItem is null)
            selectedItem = navView.FooterMenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == navItemTag);

        // Find and select the matching menu item
        navView.SelectedItem = selectedItem;

        // Give gamepad focus
        gamepadFocusManager.Focus(selectedItem);

        // Debounce: update visual selection immediately, defer actual page load
        _pendingNavTag = navItemTag;
        _navDebounceTimer.Stop();
        _navDebounceTimer.Start();
    }

    protected override void ApplyPendingNavigation(string navItemTag)
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
        if (prevNavItemTag == navItemTag)
            return;

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
        // wait until all managers have initialized
        if (ManagerFactory.Managers.Any(manager => manager.Status.HasFlag(ManagerStatus.Initializing)))
        {
            LogManager.LogWarning("Waiting for all managers to be fully initialized before halting them");

            while (ManagerFactory.Managers.Any(manager => manager.Status.HasFlag(ManagerStatus.Initializing)))
                await Task.Delay(250).ConfigureAwait(false);
        }

        CurrentDevice.Close();

        // Clean up tray menu items - must be done on UI thread
        UIHelper.TryInvoke(() =>
        {
            foreach (var menuItem in profileMenuItems.Values)
                menuItem.Dispose();
            profileMenuItems.Clear();

            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        });

        // manage events
        SystemManager.SystemStatusChanged -= OnSystemStatusChanged;
        ManagerFactory.deviceManager.UsbDeviceArrived -= GenericDeviceUpdated;
        ManagerFactory.deviceManager.UsbDeviceRemoved -= GenericDeviceUpdated;
        ManagerFactory.notificationManager.Added -= NotificationManagerUpdated;
        ManagerFactory.notificationManager.Discarded -= NotificationManagerUpdated;
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
            libraryPage.Page_Closed();
        });

        // remove all automation event handlers
        ProcessUtils.TaskWithTimeout(() => Automation.RemoveAllEventHandlers(), TimeSpan.FromSeconds(3));

        foreach (IManager manager in ManagerFactory.Managers)
            manager.Stop();

        // stop managers
        VirtualManager.Stop();
        MotionManager.Stop();
        SensorsManager.Stop();
        ControllerManager.Stop();
        InputsManager.Stop(true);
        TimerManager.Stop();
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
                break;
        }

        if (ManagerFactory.settingsManager.GetBoolean("CloseMinimises") && !appClosing)
        {
            e.Cancel = true;
            _isClosingToMinimize = true;
            SetState(WindowState.Minimized);
            return;
        }
    }

    private bool isFseActive;
    private WindowState preFseWindowState = WindowState.Normal;


    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        // ALT+ENTER toggles fullscreen (classic Windows behavior)
        if (!isFseActive && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && (e.Key == Key.Enter || e.SystemKey == Key.Enter))
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void ToggleFullscreen()
    {
        if (isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    private void EnterFullscreen()
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            if (isFullscreen || isFseActive)
                return;

            // capture current state
            preFullscreenWindowState = (WindowState == WindowState.Minimized) ? prevWindowState : WindowState;
            preFullscreenWindowStyle = WindowStyle;
            preFullscreenResizeMode = ResizeMode;
            preFullscreenBounds = RestoreBounds;

            // apply borderless fullscreen
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            SetState(WindowState.Maximized);
            Topmost = false;

            isFullscreen = true;
        });
    }

    private void ExitFullscreen()
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            if (!isFullscreen || isFseActive)
                return;

            // restore chrome
            WindowStyle = preFullscreenWindowStyle;
            ResizeMode = preFullscreenResizeMode;

            // restore window state/bounds
            WindowState = preFullscreenWindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;

            if (WindowState == WindowState.Normal)
            {
                Left = preFullscreenBounds.Left;
                Top = preFullscreenBounds.Top;
                Width = preFullscreenBounds.Width;
                Height = preFullscreenBounds.Height;
            }

            isFullscreen = false;
        });
    }

    private void FullScreenExperienceMonitor_FseStateChanged(object? sender, FullScreenExperienceMonitor.FseStateChangedEventArgs e)
    {
        UIHelper.TryInvoke(() =>
        {
            if (e.IsActive)
            {
                if (!isFseActive)
                {
                    // capture once, before we force maximize
                    preFseWindowState = (WindowState == WindowState.Minimized) ? prevWindowState : WindowState;
                    isFseActive = true;
                }

                ResizeMode = ResizeMode.NoResize;          // removes Min/Max buttons
                SetState(WindowState.Maximized);           // force max while FSE
            }
            else
            {
                ResizeMode = ResizeMode.CanResize;         // restore buttons
                SetState(preFseWindowState);               // restore what we captured
                isFseActive = false;
            }
        });
    }

    public void ToggleState()
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            switch (WindowState)
            {
                case WindowState.Normal:
                case WindowState.Maximized:
                    SetState(WindowState.Minimized);
                    break;
                case WindowState.Minimized:
                    if (prevWindowState != WindowState.Minimized)
                        SetState(prevWindowState);
                    else
                        SetState(WindowState.Normal);
                    break;
            }
        });
    }

    public void SetState(WindowState windowState)
    {
        UIHelper.TryInvoke(() =>
        {
            if (isFseActive)
            {
                switch (windowState)
                {
                    case WindowState.Minimized:
                        Hide();
                        break;
                }

                WindowState = windowState;

                switch (windowState)
                {
                    case WindowState.Normal:
                    case WindowState.Maximized:
                        Show();
                        break;
                }
            }
            else
            {
                switch (windowState)
                {
                    case WindowState.Normal:
                    case WindowState.Maximized:
                        Show();
                        break;
                }

                WindowState = windowState;

                switch (windowState)
                {
                    case WindowState.Minimized:
                        Hide();
                        break;
                }
            }
        });
    }

    private bool _pendingHide;
    private bool _dialogOpen;
    private bool _isClosingToMinimize;

    private void ContentDialog_Opened(object? sender, ContentDialogOpenedEventArgs e)
    {
        _dialogOpen = true;
    }

    private void ContentDialog_Closed(object? sender, ContentDialogClosedEventArgs e)
    {
        if (_pendingHide)
        {
            _pendingHide = false;
            TryHide();
        }

        _dialogOpen = false;
    }

    private void TryHide()
    {
        // use your existing safe hide
        try { Hide(); } catch { }

        notifyIcon.Visible = Homepage_Loaded;
        ShowInTaskbar = false;

        if (!NotifyInTaskbar)
        {
            if (ToastManager.SendToast(Title, "is running in the background"))
                NotifyInTaskbar = true;
        }
    }

    protected override void Window_StateChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded)
            return;

        switch (WindowState)
        {
            case WindowState.Minimized:
                {
                    // If a dialog is open/visible, close it and wait for Closed before hiding window.
                    if (ContentDialog is not null && _dialogOpen)
                    {
                        _pendingHide = true;

                        // Close dialog first; window will hide in ContentDialog_Closed.
                        try { ContentDialog.Hide(); } catch { _pendingHide = false; }
                        return;
                    }
                    else
                    {
                        TryHide();
                    }

                    // Don't save state when minimizing due to CloseMinimises setting
                    if (!_isClosingToMinimize && !isFseActive)
                    {
                        prevWindowState = WindowState;
                        ManagerFactory.settingsManager.SetProperty("MainWindowState", (int)WindowState);
                    }
                }
                break;

            case WindowState.Normal:
            case WindowState.Maximized:
                {
                    notifyIcon.Visible = false;
                    ShowInTaskbar = true;

                    try
                    {
                        Show();
                        Activate();
                        Topmost = true;  // important
                        Topmost = false; // important
                        Focus();
                    }
                    catch { }

                    if (!isFseActive)
                    {
                        prevWindowState = WindowState;
                        ManagerFactory.settingsManager.SetProperty("MainWindowState", (int)WindowState);
                    }

                    // Clear the flag when window is restored from CloseMinimises
                    _isClosingToMinimize = false;
                }
                break;
        }

        base.Window_StateChanged(sender, e);
    }

    private const string HomeKey = "LibraryPage";

    private async void navView_Loaded(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigated += On_Navigated;

        if (_pages.TryGetValue(HomeKey, out var homePage))
        {
            // The ProgressRing covers the content area while the page renders,
            // so navigate directly — no need for the hidden-frame pre-render step.
            var loadTask = WaitForPageLoadedAsync(homePage);
            NavigateToPage(HomeKey);
            await loadTask;
            HomePage_Loaded();
        }
    }

    private static Task WaitForPageLoadedAsync(Page page)
    {
        if (page.IsLoaded)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<object?>();
        RoutedEventHandler? onLoaded = null;
        onLoaded = (s, e) =>
        {
            page.Loaded -= onLoaded;
            tcs.TrySetResult(null);
        };
        page.Loaded += onLoaded;
        return tcs.Task;
    }

    private void GamepadWindow_PreviewGotFocus(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Control control)
            return;

        GamepadFocusManagerOnFocused(control);
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

    private void GamepadUIMore_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B3, true, false);
            await Task.Delay(40);
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B3, false, true);
        });
    }

    private void GamepadUILike_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.Back, true, false);
            await Task.Delay(40);
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.Back, false, true);
        });
    }

    private void GamepadUISelect_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B1, true, false);
            await Task.Delay(40);
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B1, false, true);
        });
    }

    private void GamepadUIBack_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B2, true, false);
            await Task.Delay(40);
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B2, false, true);
        });
    }

    private void GamepadUIToggle_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B4, true, false);
            await Task.Delay(40);
            ControllerManager.GetTarget()?.InjectButton(ButtonFlags.B4, false, true);
        });
    }

    private void On_Navigated(object sender, NavigationEventArgs e)
    {
        if (ContentFrame.SourcePageType is not null)
        {
            CurrentPageName = ContentFrame.CurrentSourcePageType.Name;

            // Update previous navigation item
            prevNavItemTag = CurrentPageName;

            var NavViewItem = navView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(n => n.Tag is not null && n.Tag.Equals(CurrentPageName));

            if (!(NavViewItem is null))
                navView.SelectedItem = NavViewItem;

            navView.Header = new TextBlock() { Text = ((Page)e.Content).Title };
        }
    }

    #endregion
}