using HandheldCompanion.Managers;
using HandheldCompanion.Views.QuickPages;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Navigation;
using Windows.System.Power;
using Application = System.Windows.Application;
using Page = System.Windows.Controls.Page;
using PowerManager = ControllerCommon.Managers.PowerManager;
using SystemPowerManager = Windows.System.Power.PowerManager;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for QuickTools.xaml
    /// </summary>
    public partial class OverlayQuickTools : Window
    {
        // page vars
        private Dictionary<string, Page> _pages = new();
        private string preNavItemTag;

        public QuickPerformancePage performancePage;
        public QuickSettingsPage settingsPage;
        public QuickProfilesPage profilesPage;
        public QuickSuspenderPage suspenderPage;

        public OverlayQuickTools()
        {
            InitializeComponent();
            ShowActivated = true;

            // create manager(s)
            PowerManager.PowerStatusChanged += PowerManager_PowerStatusChanged;
            PowerManager_PowerStatusChanged(SystemInformation.PowerStatus);

            // create pages
            performancePage = new QuickPerformancePage();
            settingsPage = new QuickSettingsPage();
            profilesPage = new QuickProfilesPage();
            suspenderPage = new QuickSuspenderPage();

            _pages.Add("QuickPerformancePage", performancePage);
            _pages.Add("QuickSettingsPage", settingsPage);
            _pages.Add("QuickProfilesPage", profilesPage);
            _pages.Add("QuickSuspenderPage", suspenderPage);

            // update Position and Size
            Left = Math.Min(SystemParameters.PrimaryScreenWidth - MinWidth, SettingsManager.GetDouble("QuickToolsLeft"));
            Top = Math.Min(SystemParameters.PrimaryScreenHeight - MinHeight, SettingsManager.GetDouble("QuickToolsTop"));
            Height = (int)Math.Max(MinHeight, SettingsManager.GetDouble("QuickToolsHeight"));
        }

        private void PowerManager_PowerStatusChanged(PowerStatus status)
        {
            // UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                int BatteryLifePercent = (int)Math.Truncate(status.BatteryLifePercent * 100.0f);
                BatteryIndicatorPercentage.Text = $"{BatteryLifePercent}%";

                // get status key
                string KeyStatus = string.Empty;
                switch (status.PowerLineStatus)
                {
                    case System.Windows.Forms.PowerLineStatus.Online:
                        KeyStatus = "Charging";
                        break;
                    default:
                        {
                            EnergySaverStatus energy = SystemPowerManager.EnergySaverStatus;
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
                int KeyValue = (int)Math.Truncate(status.BatteryLifePercent * 10);

                // set key
                string Key = $"Battery{KeyStatus}{KeyValue}";

                if (PowerManager.PowerStatusIcon.ContainsKey(Key))
                    BatteryIndicatorIcon.Glyph = PowerManager.PowerStatusIcon[Key];

                if (status.BatteryLifeRemaining > 0)
                {
                    TimeSpan time = TimeSpan.FromSeconds(status.BatteryLifeRemaining);

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
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;

            if (hwndSource != null)
                hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
        }

        public void UpdateVisibility()
        {
            // UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (Visibility)
                {
                    case Visibility.Visible:
                        this.Hide();
                        break;
                    case Visibility.Collapsed:
                    case Visibility.Hidden:
                        this.Show();
                        this.Activate();
                        break;
                }
            });
        }

        #region navView
        private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is not null)
            {
                NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
                string navItemTag = (string)navItem.Tag;

                switch (navItemTag)
                {
                    default:
                        preNavItemTag = navItemTag;
                        break;
                    case "shortcutKeyboard":
                    case "shortcutDesktop":
                    case "shortcutESC":
                    case "shortcutExpand":
                        HotkeysManager.TriggerRaised(navItemTag, null, false, true);
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
                    .OfType<NavigationViewItem>()
                    .Where(n => n.Tag.Equals(preNavPageName)).FirstOrDefault();

                if (!(NavViewItem is null))
                    navView.SelectedItem = NavViewItem;

                // navView.Header = new TextBlock() { Text = (string)((Page)e.Content).Title, Margin = new Thickness(0,-24,0,0) };//, FontSize = 14 };
            }
        }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // position and size settings
            switch (WindowState)
            {
                case WindowState.Normal:
                case WindowState.Maximized:
                    SettingsManager.SetProperty("QuickToolsLeft", Left);
                    SettingsManager.SetProperty("QuickToolsTop", Top);
                    SettingsManager.SetProperty("QuickToolsHeight", Height);
                    break;
            }

            e.Cancel = !isClosing;
            this.Visibility = Visibility.Collapsed;
        }

        private bool isClosing;
        public void Close(bool v)
        {
            isClosing = v;
            this.Close();
        }
    }
}