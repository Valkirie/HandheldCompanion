using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Extensions;
using HandheldCompanion.Views.QuickPages;
using ModernWpf.Controls;
using ModernWpf.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for QuickTools.xaml
    /// </summary>
    public partial class QuickTools : Window
    {
        #region imports
        [ComImport, Guid("4ce576fa-83dc-4F88-951c-9d0782b4e376")]
        class UIHostNoLaunch
        {
        }

        [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ITipInvocation
        {
            void Toggle(IntPtr hwnd);
        }

        [DllImport("user32.dll", SetLastError = false)]
        static extern IntPtr GetDesktopWindow();
        #endregion

        // page vars
        private Dictionary<string, Page> _pages = new();
        private string preNavItemTag;

        public QuickPerformancePage performancePage;
        public QuickSettingsPage settingsPage;
        public QuickProfilesPage profilesPage;

        // touchscroll vars
        Point scrollPoint = new Point();
        double scrollOffset = 1;
        public static bool scrollLock = false;

        // manager vers
        public static BrightnessControl brightnessControl;

        public QuickTools()
        {
            InitializeComponent();

            // create manager(s)
            brightnessControl = new();

            // create pages
            performancePage = new QuickPerformancePage();
            settingsPage = new QuickSettingsPage();
            profilesPage = new QuickProfilesPage();

            _pages.Add("QuickPerformancePage", performancePage);
            _pages.Add("QuickSettingsPage", settingsPage);
            _pages.Add("QuickProfilesPage", profilesPage);

            this.SourceInitialized += QuickTools_SourceInitialized;
        }

        private void QuickTools_SourceInitialized(object? sender, EventArgs e)
        {
            this.HideMinimizeAndMaximizeButtons();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // update Position and Size
            this.Height = (int)Math.Max(this.MinHeight, Properties.Settings.Default.QuickToolsHeight);

            this.Left = Math.Min(SystemParameters.PrimaryScreenWidth - this.MinWidth, Properties.Settings.Default.QuickToolsLeft);
            this.Top = Math.Min(SystemParameters.PrimaryScreenHeight - this.MinHeight, Properties.Settings.Default.QuickToolsTop);
        }

        public void UpdateVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                Visibility visibility = Visibility.Visible;
                switch (Visibility)
                {
                    case Visibility.Visible:
                        visibility = Visibility.Collapsed;
                        break;
                    case Visibility.Collapsed:
                    case Visibility.Hidden:
                        WindowState = WindowState.Normal;
                        visibility = Visibility.Visible;
                        break;
                }
                Visibility = visibility;
            });
        }

        #region navView
        private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
                string navItemTag = (string)navItem.Tag;

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
            preNavItemTag = "QuickPerformancePage";
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

            if (ContentFrame.SourcePageType != null)
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

        #region scrollView
        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            scrollPoint = e.GetPosition(scrollViewer);
            scrollOffset = scrollViewer.VerticalOffset;
        }

        private bool hasScrolled;
        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (scrollPoint == new Point())
                return;

            if (scrollLock)
                return;

            double diff = (scrollPoint.Y - e.GetPosition(scrollViewer).Y);

            if (Math.Abs(diff) >= 3)
            {
                scrollViewer.ScrollToVerticalOffset(scrollOffset + diff);
                hasScrolled = true;
                e.Handled = true;
            }
            else
            {
                hasScrolled = false;
                e.Handled = false;
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollPoint = new Point();

            if (hasScrolled)
            {
                e.Handled = true;
                hasScrolled = false;
            }
        }

        private void scrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            scrollPoint = new Point();

            if (hasScrolled)
            {
                e.Handled = true;
                hasScrolled = false;
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
        }
        #endregion

        private void StartTabTip()
        {
            var uiHostNoLaunch = new UIHostNoLaunch();
            var tipInvocation = (ITipInvocation)uiHostNoLaunch;
            tipInvocation.Toggle(GetDesktopWindow());
            Marshal.ReleaseComObject(uiHostNoLaunch);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // position and size settings
            switch (WindowState)
            {
                case WindowState.Normal:
                    Properties.Settings.Default.QuickToolsLeft = this.Left;
                    Properties.Settings.Default.QuickToolsTop = this.Top;

                    Properties.Settings.Default.QuickToolsHeight = this.Height;
                    break;
            }

            Properties.Settings.Default.Save();

            // stop manager(s)
            brightnessControl.Dispose();

            e.Cancel = !isClosing;
            this.Visibility = Visibility.Collapsed;
        }

        private bool isClosing;
        public void Close(bool v)
        {
            isClosing = v;
            this.Close();
        }

        private void navView_ShortcutInvoked(object sender, RoutedEventArgs e)
        {
            TitleBarButton button = (TitleBarButton)sender;
            switch (button.Tag)
            {
                case "shortcutKeyboard":
                    StartTabTip();
                    break;
                case "shortcutDesktop":
                    MainWindow.inputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_D });
                    break;
                case "shortcutESC":
                    MainWindow.inputsManager.KeyPress(VirtualKeyCode.ESCAPE);
                    break;
            }
        }
    }
}
