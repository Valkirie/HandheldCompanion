using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Notifications;
using HandheldCompanion.Shared;
using HandheldCompanion.UI;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Pages;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Threading;
using Control = System.Windows.Controls.Control;
using Page = System.Windows.Controls.Page;
using RadioButton = System.Windows.Controls.RadioButton;

namespace HandheldCompanion.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : GamepadWindow
{
    private const string LibraryKey = "LibraryPage";
    private const string ControllerKey = "ControllerPage";
    public override string HomePageKey => ManagerFactory.settingsManager.GetBoolean("LibraryPageEnabled") ? LibraryKey : ControllerKey;

    // devices vars
    private static IDevice CurrentDevice = null!;

    // page vars
    private static readonly Dictionary<string, Page> _pages = [];

    public static ControllerPage controllerPage = null!;
    public static DevicePage devicePage = null!;
    public static PerformancePage? performancePage = null;
    public static ProfilesPage profilesPage = null!;
    public static SettingsPage settingsPage = null!;
    public static AboutPage aboutPage = null!;
    public static OverlayPage overlayPage = null!;
    public static HotkeysPage hotkeysPage = null!;
    public static LayoutPage layoutPage = null!;
    public static NotificationsPage notificationsPage = null!;
    public static LibraryPage? libraryPage = null;
    public static LayoutItemPage layoutItemPage = null!;

    // overlay(s) vars
    private static MainWindow? CurrentWindow;

    public static string CurrentPageName = string.Empty;

    private bool appClosing;
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip trayContextMenu;
    private bool NotifyInTaskbar;
    public string prevNavItemTag = string.Empty;
    private bool hasDeferredInitialShow;
    private bool pendingStartupFullscreen;
    private bool startupWindowReady;
    private WindowState deferredStartupWindowState = WindowState.Minimized;

    // Track tray menu items for liked profiles
    private readonly Dictionary<Guid, ToolStripMenuItem> profileMenuItems = new();
    private ToolStripSeparator profileSeparator = null!;

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
    private FullScreenExperienceMonitor fullScreenExperienceMonitor = null!;

    public static SplashScreenHost SplashScreen = null!;

    private const int WM_QUERYENDSESSION = 0x0011;
    private const int WM_DISPLAYCHANGE = 0x007e;
    private const int WM_DEVICECHANGE = 0x0219;
    private const int TrayMenuMargin = 8;
    private const int TrayMenuCursorPadding = 20;

    private static bool StartMinimized => ManagerFactory.settingsManager.GetBoolean("StartMinimized");
    private static bool StartMaximized => ManagerFactory.settingsManager.GetBoolean("StartMaximized");
    private static bool ShowSplashScreen => ManagerFactory.settingsManager.GetBoolean("ShowSplashScreen");

    public MainWindow(SplashScreenHost splashScreen, IDevice? currentDevice = null)
    {
        SplashScreen = splashScreen;
        DataContext = new MainWindowViewModel();

        UpdateSplashStatus("Loading interface...");

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

        CurrentWindow = this;

        // define current directory
        Directory.SetCurrentDirectory(App.CurrentPath);

        // initialize notifyIcon
        trayContextMenu = new ContextMenuStrip
        {
            ShowCheckMargin = false,
            ShowImageMargin = true,
            DropShadowEnabled = true
        };

        notifyIcon = new NotifyIcon
        {
            Text = Title,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
            Visible = false
        };

        notifyIcon.DoubleClick += (sender, e) => { ToggleState(); };
        notifyIcon.MouseUp += NotifyIcon_MouseUp;

        // Build initial tray menu (will be updated when ProfileManager initializes)
        BuildTrayMenu();

        // HidHide registration can block up to 3 seconds on driver error - run off the UI thread
        Task.Run(() => HidHide.RegisterApplication(App.CurrentExe));

        // initialize device singleton synchronously: page constructors call IDevice.GetCurrent()
        // and rely on Capabilities/OEMChords that are set in the device's own constructor
        UpdateSplashStatus("Loading device information...");
        CurrentDevice = currentDevice ?? IDevice.GetCurrent();

        // FSE monitor
        fullScreenExperienceMonitor = new FullScreenExperienceMonitor();
        fullScreenExperienceMonitor.FseStateChanged += FullScreenExperienceMonitor_FseStateChanged;
        fullScreenExperienceMonitor.Start();

        // initialize UI sounds board
        UISounds uiSounds = new UISounds();

        // load all pages BEFORE starting managers (architectural requirement)
        UpdateSplashStatus("Loading pages...");
        App.overlayquickTools.loadPages();
        loadPages();

        // Subscribe to setting changes for lazy page creation
        ManagerFactory.settingsManager.SettingValueChanged += MainWindow_SettingValueChanged;

        // manage events
        SystemManager.SystemStatusChanged += OnSystemStatusChanged;
        SystemManager.SessionLockChanged += OnSessionLockChanged;
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

        // load gamepad navigation manager
        gamepadFocusManager = new(this, ContentFrame);
    }

