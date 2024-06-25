using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.QuickPages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Windows.System.Power;
using WpfScreenHelper;
using WpfScreenHelper.Enum;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Page = System.Windows.Controls.Page;
using PowerLineStatus = System.Windows.Forms.PowerLineStatus;
using Screen = WpfScreenHelper.Screen;
using SystemManager = HandheldCompanion.Managers.SystemManager;
using SystemPowerManager = Windows.System.Power.PowerManager;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Views.Windows;

/// <summary>
///     Interaction logic for QuickTools.xaml
/// </summary>
public partial class OverlayQuickTools : GamepadWindow
{
    private const int SC_MOVE = 0xF010;
    private readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    const UInt32 SWP_NOSIZE = 0x0001;
    const UInt32 SWP_NOMOVE = 0x0002;
    const UInt32 SWP_NOACTIVATE = 0x0010;
    const UInt32 SWP_NOZORDER = 0x0004;

    // Define the Win32 API constants and functions
    const int WM_PAINT = 0x000F;
    const int WM_ACTIVATEAPP = 0x001C;
    const int WM_ACTIVATE = 0x0006;
    const int WM_SETFOCUS = 0x0007;
    const int WM_KILLFOCUS = 0x0008;
    const int WM_NCACTIVATE = 0x0086;
    const int WM_SYSCOMMAND = 0x0112;
    const int WM_WINDOWPOSCHANGING = 0x0046;
    const int WM_SHOWWINDOW = 0x0018;
    const int WM_MOUSEACTIVATE = 0x0021;

    const int WS_EX_NOACTIVATE = 0x08000000;
    const int GWL_EXSTYLE = -20;

    public HwndSource hwndSource;

    // page vars
    private readonly Dictionary<string, Page> _pages = new();

    private bool AutoHide;
    private bool isClosing;
    private readonly DispatcherTimer clockUpdateTimer;

    public QuickHomePage homePage;
    public QuickDevicePage devicePage;
    public QuickPerformancePage performancePage;
    public QuickProfilesPage profilesPage;
    public QuickOverlayPage overlayPage;
    public QuickApplicationsPage applicationsPage;

    private static OverlayQuickTools CurrentWindow;
    private string preNavItemTag;

    public OverlayQuickTools()
    {
        InitializeComponent();
        CurrentWindow = this;

        // used by gamepad navigation
        Tag = "QuickTools";

        PreviewKeyDown += HandleEsc;

        clockUpdateTimer = new DispatcherTimer();
        clockUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        clockUpdateTimer.Tick += UpdateTime;

        WMPaintTimer.Elapsed += WMPaintTimer_Elapsed;

        // create manager(s)
        SystemManager.PowerStatusChanged += PowerManager_PowerStatusChanged;

        MultimediaManager.DisplaySettingsChanged += SystemManager_DisplaySettingsChanged;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // create pages
        homePage = new("quickhome");
        devicePage = new("quickdevice");
        profilesPage = new("quickprofiles");
        applicationsPage = new("quickapplications");

        _pages.Add("QuickHomePage", homePage);
        _pages.Add("QuickDevicePage", devicePage);
        _pages.Add("QuickProfilesPage", profilesPage);
        _pages.Add("QuickApplicationsPage", applicationsPage);
    }

    public void LoadPages_MVVM()
    {
        overlayPage = new QuickOverlayPage();
        performancePage = new QuickPerformancePage();

        _pages.Add("QuickOverlayPage", overlayPage);
        _pages.Add("QuickPerformancePage", performancePage);
    }

