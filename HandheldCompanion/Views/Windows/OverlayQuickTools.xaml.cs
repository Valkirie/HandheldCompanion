using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using Windows.System.Power;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Views.QuickPages;
using Inkore.UI.WPF.Modern.Controls;
using WpfScreenHelper;
using WpfScreenHelper.Enum;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Page = System.Windows.Controls.Page;
using PowerLineStatus = System.Windows.Forms.PowerLineStatus;
using PowerManager = ControllerCommon.Managers.PowerManager;
using Screen = WpfScreenHelper.Screen;
using SystemInformation = System.Windows.Forms.SystemInformation;
using SystemPowerManager = Windows.System.Power.PowerManager;
using Control = System.Windows.Controls.Control;
using HandheldCompanion.Views.Classes;

namespace HandheldCompanion.Views.Windows;

/// <summary>
///     Interaction logic for QuickTools.xaml
/// </summary>
public partial class OverlayQuickTools : GamepadWindow
{
    private const int WM_SYSCOMMAND = 0x0112;

    private const int SC_MOVE = 0xF010;

    // page vars
    private readonly Dictionary<string, Page> _pages = new();

    private bool AutoHide;

    public HwndSource hwndSource;

    private bool isClosing;

    public QuickPerformancePage performancePage;
    private string preNavItemTag;
    public QuickProfilesPage profilesPage;
    public QuickSettingsPage settingsPage;
    public QuickSuspenderPage suspenderPage;

    public OverlayQuickTools()
    {
        InitializeComponent();

        // used by gamepad navigation
        Tag = "QuickTools";

        PreviewKeyDown += HandleEsc;
        Deactivated += OverlayQuickTools_Deactivated;

        // create manager(s)
        PowerManager.PowerStatusChanged += PowerManager_PowerStatusChanged;
        PowerManager_PowerStatusChanged(SystemInformation.PowerStatus);

        SystemManager.DisplaySettingsChanged += SystemManager_DisplaySettingsChanged;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // create pages
        performancePage = new QuickPerformancePage("quickperformance");
        settingsPage = new QuickSettingsPage("quicksettings");
        profilesPage = new QuickProfilesPage("quickprofiles");
        suspenderPage = new QuickSuspenderPage("quicksuspender");

        _pages.Add("QuickPerformancePage", performancePage);
        _pages.Add("QuickSettingsPage", settingsPage);
        _pages.Add("QuickProfilesPage", profilesPage);
        _pages.Add("QuickSuspenderPage", suspenderPage);

        // update Position and Size
        Height = (int)Math.Max(MinHeight, SettingsManager.GetDouble("QuickToolsHeight"));
        navView.IsPaneOpen = SettingsManager.GetBoolean("QuickToolsIsPaneOpen");
    }

    private void OverlayQuickTools_Deactivated(object? sender, EventArgs e)
    {
        if (AutoHide)
            Hide();
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
                case "QuickToolsLocation":
                {
                    var QuickToolsLocation = Convert.ToInt32(value);
                    UpdateLocation(QuickToolsLocation);
                }
                    break;
                case "QuickToolsAutoHide":
                {
                    AutoHide = Convert.ToBoolean(value);
                }
                    break;
            }
        });
    }

    private void SystemManager_DisplaySettingsChanged(ScreenResolution resolution)
    {
        var QuickToolsLocation = SettingsManager.GetInt("QuickToolsLocation");
        UpdateLocation(QuickToolsLocation);
    }

    private void UpdateLocation(int QuickToolsLocation)
    {
        switch (QuickToolsLocation)
        {
            // top, left
            case 0:
            {
                this.SetWindowPosition(WindowPositions.TopLeft, Screen.PrimaryScreen);
                Top += Margin.Top;
                Left += Margin.Left;
            }
                break;

            // top, right
            case 1:
            {
                this.SetWindowPosition(WindowPositions.TopRight, Screen.PrimaryScreen);
                Top += Margin.Top;
                Left -= Margin.Left;
            }
                break;

            // bottom, left
            case 2:
            {
                this.SetWindowPosition(WindowPositions.BottomLeft, Screen.PrimaryScreen);
                Top -= Margin.Top;
                Left += Margin.Left;
            }
                break;

            // bottom, right
            default:
            case 3:
            {
                this.SetWindowPosition(WindowPositions.BottomRight, Screen.PrimaryScreen);
                Top -= Margin.Top;
                Left -= Margin.Left;
            }
                break;
        }

        // prevent window's from being too tall, add margin for top and bottom
        var maxHeight = (int)(Screen.PrimaryScreen.WpfBounds.Height - 2 * Margin.Top);
        if (Height > maxHeight)
            Height = maxHeight;
    }

    private void PowerManager_PowerStatusChanged(PowerStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
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

            if (PowerManager.PowerStatusIcon.TryGetValue(Key, out var glyph))
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
            }
            else
            {
                BatteryIndicatorLifeRemaining.Text = string.Empty;
            }
        });
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // load gamepad navigation maanger
        gamepadFocusManager = new(this, ContentFrame);
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        // do something
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_SYSCOMMAND:
                var command = wParam.ToInt32() & 0xfff0;
                if (command == SC_MOVE) handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Hide();
    }

    public void UpdateVisibility()
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(async () =>
        {
            switch (Visibility)
            {
                case Visibility.Collapsed:
                case Visibility.Hidden:
                    Show();
                    Activate();
                    Focus();
                    Topmost = true;
                    break;
                case Visibility.Visible:
                    Hide();
                    break;
            }
        });
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // position and size settings
        switch (WindowState)
        {
            case WindowState.Normal:
            case WindowState.Maximized:
                SettingsManager.SetProperty("QuickToolsHeight", Height);
                break;
        }

        SettingsManager.SetProperty("QuickToolsIsPaneOpen", navView.IsPaneOpen);

        e.Cancel = !isClosing;
        Hide();
    }

    public void Close(bool v)
    {
        isClosing = v;
        Close();
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        hwndSource = PresentationSource.FromVisual(this) as HwndSource;

        if (hwndSource is null)
            return;

        hwndSource.AddHook(WndProc);

        switch (Visibility)
        {
            case Visibility.Collapsed:
            case Visibility.Hidden:
                hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                break;
            case Visibility.Visible:
                hwndSource.CompositionTarget.RenderMode = RenderMode.Default;
                break;
        }
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
        navView.SelectedItem = navView.MenuItems[0];

        // If navigation occurs on SelectionChanged, this isn't needed.
        // Because we use ItemInvoked to navigate, we need to call Navigate
        // here to load the home page.
        preNavItemTag = "QuickSettingsPage";
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

        if (ContentFrame.SourcePageType is not null)
        {
            var preNavPageType = ContentFrame.CurrentSourcePageType;
            var preNavPageName = preNavPageType.Name;

            var NavViewItem = navView.MenuItems
                .OfType<NavigationViewItem>().FirstOrDefault(n => n.Tag.Equals(preNavPageName));

            if (!(NavViewItem is null))
                navView.SelectedItem = NavViewItem;

            // navView.Header = new TextBlock() { Text = (string)((Page)e.Content).Title, Margin = new Thickness(0,-24,0,0) };//, FontSize = 14 };
        }
    }

    #endregion
}