    public ContentDialog LaunchProfileContentDialog => FindName("LaunchProfileDialog") as ContentDialog
        ?? throw new InvalidOperationException("LaunchProfileDialog was not found.");

    private static void UpdateSplashStatus(string status)
    {
        SplashScreen?.SetStatus(status);
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
                        RadioButton? firstRadioButton = WPFUtils.FindChildren(control).FirstOrDefault(c => c is RadioButton) as RadioButton;
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

    private void AddNotifyIconItem(string name, object? tag = null)
    {
        tag ??= string.Concat(name.Where(c => !char.IsWhiteSpace(c)));

        var menuItemMainWindow = new ToolStripMenuItem(name)
        {
            Tag = tag
        };
        menuItemMainWindow.Click += MenuItem_Click;
        trayContextMenu.Items.Add(menuItemMainWindow);
    }

    private void AddNotifyIconSeparator()
    {
        trayContextMenu.Items.Add(new ToolStripSeparator());
    }

    /// <summary>
    /// Initializes the tray icon context menu with standard items.
    /// Called once during initialization.
    /// </summary>
    private void BuildTrayMenu()
    {
        UIHelper.TryInvoke(() =>
        {
            trayContextMenu.Items.Clear();

            // Add separator placeholder (will be shown/hidden based on liked profiles)
            profileSeparator = new ToolStripSeparator();
            profileSeparator.Visible = false;
            trayContextMenu.Items.Add(profileSeparator);

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
            System.Drawing.Icon? profileIcon = null;
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

            trayContextMenu.Items.Insert(insertIndex, menuItem);
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
                trayContextMenu.Items.Remove(menuItem);
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

    public static MainWindow? GetCurrent()
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
        // always-created pages
        controllerPage = new ControllerPage("controller");
        devicePage = new DevicePage("device");
        profilesPage = new ProfilesPage("profiles");
        settingsPage = new SettingsPage("settings");
        overlayPage = new OverlayPage("overlay");
        hotkeysPage = new HotkeysPage("hotkeys");
        notificationsPage = new NotificationsPage("notifications");
        layoutPage = new LayoutPage("layout", navView);
        aboutPage = new AboutPage();
        layoutItemPage = new LayoutItemPage("layoutitem", navView);

        layoutPage.Initialize();
        layoutItemPage.Initialize();

        // conditionally-created pages
        if (ManagerFactory.settingsManager.GetBoolean("LibraryPageEnabled"))
        {
            libraryPage = new LibraryPage("library");
            _pages.Add("LibraryPage", libraryPage);
        }

        if (ManagerFactory.settingsManager.GetBoolean("PerformanceManagerEnabled"))
        {
            performancePage = new PerformancePage();
            _pages.Add("PerformancePage", performancePage);
        }

        // store pages
        _pages.Add("ControllerPage", controllerPage);
        _pages.Add("DevicePage", devicePage);
        _pages.Add("ProfilesPage", profilesPage);
        _pages.Add("OverlayPage", overlayPage);
        _pages.Add("SettingsPage", settingsPage);
        _pages.Add("HotkeysPage", hotkeysPage);
        _pages.Add("NotificationsPage", notificationsPage);
        _pages.Add("LayoutPage", layoutPage);
        _pages.Add("AboutPage", aboutPage);
        _pages.Add("LayoutItemPage", layoutItemPage);
    }

    private void GenericDeviceUpdated(PnPDevice device, Guid IntefaceGuid)
    {
    }

    private void MainWindow_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
    {
        switch (name)
        {
            case "LibraryPageEnabled":
                if (Convert.ToBoolean(value))
                    UIHelper.TryInvoke(EnsureLibraryPage);
                break;
            case "PerformanceManagerEnabled":
                if (Convert.ToBoolean(value))
                    UIHelper.TryInvoke(EnsurePerformancePage);
                break;
        }
    }

    private void EnsureLibraryPage()
    {
        if (libraryPage is not null)
            return;

        libraryPage = new LibraryPage("library");
        _pages["LibraryPage"] = libraryPage;
    }

    private void EnsurePerformancePage()
    {
        if (performancePage is not null)
            return;

        performancePage = new PerformancePage();
        _pages["PerformancePage"] = performancePage;
    }

    private void MenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem toolStripMenuItem)
        {
            string tag = toolStripMenuItem.Tag?.ToString() ?? string.Empty;

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
                    App.overlayquickTools.ToggleVisibility();
                    break;
                case "Exit":
                    RequestClose();
                    break;
            }
        }
    }