    public static OverlayQuickTools GetCurrent()
    {
        return CurrentWindow;
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "QuickToolsLocation":
                    UpdateLocation();
                    break;
                case "QuickToolsAutoHide":
                    AutoHide = Convert.ToBoolean(value);
                    break;
                case "QuickToolsScreen":
                    UpdateLocation();
                    break;
            }
        });
    }

    private void SystemManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        // ignore if we're not ready yet
        if (!MultimediaManager.IsInitialized)
            return;

        UpdateLocation();
    }

    private const double _MaxWidth = 960;
    private const double _MaxHeight = 960;
    private const WindowStyle _Style = WindowStyle.ToolWindow;

    private void UpdateLocation()
    {
        // pull quicktools settings
        int QuickToolsLocation = SettingsManager.GetInt("QuickToolsLocation");
        string FriendlyName = SettingsManager.GetString("QuickToolsScreen");

        // Attempt to find the screen with the specified friendly name
        DesktopScreen friendlyScreen = MultimediaManager.AllScreens.Values.FirstOrDefault(a => a.FriendlyName.Equals(FriendlyName)) ?? MultimediaManager.PrimaryDesktop;

        // Find the corresponding Screen object
        Screen targerScreen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName.Equals(friendlyScreen.screen.DeviceName));

        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Common settings across cases 0 and 1
            MaxWidth = (int)Math.Min(_MaxWidth, targerScreen.WpfBounds.Width);
            MaxHeight = _MaxHeight;
            Width = (int)Math.Max(MinWidth, SettingsManager.GetDouble("QuickToolsWidth"));
            WindowStyle = _Style;

            switch (QuickToolsLocation)
            {
                case 0: // Left
                    this.SetWindowPosition(WindowPositions.BottomLeft, targerScreen);
                    Left += Margin.Left;
                    break;

                case 1: // Right
                    this.SetWindowPosition(WindowPositions.BottomRight, targerScreen);
                    Left -= Margin.Right;
                    break;

                case 2: // Maximized
                    MaxWidth = double.PositiveInfinity;
                    MaxHeight = double.PositiveInfinity;
                    WindowStyle = WindowStyle.None;
                    this.SetWindowPosition(WindowPositions.Maximize, targerScreen);
                    return; // Early return for case 2
            }

            // Common operation for case 0 and 1 after switch
            Top -= Margin.Top;
            Height = MinHeight = Math.Min(MaxHeight, targerScreen.WpfBounds.Height - Margin.Top - Margin.Bottom);
        });
    }

    private void PowerManager_PowerStatusChanged(PowerStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            var BatteryLifePercent = (int)Math.Truncate(status.BatteryLifePercent * 100.0f);
            BatteryIndicatorPercentage.Text = $"{BatteryLifePercent}%";

            // get status key
            var KeyStatus = string.Empty;
            switch (status.PowerLineStatus)
            {
                case PowerLineStatus.Online:
                    KeyStatus = "Charging";
                    break;
                default:
                    {
                        var energy = SystemPowerManager.EnergySaverStatus;
                        switch (energy)
                        {
                            case EnergySaverStatus.On:
                                KeyStatus = "Saver";
                                break;
                        }
                    }
                    break;
            }

            // get battery key
            var KeyValue = (int)Math.Truncate(status.BatteryLifePercent * 10);

            // set key
            var Key = $"Battery{KeyStatus}{KeyValue}";

            if (SystemManager.PowerStatusIcon.TryGetValue(Key, out var glyph))
                BatteryIndicatorIcon.Glyph = glyph;

            if (status.BatteryLifeRemaining > 0)
            {
                var time = TimeSpan.FromSeconds(status.BatteryLifeRemaining);

                string remaining;
                if (status.BatteryLifeRemaining >= 3600)
                    remaining = $"{time.Hours}h {time.Minutes}min";
                else
                    remaining = $"{time.Minutes}min";

                BatteryIndicatorLifeRemaining.Text = $"({remaining} remaining)";
                BatteryIndicatorLifeRemaining.Visibility = Visibility.Visible;
            }
            else
            {
                BatteryIndicatorLifeRemaining.Text = string.Empty;
                BatteryIndicatorLifeRemaining.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // load gamepad navigation maanger
        gamepadFocusManager = new(this, ContentFrame);

        hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        hwndSource.AddHook(WndProc);

        if (hwndSource != null)
            WinAPI.SetWindowPos(hwndSource.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    private IntPtr prevWParam = new(0x0000000000000086);
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_SYSCOMMAND:
                {
                    var command = wParam.ToInt32() & 0xfff0;
                    if (command == SC_MOVE) handled = true;
                }
                break;

            case WM_SETFOCUS:
                {
                    if (hwndSource != null)
                        WinAPI.SetWindowPos(hwndSource.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                    handled = true;
                }
                break;

            case WM_NCACTIVATE:
                {
                    // prevent window from loosing its fancy style
                    if (wParam == 0 && (lParam == 0))
                    {
                        if (prevWParam != new IntPtr(0x0000000000000086))
                            if (AutoHide && Visibility == Visibility.Visible)
                                ToggleVisibility();
                        handled = true;
                    }

                    if (wParam == 1)
                    {
                        handled = true;
                    }

                    prevWParam = wParam;
                }
                break;

            case WM_ACTIVATEAPP:
                {
                    if (wParam == 0)
                    {
                        if (hwndSource != null)
                            WPFUtils.SendMessage(hwndSource.Handle, WM_NCACTIVATE, WM_NCACTIVATE, 0);
                    }
                }
                break;

            case WM_ACTIVATE:
                {
                    // WA_INACTIVE
                    if (wParam == 0)
                        handled = true;
                }
                break;

            case WM_MOUSEACTIVATE:
                {
                    handled = true;
                }
                break;

            case WM_PAINT:
                {
                    DateTime drawTime = DateTime.Now;

                    double drawDiff = Math.Abs((prevDraw - drawTime).TotalMilliseconds);
                    if (drawDiff < 200)
                    {
                        if (!WMPaintPending)
                        {
                            // disable GPU acceleration
                            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

                            // set flag
                            WMPaintPending = true;

                            LogManager.LogError("ProcessRenderMode set to {0}", RenderOptions.ProcessRenderMode);
                        }
                    }

                    // update previous drawing time
                    prevDraw = drawTime;

                    if (WMPaintPending)
                    {
                        WMPaintTimer.Stop();
                        WMPaintTimer.Start();
                    }
                }
                break;
        }

        return IntPtr.Zero;
    }

    DateTime prevDraw = DateTime.MinValue;

    private void WMPaintTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (WMPaintPending)
        {
            // enable GPU acceleration
            RenderOptions.ProcessRenderMode = RenderMode.Default;

            // reset flag
            WMPaintPending = false;

            LogManager.LogError("ProcessRenderMode set to {0}", RenderOptions.ProcessRenderMode);
        }
    }

    private Timer WMPaintTimer = new(100) { AutoReset = false };
    private bool WMPaintPending = false;

    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ToggleVisibility();
    }

    public void ToggleVisibility()
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (Visibility)
            {
                case Visibility.Collapsed:
                case Visibility.Hidden:
                    Show();
                    Focus();

                    if (hwndSource != null)
                        WPFUtils.SendMessage(hwndSource.Handle, WM_NCACTIVATE, WM_NCACTIVATE, 0);

                    InvokeGotGamepadWindowFocus();

                    clockUpdateTimer.Start();
                    break;
                case Visibility.Visible:
                    Hide();

                    InvokeLostGamepadWindowFocus();

                    clockUpdateTimer.Stop();
                    break;
            }
        });
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        switch(WindowStyle)
        {
            case WindowStyle.ToolWindow:
                SettingsManager.SetProperty("QuickToolsWidth", ActualWidth);
                break;
        }

        e.Cancel = !isClosing;

        if (!isClosing)
            ToggleVisibility();
        else
        {
            // close pages
            devicePage.Close();
        }
    }

    public void Close(bool v)
    {
        isClosing = v;
        Close();
    }

    #region navView

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
                case "shortcutKeyboard":
                case "shortcutDesktop":
                case "shortcutESC":
                case "shortcutExpand":
                    HotkeysManager.TriggerRaised(navItemTag, null, 0, false, true);
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

    public void NavView_Navigate(Page _page)
    {
        ContentFrame.Navigate(_page);
    }

    private void navView_Loaded(object sender, RoutedEventArgs e)
    {
        // Add handler for ContentFrame navigation.
        ContentFrame.Navigated += On_Navigated;

        // NavView doesn't load any page by default, so load home page.
        navView.SelectedItem = navView.MenuItems[1];

        // If navigation occurs on SelectionChanged, this isn't needed.
        // Because we use ItemInvoked to navigate, we need to call Navigate
        // here to load the home page.
        preNavItemTag = "QuickHomePage";
        NavView_Navigate(preNavItemTag);
    }

    private void navView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        TryGoBack();
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
        // navHeader.Text = ((Page)((ContentControl)sender).Content).Title;
    }

    private void UpdateTime(object? sender, EventArgs e)
    {
        var timeFormat = CultureInfo.InstalledUICulture.DateTimeFormat.ShortTimePattern;
        Time.Text = DateTime.Now.ToString(timeFormat);
    }

    #endregion
}