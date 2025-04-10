using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.QuickPages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Windows.System.Power;
using WpfScreenHelper;
using WpfScreenHelper.Enum;
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

    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOACTIVATE = 0x0010;
    private const int SWP_NOZORDER = 0x0004;

    // Define the Win32 API constants and functions
    private const int WM_PAINT = 0x000F;
    private const int WM_ACTIVATEAPP = 0x001C;
    private const int WM_ACTIVATE = 0x0006;
    private const int WM_SETFOCUS = 0x0007;
    private const int WM_KILLFOCUS = 0x0008;
    private const int WM_NCACTIVATE = 0x0086;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_SHOWWINDOW = 0x0018;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 0x0003;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCAPTION = 0x02;
    private const int MA_NOACTIVATEANDEAT = 4;

    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_SIZEBOX = 0x00040000;

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    // page vars
    private readonly Dictionary<string, Page> _pages = [];

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
    public string prevNavItemTag;

    public OverlayQuickTools()
    {
        InitializeComponent();

        CurrentWindow = this;

        // used by gamepad navigation
        Tag = "QuickTools";

        Width = (int)Math.Max(MinWidth, ManagerFactory.settingsManager.GetDouble("QuickToolsWidth"));
        Height = (int)Math.Max(MinHeight, ManagerFactory.settingsManager.GetDouble("QuickToolsHeight"));

        PreviewKeyDown += HandleEsc;

        clockUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        clockUpdateTimer.Tick += UpdateTime;

        WMPaintTimer.Elapsed += WMPaintTimer_Elapsed;

        // manage events
        SystemManager.PowerStatusChanged += PowerManager_PowerStatusChanged;
        ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

        // raise events
        if (ControllerManager.HasTargetController)
            ControllerManager_ControllerSelected(ControllerManager.GetTarget());

        // load gamepad navigation manager
        gamepadFocusManager = new(this, ContentFrame);
    }

    public void loadPages()
    {
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        hwndSource.AddHook(WndProc);

        int exStyle = WinAPI.GetWindowLong(hwndSource.Handle, GWL_EXSTYLE);
        exStyle |= WS_EX_NOACTIVATE;
        WinAPI.SetWindowLong(hwndSource.Handle, GWL_EXSTYLE, exStyle);

        /*
        int Style = WinAPI.GetWindowLong(hwndSource.Handle, GWL_STYLE);
        exStyle &= ~WS_SIZEBOX;
        WinAPI.SetWindowLong(hwndSource.Handle, GWL_STYLE, Style);
        */

        WinAPI.SetWindowPos(hwndSource.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOACTIVATE);
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

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            switch (name)
            {
                case "QuickToolsLocation":
                    UpdateLocation();
                    break;
                case "QuickToolsAutoHide":
                    AutoHide = Convert.ToBoolean(value);
                    break;
                case "QuickToolsDevicePath":
                    UpdateLocation();
                    break;
            }
        });
    }

    private void ControllerManager_ControllerSelected(IController Controller)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            QTLB.Glyph = Controller.GetGlyph(ButtonFlags.L1);
            QTRB.Glyph = Controller.GetGlyph(ButtonFlags.R1);
        });
    }

    private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        UpdateLocation();
    }

    private const double _Margin = 12;
    private const double _MaxHeight = 960;
    private const double _MaxWidth = 960;
    private const WindowStyle _Style = WindowStyle.ToolWindow;
    private double _Top = 0;
    private double _Left = 0;
    private Screen targetScreen;

    private void UpdateLocation()
    {
        // pull quicktools settings
        int QuickToolsLocation = ManagerFactory.settingsManager.GetInt("QuickToolsLocation");
        string DevicePath = ManagerFactory.settingsManager.GetString("QuickToolsDevicePath");
        string DeviceName = ManagerFactory.settingsManager.GetString("QuickToolsDeviceName");

        // Use a thread-safe enumeration to find the screen with the specified friendly name
        DesktopScreen friendlyScreen = null;
        foreach (KeyValuePair<string, DesktopScreen> screen in ManagerFactory.multimediaManager.AllScreens)
        {
            if (screen.Value.DevicePath.Equals(DevicePath) || screen.Value.ToString().Equals(DeviceName))
            {
                friendlyScreen = screen.Value;
                break;
            }
        }

        // Default to PrimaryDesktop if no matching screen is found
        friendlyScreen ??= ManagerFactory.multimediaManager.PrimaryDesktop;

        if (friendlyScreen is null)
            return;

        // Find the corresponding Screen object
        targetScreen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName.Equals(friendlyScreen.screen.DeviceName));
        if (targetScreen is null)
            return;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // Common settings across cases 0 and 1
            MaxWidth = (int)Math.Min(_MaxWidth, targetScreen.WpfBounds.Width);
            Width = (int)Math.Max(MinWidth, ManagerFactory.settingsManager.GetDouble("QuickToolsWidth"));
            MaxHeight = Math.Min(targetScreen.WpfBounds.Height - _Margin * 2, _MaxHeight);
            Height = MinHeight = MaxHeight;
            WindowStyle = _Style;

            switch (QuickToolsLocation)
            {
                case 2: // Maximized
                    MaxWidth = double.PositiveInfinity;
                    MaxHeight = double.PositiveInfinity;
                    WindowStyle = WindowStyle.None;
                    break;
            }

            switch (QuickToolsLocation)
            {
                case 0: // Left
                    this.SetWindowPosition(WindowPositions.BottomLeft, targetScreen);
                    break;

                case 1: // Right
                    this.SetWindowPosition(WindowPositions.BottomRight, targetScreen);
                    break;

                case 2: // Maximized
                    this.SetWindowPosition(WindowPositions.Maximize, targetScreen);
                    break;
            }

            switch (QuickToolsLocation)
            {
                case 0: // Left
                    Top -= _Margin;
                    Left += _Margin;
                    break;

                case 1: // Right
                    Top -= _Margin;
                    Left -= _Margin;
                    break;
            }

            // used by SlideIn/SlideOut
            _Top = Top;
            _Left = Left;
        });
    }

    private void PowerManager_PowerStatusChanged(PowerStatus status)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
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
        // do something
    }

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WA_ACTIVE = 1;
    private const int WA_CLICKACTIVE = 2;
    private const int WA_INACTIVE = 0;
    private static readonly IntPtr HWND_TOP = new IntPtr(0);
    private const uint SWP_FRAMECHANGED = 0x0020;

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

            case WM_ACTIVATE:
                handled = true;
                WPFUtils.SendMessage(hwndSource.Handle, WM_NCACTIVATE, WM_NCACTIVATE, 0);
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
        UIHelper.TryInvoke(() =>
        {
            switch (Visibility)
            {
                case Visibility.Collapsed:
                case Visibility.Hidden:
                    try { Show(); } catch { /* ItemsRepeater might have a NaN DesiredSize */ }
                    break;
                case Visibility.Visible:
                    try { Hide(); } catch { /* ItemsRepeater might have a NaN DesiredSize */ }
                    break;
            }
        });
    }

    private void GamepadWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        switch (Visibility)
        {
            case Visibility.Collapsed:
            case Visibility.Hidden:
                InvokeLostGamepadWindowFocus();
                clockUpdateTimer.Stop();
                break;
            case Visibility.Visible:
                WPFUtils.SendMessage(hwndSource.Handle, WM_NCACTIVATE, WM_NCACTIVATE, 0);
                InvokeGotGamepadWindowFocus();
                clockUpdateTimer.Start();
                break;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // position and size settings
        ManagerFactory.settingsManager.SetProperty("QuickToolsWidth", ActualWidth);

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

        homePage.Close();
        devicePage.Close();
        profilesPage.Close();
        applicationsPage.Close();
    }

    #region navView

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not null)
        {
            NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
            string navItemTag = (string)navItem.Tag;

            // update prev
            prevNavItemTag = navItemTag;

            // navigate
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

    public void NavView_Navigate(Page _page)
    {
        ContentFrame.Navigate(_page);
    }

    private void navView_Loaded(object sender, RoutedEventArgs e)
    {
        // Add handler for ContentFrame navigation.
        ContentFrame.Navigated += On_Navigated;

        // navigate
        NavigateToPage("QuickHomePage");
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

    internal nint GetHandle()
    {
        return hwndSource.Handle;
    }

    #endregion
}