    public void RequestClose()
    {
        UIHelper.TryInvoke(() =>
        {
            appClosing = true;
            Close();
        }, DispatcherPriority.Normal);
    }

    private void NotifyIcon_MouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
            return;

        ShowTrayMenu();
    }

    private void ShowTrayMenu()
    {
        UIHelper.TryInvoke(() =>
        {
            if (!trayContextMenu.Items.OfType<ToolStripItem>().Any(item => item.Available))
                return;

            if (trayContextMenu.Visible)
                trayContextMenu.Close();

            var location = GetTrayMenuLocation(trayContextMenu);
            trayContextMenu.Show(location.X, location.Y);
        });
    }

    private static System.Drawing.Point GetTrayMenuLocation(ContextMenuStrip contextMenuStrip)
    {
        System.Drawing.Point cursorPosition = System.Windows.Forms.Cursor.Position;
        Screen screen = Screen.FromPoint(cursorPosition);
        System.Drawing.Rectangle workingArea = screen.WorkingArea;
        System.Drawing.Size preferredSize = contextMenuStrip.GetPreferredSize(System.Drawing.Size.Empty);

        int maxX = Math.Max(workingArea.Left + TrayMenuMargin, workingArea.Right - preferredSize.Width - TrayMenuMargin);
        int maxY = Math.Max(workingArea.Top + TrayMenuMargin, workingArea.Bottom - preferredSize.Height - TrayMenuMargin);

        return GetTaskbarEdge(screen) switch
        {
            TaskbarEdge.Top => new System.Drawing.Point(
                Math.Clamp(cursorPosition.X - preferredSize.Width + TrayMenuCursorPadding, workingArea.Left + TrayMenuMargin, maxX),
                workingArea.Top + TrayMenuMargin),
            TaskbarEdge.Left => new System.Drawing.Point(
                workingArea.Left + TrayMenuMargin,
                Math.Clamp(cursorPosition.Y - TrayMenuCursorPadding, workingArea.Top + TrayMenuMargin, maxY)),
            TaskbarEdge.Right => new System.Drawing.Point(
                workingArea.Right - preferredSize.Width - TrayMenuMargin,
                Math.Clamp(cursorPosition.Y - TrayMenuCursorPadding, workingArea.Top + TrayMenuMargin, maxY)),
            _ => new System.Drawing.Point(
                Math.Clamp(cursorPosition.X - preferredSize.Width + TrayMenuCursorPadding, workingArea.Left + TrayMenuMargin, maxX),
                workingArea.Bottom - preferredSize.Height - TrayMenuMargin),
        };
    }

    private static TaskbarEdge GetTaskbarEdge(Screen screen)
    {
        System.Drawing.Rectangle bounds = screen.Bounds;
        System.Drawing.Rectangle workingArea = screen.WorkingArea;

        if (workingArea.Top > bounds.Top)
            return TaskbarEdge.Top;

        if (workingArea.Left > bounds.Left)
            return TaskbarEdge.Left;

        if (workingArea.Right < bounds.Right)
            return TaskbarEdge.Right;

        return TaskbarEdge.Bottom;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_pages.TryGetValue(HomePageKey, out var homePage))
        {
            // The ProgressRing covers the content area while the page renders,
            // so navigate directly — no need for the hidden-frame pre-render step.
            var loadTask = WaitForPageLoadedAsync(homePage);
            NavigateToPage(HomePageKey);
            await loadTask;

            // Also wait until the home page's ViewModel has finished initializing
            // (e.g. LibraryPageViewModel loading profiles) before dismissing the splash screen.
            if (homePage.DataContext is ViewModels.LibraryPageViewModel libraryVm)
                await WaitForViewModelInitializedAsync(libraryVm);

            HomePage_Loaded();
        }

        // load gamepad navigation manager
        gamepadFocusManager.Loaded();

        // hook focus changes from all input types (mouse, touch, keyboard, gamepad)
        AddHandler(FocusManager.GotFocusEvent, new RoutedEventHandler(GamepadWindow_PreviewGotFocus));

        HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
        source?.AddHook(WndProc); // Hook into the window's message loop
    }

    private void HomePage_Loaded()
    {
        // hide the startup overlay — home page is rendered and ready
        ((MainWindowViewModel)DataContext).IsInitializing = false;

        startupWindowReady = true;

        // hide splashscreen
        SplashScreen?.Close();

        // restore window state
        WindowState windowState = (WindowState)ManagerFactory.settingsManager.GetInt("MainWindowState");
        deferredStartupWindowState = StartMinimized ? WindowState.Minimized : windowState;

        // apply fullscreen at startup (unless starting minimized)
        pendingStartupFullscreen = !StartMinimized && StartMaximized;

        ApplyStartupWindowVisibility();
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

                        // wait a bit more if device went to sleep for at least 30 minutes (arbitrary)
                        TimeSpan sleepDuration = resumeTime - pendingTime;
                        if (sleepDuration.TotalMinutes >= 30)
                            await Task.Delay(3000); // Captures synchronization context

                        // resume manager(s)
                        TimerManager.Start();
                        PerformanceManager.Resume(true);

                        ManagerFactory.Resume();

                        // resume platform(s)
                        PlatformManager.LibreHardware.Start();

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
                        App.overlayModel.SetVisibility(Visibility.Collapsed);
                        App.overlayTrackpad.SetVisibility(Visibility.Collapsed);
                        App.overlayquickTools.SetVisibility(Visibility.Collapsed);

                        // suspend manager(s)
                        ManagerFactory.Suspend();

                        ControllerManager.Suspend(true);
                        TimerManager.Stop();
                        SensorsManager.Suspend(true);

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

    private void OnSessionLockChanged(bool isLocked)
    {
        if (isLocked)
            InputsManager.Stop(false);
        else
            InputsManager.Start();

        ApplyStartupWindowVisibility();
    }

    private void ApplyStartupWindowVisibility()
    {
        if (!startupWindowReady || hasDeferredInitialShow)
            return;

        if (!SystemManager.IsSessionInteractive())
        {
            notifyIcon.Visible = true;
            ShowInTaskbar = false;

            try { Hide(); } catch { }

            return;
        }

        hasDeferredInitialShow = true;

        if (deferredStartupWindowState == WindowState.Minimized)
            TryHide();
        else
        {
            SetState(deferredStartupWindowState);

            if (pendingStartupFullscreen)
                EnterFullscreen();
        }
    }

    #region UI

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not null)
        {
            NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
            string navItemTag = (string)navItem.Tag;

            NavView_Navigate(navItemTag, true);
        }
    }

    private void NavView_Navigate(string navItemTag, bool focusNavigationItem)
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

        if (focusNavigationItem)
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

    public override void NavigateToPage(string navItemTag)
    {
        if (prevNavItemTag == navItemTag)
            return;

        // Navigate to the specified page
        NavView_Navigate(navItemTag, false);
    }

    public static void NavView_Navigate(Page _page)
    {
        CurrentWindow?.ContentFrame.Navigate(_page);
        CurrentWindow?.scrollViewer.ScrollToTop();
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

            trayContextMenu.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        });

        // manage events
        SystemManager.SystemStatusChanged -= OnSystemStatusChanged;
        SystemManager.SessionLockChanged -= OnSessionLockChanged;
        ManagerFactory.notificationManager.Added -= NotificationManagerUpdated;
        ManagerFactory.notificationManager.Discarded -= NotificationManagerUpdated;
        ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
        ManagerFactory.settingsManager.SettingValueChanged -= MainWindow_SettingValueChanged;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // stop windows
            App.overlayModel.Close(true);
            App.overlayTrackpad.Close();
            App.overlayquickTools.Close(true);

            // stop pages
            controllerPage.Page_Closed();
            profilesPage.Page_Closed();
            settingsPage.Page_Closed();
            overlayPage.Page_Closed();
            hotkeysPage.Page_Closed();
            layoutPage.Page_Closed();
            notificationsPage.Page_Closed();
            libraryPage?.Page_Closed();
        });

        // remove all automation event handlers
        ProcessUtils.TaskWithTimeout(() => Automation.RemoveAllEventHandlers(), TimeSpan.FromSeconds(3));

        foreach (IManager manager in ManagerFactory.Managers)
            manager.Stop();

        // stop managers
        await VirtualManager.Stop().ConfigureAwait(false);
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
        }, DispatcherPriority.Normal);
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
                        Hide(true);
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
        Dialog.Reset(this);

        try { Hide(true); } catch { }

        notifyIcon.Visible = true;
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
                    var openDialog = iNKORE.UI.WPF.Modern.Controls.ContentDialog.GetOpenDialog(this);
                    if (openDialog is not null)
                    {
                        if (openDialog == ContentDialog && _dialogOpen)
                        {
                            // Managed dialog: defer window hide until ContentDialog_Closed fires.
                            _pendingHide = true;
                            try { ContentDialog.Hide(); } catch { _pendingHide = false; }
                            return;
                        }
                        else
                        {
                            // Fire-and-forget dialog: close it immediately and clear stuck state.
                            try { openDialog.Hide(); } catch { }
                            Dialog.Reset(this);
                        }
                    }

                    TryHide();

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

    private enum TaskbarEdge
    {
        Left,
        Top,
        Right,
        Bottom
    }

    private async void navView_Loaded(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigated += On_Navigated;
    }

    private static Task WaitForViewModelInitializedAsync(ViewModels.LibraryPageViewModel viewModel)
    {
        if (!viewModel.IsInitializing)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<object?>();
        System.ComponentModel.PropertyChangedEventHandler? handler = null;
        handler = (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.LibraryPageViewModel.IsInitializing) && !viewModel.IsInitializing)
            {
                viewModel.PropertyChanged -= handler;
                tcs.TrySetResult(null);
            }
        };
        viewModel.PropertyChanged += handler;

        // Double-check after subscribing to avoid missing the event
        if (!viewModel.IsInitializing)
        {
            viewModel.PropertyChanged -= handler;
            tcs.TrySetResult(null);
        }

        return tcs.Task;
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
        if (e.OriginalSource is not DependencyObject dependencyObject)
            return;

        Control? control = dependencyObject as Control ?? WPFUtils.FindParent<Control>(dependencyObject);
        if (control is null || !gamepadFocusManager.IsValidFocusableContentElement(control))
            return;

        gamepadFocusManager.TrackFocusedControl(control);
        GamepadFocusManagerOnFocused(control);
    }

    private void GamepadWindow_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // do something
    }

    public bool TryGoBack()
    {
        // Don't go back if the nav pane is overlayed.
        if (navView.IsPaneOpen &&
            (navView.DisplayMode == NavigationViewDisplayMode.Compact ||
             navView.DisplayMode == NavigationViewDisplayMode.Minimal))
            return false;

        if (ContentFrame.Content is LibraryPage currentLibraryPage && currentLibraryPage.TryGoBack())
            return true;

        if (ContentFrame.Content is LayoutPage currentLayoutPage && currentLayoutPage.TryGoBack())
            return true;

        if (!ContentFrame.CanGoBack)
            return false;

        ContentFrame.GoBack();
        return true;
    }

    private void GamepadUIMore_Click(object sender, RoutedEventArgs e)
    {
        UIHelper.TryInvoke(() => gamepadFocusManager.TryMore());
    }

    private void GamepadUILike_Click(object sender, RoutedEventArgs e)
    {
        UIHelper.TryInvoke(() => gamepadFocusManager.TryLike());
    }

    private void GamepadUISelect_Click(object sender, RoutedEventArgs e)
    {
        UIHelper.TryInvoke(() => gamepadFocusManager.TrySelect());
    }

    private void GamepadUIBack_Click(object sender, RoutedEventArgs e)
    {
        UIHelper.TryInvoke(() => gamepadFocusManager.TryGoBack());
    }

    private void GamepadUIToggle_Click(object sender, RoutedEventArgs e)
    {
        UIHelper.TryInvoke(() => gamepadFocusManager.TryToggle());
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