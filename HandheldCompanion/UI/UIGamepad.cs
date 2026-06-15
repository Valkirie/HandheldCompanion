using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.UI;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Frame = iNKORE.UI.WPF.Modern.Controls.Frame;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using Page = System.Windows.Controls.Page;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public class UIGamepad
    {
        #region events
        public static event GotFocusEventHandler? GotFocus;
        public delegate void GotFocusEventHandler(string Name);

        public static event LostFocusEventHandler? LostFocus;
        public delegate void LostFocusEventHandler(string Name);
        #endregion

        private GamepadWindow gamepadWindow;
        private string windowName = string.Empty;

        private ScrollViewer? scrollViewer;

        // NavigationViews
        // - windowNavigationView: the NavigationView hosted by the Window (e.g. MainWindow)
        // - pageNavigationView: an optional NavigationView hosted inside the current Page (e.g. LayoutPage)
        private NavigationView? windowNavigationView;
        private NavigationView? pageNavigationView;

        private Frame gamepadFrame;
        private Page? gamepadPage;
        private Timer gamepadTimer;

        // tooltip
        private static Timer tooltipTimer = null!;
        private static ToolTip tooltip = new ToolTip
        {
            Content = "This is a tooltip!",
            Placement = PlacementMode.Top,
            IsOpen = false // Start with tooltip hidden
        };

        private bool _rendered;
        private readonly object _rendering = new();
        private bool _isNavigationViewFocusNavigationInProgress;
        private bool _isNavigationViewContentRestoreInProgress;
        private int _navigationViewContentRestoreRequestId;
        // True from the moment ContentNavigated fires for a new page until ContentRendered consumes it.
        // Ensures ShouldKeepFocusOnWindowNavigation() never suppresses the first-focus pass.
        private bool _justNavigatedToNewPage;
        // The iNKORE Frame sitting inside pageNavigationView (e.g. LayoutPage's ContentFrame).
        // Tracked so we can subscribe to its ContentRendered when the outer page renders before
        // the inner sub-page has finished loading.
        private Frame? _embeddedNavFrame;

        private readonly ButtonState prevButtonState = new();
        private volatile bool _suppressNextInput;
        private volatile bool _layoutModeIsDesktop;
        private Control? _lastWindowNavigationItem;
        private readonly Dictionary<Page, PageFocusState> _pageFocusStates = [];

        // key: Window, store which window has focus
        private static readonly ConcurrentDictionary<string, bool> _focused = new();

        private bool IsQuicktools => this.windowName.Equals("QuickTools");
        private bool IsMainWindow => !IsQuicktools;

        // Store profile Guid when toggling like, to restore focus after ProfileManager updates
        private Guid? pendingFocusRestoreProfileGuid = null;

        public static bool HasFocus()
        {
            return _focused.Any(w => w.Value);
        }

        /// <summary>
        /// Suppresses the next button-state change on this window.
        /// When the next <c>InputsUpdated</c> tick arrives with a changed button state,
        /// it is silently recorded as already-seen without firing any action.
        /// Use this on the destination window just before it gains focus mid-press,
        /// so that buttons still held during the transition are not replayed as new presses.
        /// </summary>
        public void SuppressNextInput()
        {
            _suppressNextInput = true;
        }

        public bool CanGoBack
        {
            get
            {
                if (GetFocusedElement() is Control control)
                {
                    if (control is NavigationViewItem navigationViewItem)
                    {
                        if (navigationViewItem.Tag is string navString && navString.Equals(gamepadWindow.HomePageKey))
                            return false;
                        return true;
                    }
                }

                return true;
            }
        }

        private sealed class PageFocusState
        {
            public Control? LastContentControl { get; set; }
            public Guid? LastContentProfileGuid { get; set; }
            public Control? LastEmbeddedNavigationItem { get; set; }
            public Dictionary<string, Control> LastContentControlsByView { get; } = [];
            public Dictionary<string, Guid> LastProfileGuidsByScope { get; } = [];
        }

        private enum FocusSource
        {
            Visibility,
            Activate,
            Focus
        }

        public UIGamepad(GamepadWindow gamepadWindow, Frame contentFrame)
        {
            // set current window
            this.gamepadWindow = gamepadWindow;
            this.gamepadWindow.AddHandler(FocusManager.GotFocusEvent, new RoutedEventHandler(GamepadWindow_GotFocus));
            this.gamepadWindow.ContentDialogOpened += ContentDialogOpened;
            this.gamepadWindow.ContentDialogClosed += ContentDialogClosed;

            this.windowName = gamepadWindow.Tag?.ToString() ?? string.Empty;

            if (gamepadWindow is OverlayQuickTools quickTools)
            {
                quickTools.GotGamepadWindowFocus += (sender) => WindowGotFocus(null, null, FocusSource.Visibility);
                quickTools.LostGamepadWindowFocus += (sender) => WindowLostFocus(null, null, FocusSource.Visibility);
            }
            else if (gamepadWindow is MainWindow mainWindow)
            {
                // Only subscribe to window-level Activated/Deactivated and StateChanged.
                // Element-level GotFocus/LostFocus fire on every mouse click inside the window
                // and create spurious WindowGotFocus calls that clash with gamepad input.
                mainWindow.Activated += (sender, e) => WindowGotFocus(sender, null, FocusSource.Activate);
                mainWindow.Deactivated += (sender, e) => WindowLostFocus(sender, null, FocusSource.Activate);
                mainWindow.StateChanged += (sender, e) =>
                {
                    switch (mainWindow.WindowState)
                    {
                        case WindowState.Normal:
                        case WindowState.Maximized:
                            WindowGotFocus(sender, null, FocusSource.Activate);
                            break;
                        case WindowState.Minimized:
                            WindowLostFocus(sender, null, FocusSource.Activate);
                            break;
                    }
                };
            }

            gamepadFrame = contentFrame;
            gamepadFrame.Navigated += ContentNavigated;

            gamepadTimer = new Timer(250) { AutoReset = false };
            gamepadTimer.Elapsed += ContentRendered;

            tooltipTimer = new Timer(2000) { AutoReset = false };
            tooltipTimer.Elapsed += TooltipTimer_Elapsed;

            ControllerManager.InputsUpdated += InputsUpdated;
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            _layoutModeIsDesktop = (LayoutModes)ManagerFactory.settingsManager.GetInt("LayoutMode") == LayoutModes.Desktop;
        }

        private void GamepadWindow_GotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject dependencyObject)
                return;

            Control? control = dependencyObject as Control ?? WPFUtils.FindParent<Control>(dependencyObject);
            if (control is null || !IsValidFocusableContentElement(control))
                return;

            TrackFocusedControl(control);
        }

        public void Loaded()
        {
            this.scrollViewer = WPFUtils.FindVisualChild<ScrollViewer>(gamepadWindow);
            this.windowNavigationView = FindWindowNavigationView();

            // will be resolved once the first Page is rendered
            this.pageNavigationView = null;

            // ContentRendered may have already fired before Loaded() was called (the page navigated and
            // rendered while windowNavigationView was still null), so _lastWindowNavigationItem was never
            // populated.  Re-run the post-render sync now that the NavigationView is resolved so that
            // gamepad navigation works immediately on startup without requiring a minimize/restore cycle.
            if (gamepadPage is not null && gamepadPage.IsLoaded)
                ContentRendered(null, null);
        }

        private PageFocusState GetPageFocusState(Page page)
        {
            if (!_pageFocusStates.TryGetValue(page, out PageFocusState? state))
            {
                state = new PageFocusState();
                _pageFocusStates[page] = state;
            }

            return state;
        }

        private PageFocusState? TryGetCurrentPageFocusState()
        {
            return gamepadPage is null ? null : GetPageFocusState(gamepadPage);
        }

        private static NavigationViewItem? ResolveNavigationViewItemContainer(NavigationView? navigationView, object? item)
        {
            if (navigationView is null || item is null)
                return null;

            if (item is NavigationViewItem navigationViewItem)
                return navigationViewItem;

            foreach (Control control in GetNavigationItems(navigationView))
            {
                if (control is NavigationViewItem container && ReferenceEquals(container.DataContext, item))
                    return container;
            }

            return null;
        }

        private static string? GetLibraryCollectionKey(Control? control)
        {
            if (control is not Button button || button.DataContext is not CollectionGroupViewModel group)
                return null;

            if (group.Collection is not null)
                return $"collection:{group.Collection.Id}";

            return string.Equals(group.Name, "Favorites", StringComparison.Ordinal) ? "favorites" : null;
        }

        private Control? FindLibraryCollectionControlByKey(Page page, string? collectionKey)
        {
            if (string.IsNullOrWhiteSpace(collectionKey))
                return null;

            return WPFUtils.FindVisualChildren<Button>(page)
                .FirstOrDefault(button => button.IsEnabled
                    && button.IsVisible
                    && string.Equals(GetLibraryCollectionKey(button), collectionKey, StringComparison.Ordinal));
        }

        private Control? FindDefaultLibraryCollectionControl(Page page)
        {
            return WPFUtils.GetTopLeftControl<Button>(
                WPFUtils.FindVisualChildren<Button>(page)
                    .Where(button => button.IsEnabled
                        && button.IsVisible
                        && !string.IsNullOrWhiteSpace(GetLibraryCollectionKey(button)))
                    .Cast<Control>()
                    .ToList());
        }

        private NavigationView? FindWindowNavigationView()
        {
            if (gamepadWindow is FrameworkElement frameworkElement && frameworkElement.FindName("navView") is NavigationView namedNavigationView)
                return namedNavigationView;

            return WPFUtils.FindVisualChild<NavigationView>(gamepadWindow);
        }

        private NavigationView? FindActivePageNavigationView(Page? page = null)
        {
            page ??= gamepadPage;
            if (page is null)
                return null;

            if (page is FrameworkElement frameworkElement && frameworkElement.FindName("navView") is NavigationView namedNavigationView)
                return namedNavigationView == windowNavigationView ? null : namedNavigationView;

            return WPFUtils.FindVisualChildren<NavigationView>(page)
                .FirstOrDefault(navigationView => navigationView != windowNavigationView
                    && Window.GetWindow(navigationView) == gamepadWindow);
        }

        private static NavigationView? FindOwningNavigationView(Control? control)
        {
            return control is null ? null : WPFUtils.FindParent<NavigationView>(control);
        }

        private static NavigationViewItem? GetFirstNavigationViewItem(NavigationView? navigationView)
        {
            return navigationView is null
                ? null
                : GetNavigableNavigationViewItems(navigationView).FirstOrDefault();
        }

        private static Control NormalizeNavigationViewFocus(Control? control)
        {
            if (control is null)
                return null!;

            return WPFUtils.FindParent<NavigationViewItem>(control) ?? control;
        }

        private bool IsEmbeddedNavigationItem(Control? control)
        {
            return control is NavigationViewItem navigationViewItem
                && FindOwningNavigationView(navigationViewItem) == pageNavigationView;
        }

        private static bool TryGetNavigationItemKey(NavigationViewItem navigationViewItem, out string key)
        {
            key = string.Empty;

            if (navigationViewItem.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                key = tag;
                return true;
            }

            return false;
        }

        private static string? GetPageFromNavigationViewItemTag(NavigationViewItem? navigationViewItem)
        {
            return navigationViewItem is not null && TryGetNavigationItemKey(navigationViewItem, out string key) ? key : null;
        }

        private static bool IsNavigableNavigationViewItem(NavigationViewItem navigationViewItem)
        {
            return navigationViewItem.IsLoaded
                && navigationViewItem.IsVisible
                && navigationViewItem.IsEnabled
                && navigationViewItem.Focusable
                && navigationViewItem.IsTabStop
                && !string.IsNullOrWhiteSpace(GetPageFromNavigationViewItemTag(navigationViewItem));
        }

        private static void AddNavigableNavigationViewItems(NavigationView navigationView, System.Collections.IEnumerable? sourceItems, List<NavigationViewItem> items)
        {
            if (sourceItems is null)
                return;

            foreach (object? sourceItem in sourceItems)
            {
                NavigationViewItem? navigationViewItem = ResolveNavigationViewItemContainer(navigationView, sourceItem);
                if (navigationViewItem is null)
                    continue;

                if (IsNavigableNavigationViewItem(navigationViewItem))
                    items.Add(navigationViewItem);

                if (navigationViewItem.MenuItems is not null)
                    AddNavigableNavigationViewItems(navigationView, navigationViewItem.MenuItems, items);
            }
        }

        private static List<NavigationViewItem> GetNavigableNavigationViewItems(NavigationView? navigationView)
        {
            if (navigationView is null)
                return [];

            List<NavigationViewItem> items = [];

            try
            {
                AddNavigableNavigationViewItems(navigationView, navigationView.MenuItems, items);
                AddNavigableNavigationViewItems(navigationView, navigationView.FooterMenuItems, items);
            }
            catch
            {
                items.Clear();
            }

            if (items.Count > 0)
                return items;

            return WPFUtils.FindVisualChildren<NavigationViewItem>(navigationView)
                .Where(IsNavigableNavigationViewItem)
                .ToList();
        }

        private NavigationViewItem? GetCurrentNavigationViewItem(NavigationView? navigationView)
        {
            List<NavigationViewItem> items = GetNavigableNavigationViewItems(navigationView);
            if (items.Count == 0)
                return null;

            NavigationViewItem? currentItem = null;

            if (navigationView == windowNavigationView)
                currentItem = _lastWindowNavigationItem as NavigationViewItem;
            else if (navigationView == pageNavigationView)
                currentItem = TryGetCurrentPageFocusState()?.LastEmbeddedNavigationItem as NavigationViewItem;

            if (currentItem is not null && items.Contains(currentItem))
                return currentItem;

            currentItem = ResolveNavigationViewItemContainer(navigationView, navigationView?.SelectedItem);
            if (currentItem is not null && items.Contains(currentItem))
                return currentItem;

            currentItem = navigationView is null ? null : GetSelectedNavigationViewItem(navigationView);
            return currentItem is not null && items.Contains(currentItem) ? currentItem : items.FirstOrDefault();
        }

        private bool FocusNextNavigationViewItem(NavigationView? navigationView, bool moveLeft)
        {
            List<NavigationViewItem> items = GetNavigableNavigationViewItems(navigationView);
            if (items.Count == 0)
                return false;

            NavigationViewItem currentItem = GetCurrentNavigationViewItem(navigationView) ?? items[0];
            int currentIndex = items.IndexOf(currentItem);
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = moveLeft ? currentIndex - 1 : currentIndex + 1;
            if (nextIndex < 0)
                nextIndex = items.Count - 1;
            else if (nextIndex >= items.Count)
                nextIndex = 0;

            NavigationViewItem nextItem = items[nextIndex];

            if (navigationView == windowNavigationView)
                _lastWindowNavigationItem = nextItem;
            else if (gamepadPage is not null && navigationView == pageNavigationView)
                GetPageFocusState(gamepadPage).LastEmbeddedNavigationItem = nextItem;

            Focus(nextItem);
            return true;
        }

        private static void SetSelectedNavigationViewItem(NavigationView navigationView, NavigationViewItem navigationViewItem)
        {
            object selectedItem = navigationView.MenuItemsSource is not null && navigationViewItem.DataContext is not null
                ? navigationViewItem.DataContext
                : navigationViewItem;

            if (!ReferenceEquals(navigationView.SelectedItem, selectedItem))
                navigationView.SelectedItem = selectedItem;
        }

        private string? GetActivePageViewKey(Page page, NavigationView? navigationView = null)
        {
            navigationView ??= FindActivePageNavigationView(page);

            if (navigationView is not null)
                return GetPageFromNavigationViewItemTag(GetCurrentNavigationViewItem(navigationView));

            return GetPageFocusScopeKey(page);
        }

        private bool IsNavigationViewFocusChangeInProgress()
        {
            return _isNavigationViewFocusNavigationInProgress || _isNavigationViewContentRestoreInProgress;
        }

        private DependencyObject? GetNavigationViewContentRoot(NavigationView? navigationView, Page? page)
        {
            // Resolve the content root for the embedded NavigationView (pageNavigationView).
            if (navigationView == pageNavigationView)
            {
                Frame? embeddedFrame = FindEmbeddedNavFrame(navigationView);
                if (embeddedFrame is not null)
                {
                    DependencyObject? result = embeddedFrame.Content as DependencyObject ?? embeddedFrame;
                    LogManager.LogTrace("[UIGamepad] GetNavigationViewContentRoot(pageNavView): frame={0}, content={1}, returning={2}",
                        embeddedFrame.GetType().Name,
                        embeddedFrame.Content?.GetType().Name ?? "null",
                        result.GetType().Name);
                    return result;
                }

                if (navigationView?.Content is DependencyObject content)
                    return content;

                // No frame found — return null so callers can detect "not ready yet"
                return null;
            }

            // When the window-level nav is requested but an embedded nav is active,
            // scope to the embedded nav's content root so we don't scan sidebar items.
            if (navigationView == windowNavigationView && pageNavigationView is not null)
                return GetNavigationViewContentRoot(pageNavigationView, page);

            return page;
        }

        // Returns the iNKORE Frame that sits inside the given NavigationView's content area.
        // For LayoutPage the XAML places an <ui:Frame Name="ContentFrame"/> as the NavigationView content.
        private static Frame? FindEmbeddedNavFrame(NavigationView? navigationView)
        {
            if (navigationView is null)
                return null;

            if (navigationView.Content is Frame f)
                return f;

            // Fallback: first Frame anywhere inside the NavigationView
            return WPFUtils.FindVisualChild<Frame>(navigationView);
        }

        public bool IsValidFocusableContentElement(Control? control, DependencyObject? scopeRoot = null, bool includeNavigationViewItems = false)
        {
            if (control is null)
                return false;

            if (!includeNavigationViewItems && control is NavigationViewItem)
                return false;

            if (control is ItemsControl and not Selector)
                return false;

            // A SettingsCard with IsClickEnabled=false is non-interactive; skip it
            if (control is SettingsCard settingsCard && !settingsCard.IsClickEnabled)
                return false;

            // A TextBox with IsReadOnly=true is non-interactive; skip it
            if (control is TextBox textBox && textBox.IsReadOnly)
                return false;

            return !IsTransientContainerControl(control)
                && control.IsLoaded
                && control.IsVisible
                && control.IsEnabled
                && control.Focusable
                && control.Opacity > 0
                && control.ActualWidth > 0
                && control.ActualHeight > 0
                && Window.GetWindow(control) == gamepadWindow
                && (scopeRoot is null || VisualTreeHelperExtensions.FindCommonAncestor(control, scopeRoot as Visual) == scopeRoot);
        }

        private Control? GetTopLeftFocusableContentControl(DependencyObject? scopeRoot, bool includeNavigationViewItems = false)
        {
            if (scopeRoot is null)
                return null;

            List<Control> allControls = WPFUtils.FindVisualChildren<Control>(scopeRoot).ToList();
            List<Control> controls = allControls
                .Where(control => IsValidFocusableContentElement(control, scopeRoot, includeNavigationViewItems))
                .ToList();

            if (controls.Count == 0 && allControls.Count > 0)
            {
                // Log why the first few candidates were rejected
                foreach (Control c in allControls.Take(5))
                {
                    if (c is ItemsControl || IsTransientContainerControl(c)) continue;
                    LogManager.LogTrace(
                        "[UIGamepad] GetTopLeft reject {0}: isNav={1} loaded={2} visible={3} enabled={4} focusable={5} opacity={6} w={7} h={8} window={9} ancestor={10}",
                        c.GetType().Name,
                        c is NavigationViewItem,
                        c.IsLoaded, c.IsVisible, c.IsEnabled, c.Focusable,
                        c.Opacity, (int)c.ActualWidth, (int)c.ActualHeight,
                        Window.GetWindow(c) == gamepadWindow,
                        scopeRoot is null ? "null" : VisualTreeHelperExtensions.FindCommonAncestor(c, scopeRoot as Visual) == scopeRoot ? "ok" : "fail");
                }
            }

            return WPFUtils.GetTopLeftControl<Control>(controls);
        }

        private Control? ResolveStoredContentControl(Page page, NavigationView? navigationView)
        {
            PageFocusState state = GetPageFocusState(page);
            DependencyObject? contentRoot = GetNavigationViewContentRoot(navigationView, page);
            string? viewKey = GetActivePageViewKey(page, navigationView);

            if (!string.IsNullOrWhiteSpace(viewKey) && state.LastContentControlsByView.TryGetValue(viewKey, out Control? storedViewControl))
            {
                if (IsValidFocusableContentElement(storedViewControl, contentRoot))
                    return storedViewControl;

                state.LastContentControlsByView.Remove(viewKey);
            }

            if (IsLibraryPage(page))
            {
                string? scopeKey = GetPageFocusScopeKey(page);

                if (page.DataContext is LibraryPageViewModel libraryPageViewModel
                    && libraryPageViewModel.IsCollectionsOverviewNavigationKey(scopeKey))
                {
                    Control? collectionControl = FindLibraryCollectionControlByKey(page, libraryPageViewModel.GetLastCollectionsOverviewItemKey())
                        ?? FindDefaultLibraryCollectionControl(page);
                    if (IsValidFocusableContentElement(collectionControl, contentRoot))
                    {
                        state.LastContentControl = collectionControl;
                        return collectionControl;
                    }

                    return null;
                }

                if (!string.IsNullOrWhiteSpace(scopeKey)
                    && state.LastProfileGuidsByScope.TryGetValue(scopeKey, out Guid scopedProfileGuid))
                {
                    Control? scopedControl = FindProfileControl(scopedProfileGuid, page);
                    if (IsValidFocusableContentElement(scopedControl, contentRoot))
                    {
                        state.LastContentControl = scopedControl;
                        state.LastContentProfileGuid = scopedProfileGuid;
                        return scopedControl;
                    }
                }
            }

            if (IsValidFocusableContentElement(state.LastContentControl, contentRoot))
            {
                LogManager.LogTrace("[UIGamepad] ResolveStoredContentControl: restoring LastContentControl={0}",
                    state.LastContentControl!.GetType().Name);
                return state.LastContentControl;
            }
            else if (state.LastContentControl is not null)
            {
                LogManager.LogTrace("[UIGamepad] ResolveStoredContentControl: LastContentControl={0} failed validity check (loaded={1} visible={2} enabled={3})",
                    state.LastContentControl.GetType().Name,
                    state.LastContentControl.IsLoaded,
                    state.LastContentControl.IsVisible,
                    state.LastContentControl.IsEnabled);
            }

            if (state.LastContentProfileGuid.HasValue)
            {
                Control? resolvedControl = FindProfileControl(state.LastContentProfileGuid.Value, page);
                if (IsValidFocusableContentElement(resolvedControl, contentRoot))
                {
                    state.LastContentControl = resolvedControl;
                    return resolvedControl;
                }
            }

            return null;
        }

        private bool RestoreOrFocusTopLeftElementInNavigationViewContent(NavigationView? navigationView)
        {
            if (gamepadPage is null)
                return false;

            try
            {
                _isNavigationViewContentRestoreInProgress = true;

                DependencyObject? contentRoot = GetNavigationViewContentRoot(navigationView, gamepadPage);
                LogManager.LogTrace("[UIGamepad] RestoreOrFocus: navView={0}, contentRoot={1}",
                    navigationView == pageNavigationView ? "pageNav" : navigationView == windowNavigationView ? "windowNav" : "other",
                    contentRoot?.GetType().Name ?? "null");

                Control? stored = ResolveStoredContentControl(gamepadPage, navigationView);
                Control? topLeft = stored is null ? GetTopLeftFocusableContentControl(contentRoot) : null;
                Control? topLeftWithNav = (stored is null && topLeft is null) ? GetTopLeftFocusableContentControl(contentRoot, includeNavigationViewItems: true) : null;
                Control? navItem = (stored is null && topLeft is null && topLeftWithNav is null) ? GetCurrentNavigationViewItem(navigationView) : null;
                Control? control = stored ?? topLeft ?? topLeftWithNav ?? navItem;

                LogManager.LogTrace("[UIGamepad] RestoreOrFocus: stored={0}, topLeft={1}, topLeftWithNav={2}, navItem={3} => control={4}",
                    stored?.GetType().Name ?? "null",
                    topLeft?.GetType().Name ?? "null",
                    topLeftWithNav?.GetType().Name ?? "null",
                    navItem?.GetType().Name ?? "null",
                    control?.GetType().Name ?? "null");

                if (control is not null)
                {
                    Focus(control);
                    return true;
                }

                Frame? embeddedFrame = FindEmbeddedNavFrame(navigationView);
                if (embeddedFrame is not null)
                {
                    LogManager.LogTrace("[UIGamepad] RestoreOrFocus: no control found, subscribing EmbeddedNavFrame_Navigated");
                    embeddedFrame.Navigated -= EmbeddedNavFrame_Navigated;
                    embeddedFrame.Navigated += EmbeddedNavFrame_Navigated;
                    return true;
                }

                return false;
            }
            finally
            {
                _isNavigationViewContentRestoreInProgress = false;
            }
        }

        private void RestoreOrFocusTopLeftElementInNavigationViewContentAsync(NavigationView? navigationView, Page page, string navigationTarget)
        {
            // The embedded Frame's ContentRendered fires at exactly the right moment — after the
            // sub-page's visual tree is fully built — regardless of dispatcher priority races.
            Frame? embeddedFrame = FindEmbeddedNavFrame(navigationView);
            if (embeddedFrame is null)
                return;

            int requestId = ++_navigationViewContentRestoreRequestId;

            NavigatedEventHandler? handler = null;
            handler = (s, e) =>
            {
                embeddedFrame.Navigated -= handler;

                // Discard stale requests (user navigated away before this fired).
                if (requestId != _navigationViewContentRestoreRequestId)
                {
                    LogManager.LogTrace("[UIGamepad] RestoreAsync Navigated: stale request {0} vs {1}, discarding", requestId, _navigationViewContentRestoreRequestId);
                    return;
                }

                if (!ReferenceEquals(gamepadPage, page))
                {
                    LogManager.LogTrace("[UIGamepad] RestoreAsync Navigated: page changed, discarding");
                    return;
                }

                LogManager.LogTrace("[UIGamepad] RestoreAsync Navigated: fired for target={0}, content={1}",
                    navigationTarget, e.Content?.GetType().Name ?? "null");

                // Defer to DispatcherPriority.Loaded so all layout/render passes complete
                // (ItemsControl containers, IsSupported visibility bindings, etc.) before we scan.
                FrameworkElement? frameContent = e.Content as FrameworkElement;
                gamepadWindow.Dispatcher.BeginInvoke(() =>
                {
                    if (requestId != _navigationViewContentRestoreRequestId || !ReferenceEquals(gamepadPage, page))
                        return;

                    frameContent?.UpdateLayout();
                    RestoreOrFocusTopLeftElementInNavigationViewContent(navigationView);
                }, DispatcherPriority.Loaded);
            };

            embeddedFrame.Navigated += handler;
        }

        private bool IsCurrentNavigationViewTarget(NavigationView navigationView, string navigationTarget)
        {
            if (navigationView == windowNavigationView)
                return string.Equals(gamepadPage?.GetType().Name, navigationTarget, StringComparison.Ordinal);

            // SelectionFollowsFocus updates SelectedItem before the Frame has navigated,
            // so we must check the Frame's actual content instead of the nav selection.
            if (navigationView == pageNavigationView)
            {
                Frame? embeddedFrame = FindEmbeddedNavFrame(navigationView);
                if (embeddedFrame is not null)
                    return string.Equals(embeddedFrame.Content?.GetType().Name, navigationTarget, StringComparison.Ordinal);
            }

            return gamepadPage is not null
                && string.Equals(GetActivePageViewKey(gamepadPage, navigationView), navigationTarget, StringComparison.Ordinal);
        }

        private bool NavigateActivePageNavigationView(string navigationTarget)
        {
            if (gamepadPage is null)
                return false;

            if (gamepadPage is LayoutPage layoutPage)
            {
                layoutPage.NavView_Navigate(navigationTarget);
                return true;
            }

            try
            {
                var method = gamepadPage.GetType().GetMethod("NavView_Navigate", new[] { typeof(string) });
                if (method is null)
                    return false;

                method.Invoke(gamepadPage, new object[] { navigationTarget });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool NavigateFromFocusedNavigationViewItem(NavigationViewItem navigationViewItem)
        {
            NavigationView? navigationView = FindOwningNavigationView(navigationViewItem);
            string? navigationTarget = GetPageFromNavigationViewItemTag(navigationViewItem);
            if (navigationView is null || string.IsNullOrWhiteSpace(navigationTarget))
                return false;

            if (IsNavigationViewFocusChangeInProgress())
                return true;

            try
            {
                _isNavigationViewFocusNavigationInProgress = true;
                SetSelectedNavigationViewItem(navigationView, navigationViewItem);

                if (navigationView == windowNavigationView)
                {
                    _lastWindowNavigationItem = navigationViewItem;

                    if (!IsCurrentNavigationViewTarget(navigationView, navigationTarget))
                    {
                        gamepadWindow.NavigateToPage(navigationTarget);
                        return true;
                    }

                    return RestoreOrFocusTopLeftElementInNavigationViewContent(windowNavigationView);
                }

                if (gamepadPage is null || navigationView != pageNavigationView)
                    return false;

                GetPageFocusState(gamepadPage).LastEmbeddedNavigationItem = navigationViewItem;

                LogManager.LogTrace("[UIGamepad] NavigateFromFocused: embedded nav target={0}, currentTarget={1}, isCurrentTarget={2}",
                    navigationTarget,
                    GetActivePageViewKey(gamepadPage, navigationView) ?? "null",
                    IsCurrentNavigationViewTarget(navigationView, navigationTarget));

                if (!IsCurrentNavigationViewTarget(navigationView, navigationTarget))
                {
                    if (!NavigateActivePageNavigationView(navigationTarget))
                        return false;

                    RestoreOrFocusTopLeftElementInNavigationViewContentAsync(navigationView, gamepadPage, navigationTarget);
                    return true;
                }

                // No Frame means the nav only filters content (e.g. library page).
                // Defer so the content update/filtering can complete before we search for controls.
                if (FindEmbeddedNavFrame(navigationView) is null)
                {
                    Page pageRef = gamepadPage;
                    NavigationView navRef = navigationView;
                    gamepadWindow.Dispatcher.BeginInvoke(() =>
                    {
                        if (ReferenceEquals(gamepadPage, pageRef))
                            RestoreOrFocusTopLeftElementInNavigationViewContent(navRef);
                    }, DispatcherPriority.Loaded);
                    return true;
                }

                return RestoreOrFocusTopLeftElementInNavigationViewContent(navigationView);
            }
            finally
            {
                _isNavigationViewFocusNavigationInProgress = false;
            }
        }

        private bool TryEnterContentFromNavigationItem(NavigationViewItem navigationViewItem)
        {
            return NavigateFromFocusedNavigationViewItem(navigationViewItem);
        }

        private bool TryFocusEmbeddedNavigationAnchor(Page page)
        {
            PageFocusState state = GetPageFocusState(page);
            if (IsUsableStoredControl(state.LastEmbeddedNavigationItem))
            {
                Focus(state.LastEmbeddedNavigationItem);
                return true;
            }

            NavigationViewItem? currentNavigationItem = ResolveNavigationViewItemContainer(pageNavigationView, pageNavigationView?.SelectedItem)
                ?? GetSelectedNavigationViewItem(pageNavigationView)
                ?? GetFirstNavigationViewItem(pageNavigationView);

            if (!IsUsableStoredControl(currentNavigationItem))
                return false;

            state.LastEmbeddedNavigationItem = currentNavigationItem;
            Focus(currentNavigationItem);
            return true;
        }

        private bool TryFocusWindowNavigationAnchor()
        {
            Control? anchor = _lastWindowNavigationItem
                ?? GetSelectedNavigationViewItem(windowNavigationView)
                ?? GetFirstNavigationViewItem(windowNavigationView);

            if (!IsUsableStoredControl(anchor))
                return false;

            _lastWindowNavigationItem = anchor;
            FocusWindowNavigationAnchor(anchor);
            return true;
        }

        private bool TryRestoreLastFocusedControl(Page page)
        {
            Control? control = ResolveStoredContentControl(page, pageNavigationView ?? windowNavigationView);
            if (control is null)
                return false;

            Focus(control);
            return true;
        }

        private bool TryFocusPageContent(Page? page)
        {
            if (page is null)
                return false;

            Control? control = ResolveStoredContentControl(page, windowNavigationView)
                ?? GetTopLeftFocusableContentControl(GetNavigationViewContentRoot(windowNavigationView, page));
            if (control is null)
                return false;

            Focus(control);
            return true;
        }

        private static bool IsLibraryPage(Page page)
        {
            return page is LibraryPage;
        }

        private string? GetPageFocusScopeKey(Page page)
        {
            if (page is LibraryPage && page.DataContext is LibraryPageViewModel libraryPageViewModel)
                return libraryPageViewModel.SelectedNavigationItem?.Key;

            return null;
        }

        private Control? GetTopLeftNavigableControl(List<Type>? extraIgnoredTypes = null)
        {
            List<Type> ignoreList =
            [
                typeof(NavigationViewItem),
                typeof(SplitView),
                typeof(ScrollViewer),
                typeof(Frame),
                typeof(Page)
            ];

            if (extraIgnoredTypes is not null)
                ignoreList.AddRange(extraIgnoredTypes);

            return WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements, ignoreList);
        }

        private static bool IsTransientContainerControl(Control? control)
        {
            return control is null
                || control is SplitView
                || control is ScrollViewer
                || control.GetType().Name is "TouchScrollViewer"
                || control is Frame
                || control is Page;
        }

        private Control? GetDefaultPageControl()
        {
            List<Type> ignoreList = [];

            if (IsQuicktools)
                ignoreList.Add(typeof(AppBarButton));

            return GetTopLeftNavigableControl(ignoreList);
        }

        private Control? GetDefaultPageContentControl(Page? page)
        {
            if (page is null)
                return GetDefaultPageControl();

            if (IsLibraryPage(page))
            {
                string? scopeKey = GetPageFocusScopeKey(page);

                if (page.DataContext is LibraryPageViewModel libraryPageViewModel
                    && libraryPageViewModel.IsCollectionsOverviewNavigationKey(scopeKey))
                {
                    return FindLibraryCollectionControlByKey(page, libraryPageViewModel.GetLastCollectionsOverviewItemKey())
                        ?? FindDefaultLibraryCollectionControl(page);
                }

                Control? control = WPFUtils.GetTopLeftControl<Button>(
                    WPFUtils.FindVisualChildren<Button>(page)
                        .Where(control => control.IsVisible
                            && Window.GetWindow(control) == gamepadWindow
                            && IsPreferredLibraryContentControlForScope(control, scopeKey))
                        .Cast<Control>()
                        .ToList());

                if (control is not null)
                    return control;
            }

            return GetDefaultPageControl();
        }

        private bool IsHomePage(Page page)
        {
            return string.Equals(page.GetType().Name, gamepadWindow.HomePageKey, StringComparison.Ordinal);
        }

        private bool IsCurrentHomePage()
        {
            return gamepadPage is not null && IsHomePage(gamepadPage);
        }

        private void ResetBackNavigationAtHomePage()
        {
            if (!IsCurrentHomePage())
                return;

            while (gamepadFrame.CanGoBack)
                gamepadFrame.RemoveBackEntry();
        }

        private void ContentDialogClosed(ContentDialog contentDialog)
        {
            if (gamepadPage is not null)
            {
                Control? control = ResolveStoredContentControl(gamepadPage, pageNavigationView ?? windowNavigationView)
                    ?? ResolveStoredContentControl(gamepadPage, windowNavigationView);
                if (_focused[windowName] && control is not null)
                    Focus(control);
            }
        }

        private bool HasFlyoutOpen = false;
        private List<MenuItem> flyoutMenuItems = new();  // populated from MenuFlyout.Items when open
        private MenuItem? focusedFlyoutItem = null;        // tracks which item is highlighted

        private void FocusFlyoutMenuItem(MenuItem menuItem)
        {
            List<MenuItem> siblingMenuItems = WPFUtils.GetSiblingMenuItems(menuItem);

            if (siblingMenuItems.Count == 0 && gamepadWindow.currentFlyoutButton?.Flyout is MenuFlyout menuFlyout)
                siblingMenuItems = WPFUtils.GetDirectMenuItems(menuFlyout);

            flyoutMenuItems = siblingMenuItems;
            focusedFlyoutItem = menuItem;
            Keyboard.Focus(menuItem);
            gamepadWindow.SetFocusedElement(menuItem);
        }

        private bool TryOpenFlyoutSubmenu(MenuItem menuItem)
        {
            if (!menuItem.HasItems)
                return false;

            MenuItem? firstChild = WPFUtils.GetDirectMenuItems(menuItem)
                .FirstOrDefault(m => m.IsEnabled);

            if (firstChild is null)
                return false;

            menuItem.IsSubmenuOpen = true;
            menuItem.Dispatcher.BeginInvoke(() => FocusFlyoutMenuItem(firstChild), DispatcherPriority.Loaded);
            return true;
        }

        private bool ShouldKeepFocusOnWindowNavigation(bool justNavigated = false)
        {
            if (justNavigated)
                return false;

            return IsWindowNavigationItem(GetFocusedElement()) && !IsNavigationViewFocusChangeInProgress();
        }

        private bool TryCloseFlyoutSubmenu(MenuItem menuItem)
        {
            MenuItem? parentMenuItem = WPFUtils.GetParentMenuItem(menuItem);
            if (parentMenuItem is null)
                return false;

            parentMenuItem.IsSubmenuOpen = false;
            FocusFlyoutMenuItem(parentMenuItem);
            return true;
        }

        private void ContentDialogOpened(ContentDialog contentDialog)
        {
            // Defer: ContentDialog children are not in the visual tree yet when this
            // event fires (OnLayoutUpdated calls us synchronously mid-layout-pass).
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                Control? control = GetTopLeftNavigableControl();
                if (control is not null)
                    Focus(control);
            }, DispatcherPriority.Loaded);
        }

        private void WindowGotFocus(object? sender, RoutedEventArgs? e, FocusSource focusSource)
        {
            // already has focus
            if (_focused.TryGetValue(windowName, out bool isFocused) && isFocused)
                return;

            // check focus based on our scenarios
            bool gamepadFocused = false;

            WindowState windowState = gamepadWindow.WindowState;
            if (windowState != WindowState.Minimized)
            {
                switch (focusSource)
                {
                    case FocusSource.Visibility:
                        gamepadFocused = gamepadWindow.IsHitTestVisible && gamepadWindow.IsVisible;

                        // only send gamepad inputs to quicktools if it's on main screen
                        // this is important for dual screen devices
                        if (gamepadWindow is OverlayQuickTools)
                            gamepadFocused &= gamepadWindow.IsPrimary;
                        break;
                    case FocusSource.Activate:
                        gamepadFocused = gamepadWindow.IsActive;
                        break;
                    case FocusSource.Focus:
                        gamepadFocused = gamepadWindow.IsFocused;
                        break;
                }
            }

            // set focus
            _focused[windowName] = gamepadFocused;

            // raise event
            if (_focused[windowName])
            {
                LogManager.LogTrace("GotFocus: {0}", windowName);
                GotFocus?.Invoke(windowName);

                foreach (string window in _focused.Keys)
                {
                    if (window.Equals(windowName))
                        continue;

                    if (_focused.TryGetValue(window, out isFocused) && !isFocused)
                        continue;

                    // remove focus
                    _focused[window] = false;

                    // raise event
                    LostFocus?.Invoke(window);
                }
            }

            if (gamepadPage is not null && gamepadPage.IsLoaded)
                ContentRendered(null, null);
        }

        private void WindowLostFocus(object? sender, RoutedEventArgs? e, FocusSource focusSource)
        {
            // doesn't have focus
            if (_focused.TryGetValue(windowName, out bool isFocused) && !isFocused)
                return;

            // check if sender is part of current window
            if (e is not null && e.OriginalSource is not null)
            {
                Window yourParentWindow = Window.GetWindow((DependencyObject)e.OriginalSource);

                // sender is part of parent window, return
                if (yourParentWindow == gamepadWindow)
                    return;
            }

            // unset focus
            _focused[windowName] = false;

            // halt timer
            gamepadTimer.Stop();

            // raise event
            LogManager.LogTrace("LostFocus: {0}", windowName);
            LostFocus?.Invoke(windowName);

            foreach (string window in _focused.Keys)
            {
                if (window.Equals(windowName))
                    continue;

                GamepadWindow gamepadWindow;
                switch (window)
                {
                    default:
                    case "Main":
                        gamepadWindow = MainWindow.GetCurrent();
                        break;
                    case "QuickTools":
                        gamepadWindow = OverlayQuickTools.GetCurrent();
                        break;
                }

                if (gamepadWindow.Visibility != Visibility.Visible)
                    continue;

                if (gamepadWindow.WindowState == WindowState.Minimized)
                    continue;

                if (!gamepadWindow.IsActive && gamepadWindow is MainWindow)
                    continue;

                if (!gamepadWindow.IsPrimary)
                    continue;

                if (_focused.TryGetValue(window, out isFocused) && isFocused)
                    continue;

                // set focus
                _focused[window] = true;

                // raise event
                if (_focused[window])
                    GotFocus?.Invoke(window);
            }

            // hide tooltip
            tooltip.PlacementTarget = null;
            tooltip.IsOpen = false;
        }

        private void ContentNavigated(object sender, NavigationEventArgs e)
        {
            lock (_rendering)
            {
                // halt timer
                gamepadTimer.Stop();

                // set state(s)
                _rendered = false;

                // store current Frame and listen to render events
                if (gamepadPage != (Page)gamepadFrame.Content)
                {
                    // Unsubscribe from the previous page's library events
                    if (gamepadPage is LibraryPage && gamepadPage.DataContext is LibraryPageViewModel prevLibraryVm)
                    {
                        prevLibraryVm.CollectionOpened -= LibraryPageViewModel_CollectionOpened;
                        prevLibraryVm.NavigatedBackToCollections -= LibraryPageViewModel_NavigatedBackToCollections;
                    }

                    Page newPage = (Page)gamepadFrame.Content;

                    gamepadFrame = (Frame)sender;
                    gamepadFrame.ContentRendered += ContentRendering;

                    // store current Page
                    gamepadPage = newPage;

                    // reset page-scoped navigation view state
                    pageNavigationView = null;
                    _justNavigatedToNewPage = true;

                    // Subscribe to collection-open events so focus moves to the first profile card
                    if (gamepadPage is LibraryPage && gamepadPage.DataContext is LibraryPageViewModel newLibraryVm)
                    {
                        newLibraryVm.CollectionOpened += LibraryPageViewModel_CollectionOpened;
                        newLibraryVm.NavigatedBackToCollections += LibraryPageViewModel_NavigatedBackToCollections;
                    }
                }
                else
                {
                    // page already rendered
                    ContentRendered(null, null);
                }
            }
        }

        private void LibraryPageViewModel_CollectionOpened()
        {
            if (gamepadPage is null)
                return;

            Page pageRef = gamepadPage;
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                pageRef.UpdateLayout();
                TryFocusPageContent(pageRef);
            }, DispatcherPriority.Loaded);
        }

        private void LibraryPageViewModel_NavigatedBackToCollections()
        {
            if (gamepadPage is null)
                return;

            Page pageRef = gamepadPage;
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                pageRef.UpdateLayout();
                TryFocusPageContent(pageRef);
            }, DispatcherPriority.Loaded);
        }

        private void ContentRendering(object? sender, EventArgs e)
        {
            gamepadTimer.Stop();
            gamepadTimer.Start();
        }

        private void ContentRendered(object? sender, System.Timers.ElapsedEventArgs? e)
        {
            // stop listening for render events
            gamepadFrame.ContentRendered -= ContentRendering;

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                // Consume the navigation flag before any early return so it is never stale.
                bool justNavigated = _justNavigatedToNewPage;
                _justNavigatedToNewPage = false;

                // refresh page-scoped NavigationView if any
                // (e.g. LayoutPage has its own NavigationView hosted inside the Page)
                if (gamepadPage is not null)
                {
                    pageNavigationView = FindActivePageNavigationView(gamepadPage);

                    // Track the Frame inside the embedded NavigationView (if any) so we
                    // can defer focus until the inner sub-page has actually rendered.
                    Frame? prevEmbeddedNavFrame = _embeddedNavFrame;
                    _embeddedNavFrame = FindEmbeddedNavFrame(pageNavigationView);

                    if (prevEmbeddedNavFrame != null && prevEmbeddedNavFrame != _embeddedNavFrame)
                        prevEmbeddedNavFrame.Navigated -= EmbeddedNavFrame_Navigated;
                }

                // store selected navigation items (window and page)
                _lastWindowNavigationItem = GetCurrentNavigationViewItem(windowNavigationView);

                if (gamepadPage is not null)
                {
                    PageFocusState state = GetPageFocusState(gamepadPage);

                    state.LastEmbeddedNavigationItem = GetCurrentNavigationViewItem(pageNavigationView);

                    if (!TryRestoreLastFocusedControl(gamepadPage))
                    {
                        if (!ShouldKeepFocusOnWindowNavigation(justNavigated))
                        {
                            NavigationView? activeNavView = pageNavigationView ?? windowNavigationView;
                            DependencyObject? contentRoot = GetNavigationViewContentRoot(activeNavView, gamepadPage);
                            Control? control = ResolveStoredContentControl(gamepadPage, activeNavView)
                                ?? GetTopLeftFocusableContentControl(contentRoot);

                            if (control is not null)
                            {
                                Focus(control);
                            }
                            else if (_embeddedNavFrame is not null)
                            {
                                // control is null: the embedded nav's inner Frame either has no content yet
                                // or its content has no focusable elements yet. Subscribe once so we focus
                                // as soon as the inner sub-page navigation completes.
                                _embeddedNavFrame.Navigated -= EmbeddedNavFrame_Navigated;
                                _embeddedNavFrame.Navigated += EmbeddedNavFrame_Navigated;
                            }
                        }
                    }

                    ResetBackNavigationAtHomePage();
                }

                // set rendering state
                _rendered = true;
            });
        }

        // Called when the Frame inside an embedded NavigationView completes a navigation.
        // Navigated fires reliably for every Navigate() call, including cached page instances.
        private void EmbeddedNavFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (sender is Frame frame)
                frame.Navigated -= EmbeddedNavFrame_Navigated;

            LogManager.LogTrace("[UIGamepad] EmbeddedNavFrame_Navigated fired: content={0}, hasFocus={1}, page={2}",
                e.Content?.GetType().Name ?? "null",
                HasFocus(),
                gamepadPage?.GetType().Name ?? "null");

            if (gamepadPage is null || !HasFocus())
                return;

            Page pageRef = gamepadPage;
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(gamepadPage, pageRef))
                    return;

                pageRef.UpdateLayout();

                NavigationView? activeNavView = pageNavigationView ?? windowNavigationView;
                Control? control = ResolveStoredContentControl(pageRef, activeNavView)
                    ?? GetTopLeftFocusableContentControl(GetNavigationViewContentRoot(activeNavView, pageRef));

                LogManager.LogTrace("[UIGamepad] EmbeddedNavFrame_Navigated deferred: control={0}", control?.GetType().Name ?? "null");

                if (control is not null)
                    Focus(control);
            }, DispatcherPriority.Loaded);
        }

        private Control? forcedFocus;
        private Control? parentFocus;

        private void TooltipTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                tooltip.PlacementTarget = null;
                tooltip.IsOpen = false;
            });
        }

        public void Focus(Control? control, Control? parent = null, bool force = false)
        {
            if (control is null || IsTransientContainerControl(control) || !HasFocus())
                return;

            // prevent keyboard focus from overlapping with our own tooltip logic
            ToolTipService.SetShowsToolTipOnKeyboardFocus(control, false);

            // manage tooltip
            if (tooltip.PlacementTarget != control)
            {
                // hide tooltip
                tooltip.IsOpen = false;

                // change target
                tooltip.PlacementTarget = control;

                // (re)start timer
                tooltipTimer.Stop();
                tooltipTimer.Start();
            }

            if (control.ToolTip is not null)
            {
                tooltip.Content = control.ToolTip.ToString();
                tooltip.IsOpen = true;
            }

            // set tooltip initial delay
            string controlType = control.GetType().Name;
            switch (controlType)
            {
                case "ContentDialog":
                    return;
            }

            if (force)
            {
                forcedFocus = control;
                parentFocus = parent;
            }
            else
            {
                forcedFocus = null;
                parentFocus = null;
            }

            // set focus to control
            control.Focus();
            control.BringIntoView();
            Keyboard.Focus(control);
            FocusManager.SetFocusedElement(gamepadWindow, control);
            gamepadWindow.SetFocusedElement(control);
        }

        public Control? GetFocusedElement()
        {
            // When a flyout is open, return the tracked popup item directly.
            // Popup elements have no common visual ancestor with gamepadWindow,
            // so the FindCommonAncestor check below would incorrectly reset focus.
            if (HasFlyoutOpen && focusedFlyoutItem is not null)
                return focusedFlyoutItem;

            IInputElement FocusedElement = forcedFocus is not null ? forcedFocus : gamepadWindow.GetFocusedElement();

            DependencyObject commonAncestor = VisualTreeHelperExtensions.FindCommonAncestor((DependencyObject)FocusedElement, gamepadWindow);
            if (commonAncestor is null && forcedFocus is null)
            {
                FocusManager.SetFocusedElement(gamepadWindow, GetTopLeftNavigableControl());
                FocusedElement = FocusManager.GetFocusedElement(gamepadWindow);
            }

            if (FocusedElement is null)
                FocusedElement = gamepadWindow;

            if (FocusedElement.Focusable && FocusedElement is Control)
            {
                Control controlFocused = (Control)FocusedElement;

                string keyboardType = controlFocused.GetType().Name;

                switch (keyboardType)
                {
                    case "MainWindow":
                    case "OverlayQuickTools":
                    case "ScrollViewer":
                    case "TouchScrollViewer":
                    case "SplitView":
                        {
                            // a new page opened
                            if (_lastWindowNavigationItem is not null)
                                controlFocused = _lastWindowNavigationItem;
                        }
                        break;

                    case "NavigationViewItem":
                        break;

                    default:
                        break;
                }

                if (controlFocused is not null)
                {
                    // pick the last known Control
                    return controlFocused;
                }
                else
                {
                    // pick nearest navigation element
                    return WPFUtils.GetTopLeftControl<NavigationViewItem>(gamepadWindow.controlElements);
                }
            }
            else
            {
                // pick nearest navigation element
                return WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements);
            }

            return null;
        }

        private void StoreFocusedControl(Page page, Control control)
        {
            if (IsTransientContainerControl(control))
            {
                LogManager.LogTrace("[UIGamepad] StoreFocusedControl: skipped transient {0}", control.GetType().Name);
                return;
            }

            if (control is NavigationViewItem)
            {
                LogManager.LogTrace("[UIGamepad] StoreFocusedControl: skipped NavigationViewItem");
                return;
            }

            // Normalize ComboBoxItem to its parent ComboBox — items are transient (hidden when closed).
            if (control is ComboBoxItem comboBoxItem)
            {
                if (ItemsControl.ItemsControlFromItemContainer(comboBoxItem) is ComboBox parentComboBox)
                {
                    LogManager.LogTrace("[UIGamepad] StoreFocusedControl: normalized ComboBoxItem -> ComboBox");
                    control = parentComboBox;
                }
                else
                {
                    LogManager.LogTrace("[UIGamepad] StoreFocusedControl: skipped orphan ComboBoxItem");
                    return;
                }
            }

            PageFocusState state = GetPageFocusState(page);
            state.LastContentControl = control;

            string? viewKey = GetActivePageViewKey(page);
            if (!string.IsNullOrWhiteSpace(viewKey))
                state.LastContentControlsByView[viewKey] = control;

            if (page.DataContext is LibraryPageViewModel libraryPageViewModel && control.DataContext is CollectionGroupViewModel collectionGroup)
                libraryPageViewModel.RememberCollectionsOverviewItem(collectionGroup);

            if (TryGetProfileGuid(control, out Guid profileGuid))
            {
                state.LastContentProfileGuid = profileGuid;
                string? scopeKey = GetPageFocusScopeKey(page);
                if (!string.IsNullOrWhiteSpace(scopeKey))
                    state.LastProfileGuidsByScope[scopeKey] = profileGuid;
            }

            if (control is NavigationViewItem navigationViewItem)
            {
                NavigationView? navigationView = WPFUtils.FindParent<NavigationView>(navigationViewItem);
                if (navigationView is not null && navigationView != windowNavigationView)
                    state.LastEmbeddedNavigationItem = navigationViewItem;
            }

            LogManager.LogTrace("[UIGamepad] StoreFocusedControl: stored {0} (viewKey={1})",
                control.GetType().Name, viewKey ?? "null");
        }

        public void TrackFocusedControl(Control control)
        {
            if (Window.GetWindow(control) != gamepadWindow)
                return;

            if (IsTransientContainerControl(control))
                return;

            gamepadWindow.SetFocusedElement(control);

            if (IsWindowNavigationItem(control))
            {
                _lastWindowNavigationItem = control;

                if (control is NavigationViewItem focusedNavigationViewItem && !IsNavigationViewFocusChangeInProgress())
                    NavigateFromFocusedNavigationViewItem(focusedNavigationViewItem);

                return;
            }

            if (gamepadPage is null || gamepadWindow.currentDialog is not null || HasFlyoutOpen)
                return;

            if (control is NavigationViewItem navigationViewItem)
            {
                NavigationView? navigationView = FindOwningNavigationView(navigationViewItem);
                if (navigationView is not null && navigationView != windowNavigationView)
                {
                    GetPageFocusState(gamepadPage).LastEmbeddedNavigationItem = navigationViewItem;

                    if (!IsNavigationViewFocusChangeInProgress())
                        NavigateFromFocusedNavigationViewItem(navigationViewItem);
                }

                return;
            }

            // Normalize for deduplication: ComboBoxItem -> parent ComboBox (mirrors StoreFocusedControl).
            Control storeCandidate = control is ComboBoxItem cbi
                && ItemsControl.ItemsControlFromItemContainer(cbi) is ComboBox cb ? cb : control;

            // Focus() issues multiple WPF focus calls (control.Focus, Keyboard.Focus, FocusManager, SetFocusedElement).
            // Each one re-fires GotFocusEvent and lands here. Skip the write when nothing has actually changed.
            PageFocusState currentState = GetPageFocusState(gamepadPage);
            if (ReferenceEquals(currentState.LastContentControl, storeCandidate))
                return;

            StoreFocusedControl(gamepadPage, control);
        }

        private bool IsUsableStoredControl(Control? control)
        {
            return control is not null
                && !IsTransientContainerControl(control)
                && control.IsLoaded
                && control.IsEnabled
                && control.IsVisible
                && (control.Parent is not null || VisualTreeHelper.GetParent(control) is not null)
                && Window.GetWindow(control) == gamepadWindow;
        }

        private static bool TryGetProfileGuid(Control? control, out Guid profileGuid)
        {
            profileGuid = Guid.Empty;

            ProfileViewModel? profileViewModel = control?.Tag as ProfileViewModel
                ?? control?.DataContext as ProfileViewModel;

            if (profileViewModel?.Profile is null)
                return false;

            profileGuid = profileViewModel.Profile.Guid;
            return true;
        }

        private static bool IsPreferredLibraryContentControl(Control? control)
        {
            return TryGetProfileGuid(control, out _);
        }

        private static bool IsPreferredLibraryContentControlForScope(Control? control, string? scopeKey)
        {
            return TryGetProfileGuid(control, out _);
        }

        private bool TryFocusLibraryBackTarget(Control focusedElement)
        {
            if (gamepadPage is not LibraryPage libraryPage || gamepadPage.DataContext is not LibraryPageViewModel libraryPageViewModel)
                return false;

            if (!IsPreferredLibraryContentControl(focusedElement))
                return false;

            if (libraryPageViewModel.CanGoBack)
            {
                // TryGoBack fires NavigatedBackToCollections, which defers focus via the event handler
                if (!libraryPage.TryGoBack())
                    return false;

                return true;
            }

            return TryFocusEmbeddedNavigationAnchor(gamepadPage);
        }

        private Control? FindProfileControl(Guid profileGuid, DependencyObject? searchRoot = null)
        {
            searchRoot ??= gamepadPage is not null ? gamepadPage : gamepadWindow;

            return WPFUtils.FindVisualChildren<Button>(searchRoot)
                .FirstOrDefault(button => button.IsEnabled
                    && button.IsVisible
                    && TryGetProfileGuid(button, out Guid guid)
                    && guid == profileGuid);
        }

        public bool TryGoBack()
        {
            return TryNavigateBackInHistory();
        }

        public void TrySelect()
        {
            UIHelper.TryInvoke(() =>
            {
                Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());
                if (focusedElement is null || !focusedElement.IsVisible || !focusedElement.IsEnabled)
                    return;
                ExecuteSelect(focusedElement);
            });
        }

        public void TryMore()
        {
            UIHelper.TryInvoke(() =>
            {
                Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());
                if (focusedElement is null || !focusedElement.IsVisible || !focusedElement.IsEnabled)
                    return;
                ExecuteMore(focusedElement);
            });
        }

        public void TryToggle()
        {
            UIHelper.TryInvoke(() =>
            {
                Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());
                if (focusedElement is null || !focusedElement.IsVisible || !focusedElement.IsEnabled)
                    return;
                ExecuteToggle(focusedElement);
            });
        }

        public void TryLike()
        {
            UIHelper.TryInvoke(() =>
            {
                Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());
                if (focusedElement is null || !focusedElement.IsVisible || !focusedElement.IsEnabled)
                    return;
                ExecuteLike(focusedElement);
            });
        }

        private void ExecuteSelect(Control focusedElement)
        {
            if (focusedElement is Button button && focusedElement is not DropDownButton)
            {
                Focus(button);

                if (focusedElement.Tag?.Equals("GoBack") == true && gamepadFrame.CanGoBack)
                    gamepadFrame.GoBack();

                button.Command?.Execute(button.CommandParameter);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
            else if (focusedElement is RepeatButton repeatButton)
            {
                Focus(repeatButton);
                repeatButton.Command?.Execute(repeatButton.CommandParameter);
                repeatButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
            else if (focusedElement is ToggleButton toggleButton)
            {
                Focus(toggleButton);

                if (toggleButton.Name.Equals("ExpanderHeader"))
                {
                    Expander? Expander = WPFUtils.FindParent<Expander>(toggleButton);
                    Expander?.IsExpanded = !Expander.IsExpanded;
                }
                else if (toggleButton is RadioButton radioButton)
                {
                    toggleButton.IsChecked = !toggleButton.IsChecked;
                }
                else
                {
                    switch (toggleButton.Tag)
                    {
                        default:
                            if (BindingOperations.GetBindingExpression(toggleButton, ToggleButton.IsCheckedProperty)?.ParentBinding?.Mode != BindingMode.OneWay)
                                toggleButton.IsChecked = !toggleButton.IsChecked;
                            break;
                        case "Hotkey":
                            break;
                    }
                }

                toggleButton.Command?.Execute(toggleButton.CommandParameter);
            }
            else if (focusedElement is SettingsCard settingsCard)
            {
                if (settingsCard.IsClickEnabled)
                {
                    Focus(settingsCard);
                    settingsCard.Command?.Execute(settingsCard.CommandParameter);
                    settingsCard.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                    switch (focusedElement.Tag)
                    {
                        case "Navigation":
                            break;
                        case "GoBack":
                            if (gamepadFrame.CanGoBack)
                                gamepadFrame.GoBack();
                            break;
                    }
                }
            }
            else if (focusedElement is ToggleSwitch toggleSwitch)
            {
                toggleSwitch.IsOn = !toggleSwitch.IsOn;
            }
            else if (focusedElement is RadioButton radioButton2)
            {
                radioButton2.IsChecked = !radioButton2.IsChecked;
                radioButton2.Command?.Execute(radioButton2.CommandParameter);
            }
            else if (focusedElement is HyperlinkButton hyperlinkButton)
            {
                if (hyperlinkButton.NavigateUri is not null)
                    Process.Start(new ProcessStartInfo(hyperlinkButton.NavigateUri.AbsoluteUri) { UseShellExecute = true });

                hyperlinkButton.Command?.Execute(hyperlinkButton.CommandParameter);
            }
            else if (focusedElement is CheckBox checkBox)
            {
                checkBox.IsChecked = !checkBox.IsChecked;
                checkBox.Command?.Execute(checkBox.CommandParameter);
            }
            else if (focusedElement is NavigationViewItem navigationViewItem)
            {
                if (TryEnterContentFromNavigationItem(navigationViewItem))
                    UISounds.PlayOggFile(UISounds.Expanded);
            }
            else if (focusedElement is ComboBox comboBox)
            {
                comboBox.DropDownClosed += (sender, e) => Focus(comboBox, null, true);
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;

                Control? item = null;
                int idx = comboBox.SelectedIndex;
                if (idx != -1)
                {
                    item = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);
                }
                else if (comboBox.IsDropDownOpen)
                {
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        if (comboBox.ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxItem ci && ci.IsEnabled && ci.IsVisible)
                        {
                            item = ci;
                            break;
                        }
                    }
                }

                Focus(item ?? focusedElement, comboBox, true);
            }
            else if (focusedElement is ComboBoxItem comboBoxItem)
            {
                if (ItemsControl.ItemsControlFromItemContainer(focusedElement) is ComboBox parentComboBox && parentComboBox.IsDropDownOpen)
                {
                    int idx = parentComboBox.Items.IndexOf(comboBoxItem);
                    if (idx == -1) idx = parentComboBox.Items.IndexOf(comboBoxItem.Content);
                    parentComboBox.SelectedIndex = idx;
                    parentComboBox.IsDropDownOpen = false;
                    Focus(parentComboBox);
                }
            }
            else if (focusedElement is ListBoxItem listBoxItem)
            {
                ListBox? listBox = (ListBox)ItemsControl.ItemsControlFromItemContainer(focusedElement);
                if (listBox is not null)
                {
                    Control? below = WPFUtils.GetClosestControl<Control>(listBox, gamepadWindow.controlElements, WPFUtils.Direction.Down);
                    if (below is not null) Focus(below);
                }
            }
            else if (focusedElement is DropDownButton dropDownButton)
            {
                var flyout = dropDownButton.Flyout;
                if (flyout is not null)
                {
                    EventHandler<object>? closedHandler = null;
                    closedHandler = (s, e) =>
                    {
                        flyout.Closed -= closedHandler;
                        HasFlyoutOpen = false;
                        gamepadWindow.currentFlyoutButton = null;
                        flyoutMenuItems.Clear();
                        focusedFlyoutItem = null;
                        UIHelper.TryInvoke(() =>
                        {
                            if (_focused[windowName] && gamepadWindow.currentDialog is null)
                                Focus(dropDownButton);
                        });
                    };
                    flyout.Closed += closedHandler;

                    EventHandler<object>? openedHandler = null;
                    openedHandler = (s, e) =>
                    {
                        flyout.Opened -= openedHandler;
                        HasFlyoutOpen = true;
                        gamepadWindow.currentFlyoutButton = dropDownButton;
                        UIHelper.TryInvoke(() =>
                        {
                            flyoutMenuItems.Clear();
                            if (dropDownButton.Flyout is MenuFlyout menuFlyout)
                                flyoutMenuItems = WPFUtils.GetDirectMenuItems(menuFlyout);

                            var firstItem = flyoutMenuItems.FirstOrDefault(m => m.IsEnabled && m.IsVisible);
                            if (firstItem is not null)
                                FocusFlyoutMenuItem(firstItem);
                        });
                    };
                    flyout.Opened += openedHandler;
                    flyout.ShowAt(dropDownButton);
                }
            }
            else if (focusedElement is MenuItem menuItem && HasFlyoutOpen)
            {
                if (!TryOpenFlyoutSubmenu(menuItem))
                {
                    if (menuItem.Command?.CanExecute(menuItem.CommandParameter) == true)
                        menuItem.Command.Execute(menuItem.CommandParameter);

                    if (HasFlyoutOpen)
                        gamepadWindow.currentFlyoutButton?.Flyout?.Hide();
                }
            }
        }

        private void ExecuteMore(Control focusedElement)
        {
            if (focusedElement is Button && focusedElement.Tag is ProfileViewModel profileViewModelMore)
                profileViewModelMore.OpenLayout?.Execute(null);
        }

        private void ExecuteToggle(Control focusedElement)
        {
            if (focusedElement is Button)
            {
                if (focusedElement.Tag is ProfileViewModel profileViewModelToggle)
                    profileViewModelToggle.ToggleProcessCommand.Execute(null);
                else
                {
                    // To get the first RadioButton in the list, if any
                    RadioButton? firstRadioButton = WPFUtils.FindChildren(focusedElement).FirstOrDefault(c => c is RadioButton) as RadioButton;
                    firstRadioButton?.IsChecked = true;
                }
            }
        }

        private void ExecuteLike(Control focusedElement)
        {
            if (focusedElement is Button && focusedElement.Tag is ProfileViewModel profileViewModelLike)
            {
                Profile profile = profileViewModelLike.Profile;
                pendingFocusRestoreProfileGuid = profile.Guid;
                profile.IsLiked = !profile.IsLiked;
                ManagerFactory.profileManager.UpdateOrCreateProfile(profile, UpdateSource.Background);
            }
        }

        private bool TryNavigateBackInHistory()
        {
            if (gamepadWindow is MainWindow mainWindow && mainWindow.TryGoBack())
                return true;

            if (!gamepadFrame.CanGoBack || IsCurrentHomePage())
                return false;

            gamepadFrame.GoBack();
            return true;
        }

        private void FocusWindowNavigationAnchor(Control? control)
        {
            if (control is null)
                return;

            Focus(control);
        }

        private bool IsWindowNavigationItem(Control? control)
        {
            return control is NavigationViewItem navigationViewItem
                && windowNavigationView is not null
                && WPFUtils.FindParent<NavigationView>(navigationViewItem) == windowNavigationView;
        }

        private static List<Control> GetNavigationItems(NavigationView navView)
        {
            if (navView is null)
                return [];

            // Prefer logical order from MenuItems/FooterMenuItems (stable and matches UI order).
            // Fallback to visual tree enumeration if containers are not NavigationViewItem instances.
            var ordered = new List<Control>();

            try
            {
                if (navView.MenuItems is not null)
                {
                    foreach (var mi in navView.MenuItems)
                    {
                        if (mi is NavigationViewItem nvi && nvi.IsEnabled && nvi.IsVisible)
                            ordered.Add(nvi);
                    }
                }

                if (navView.FooterMenuItems is not null)
                {
                    foreach (var mi in navView.FooterMenuItems)
                    {
                        if (mi is NavigationViewItem nvi && nvi.IsEnabled && nvi.IsVisible)
                            ordered.Add(nvi);
                    }
                }
            }
            catch
            {
                // Some NavigationView implementations may throw or not expose these collections.
                // We'll fall back to visual enumeration below.
                ordered.Clear();
            }

            if (ordered.Count > 0)
                return ordered;

            // Fallback: collect all NavigationViewItem instances within this NavigationView.
            // This ensures navigation does not "jump" across nested NavigationViews.
            return WPFUtils.FindVisualChildren<NavigationViewItem>(navView)
                           .Where(i => i is Control c && c.IsEnabled && c.IsVisible)
                           .Cast<Control>()
                           .ToList();
        }

        private static bool IsTopPaneNavigationView(NavigationView navView)
        {
            // For our purposes, only a fully left-displayed pane should be treated as a navigation "sidebar".
            return navView is not null && navView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top;
        }

        private static WPFUtils.Direction GetDirectionTowardsPane(NavigationView navView)
        {
            // Used when we want to move focus "back" to the pane items from content.
            // Left-pane -> Left, Top-pane -> Up.
            return navView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top ? WPFUtils.Direction.Up : WPFUtils.Direction.Left;
        }

        private static NavigationViewItem? GetSelectedNavigationViewItem(NavigationView navView)
        {
            if (navView is null)
                return null;

            // Best effort: SelectedItem can be a container or a data item.
            if (navView.SelectedItem is NavigationViewItem selected)
                return selected;

            // Fallback: find the visual container that is marked selected.
            return WPFUtils.FindVisualChildren<NavigationViewItem>(navView).FirstOrDefault(i => i.IsSelected);
        }

        // declare a DateTime variable to store the last time the function was called
        private DateTime lastCallTime;

        // declare a DateTime variable to store the last time the button state changed
        private DateTime lastChangeTime;

        private void SettingsManager_SettingValueChanged(string? name, object? value, bool temporary, bool initializing)
        {
            if (name == "LayoutMode")
                _layoutModeIsDesktop = (LayoutModes)ManagerFactory.settingsManager.GetInt("LayoutMode") == LayoutModes.Desktop;
        }

        private void InputsUpdated(ControllerState controllerState, bool IsMapped)
        {
            // skip if page hasn't yet rendered
            if (!_rendered)
                return;

            // skip if inputs were remapped
            if (IsMapped)
                return;

            // Fast-path: the built-in Desktop layout maps every navigational button to a
            // keyboard/mouse action — bail out entirely rather than checking each input.
            if (_layoutModeIsDesktop)
                return;

            // skip if page doesn't have focus
            if (!_focused.TryGetValue(windowName, out bool isFocused) || !isFocused)
                return;

            // stop gamepad navigation when InputsManager is listening
            if (InputsManager.IsListening)
                return;

            // get the current time
            DateTime currentTime = DateTime.Now;

            // check if the button state is equal to the previous button state
            if (controllerState.ButtonState.Equals(prevButtonState))
            {
                if (!controllerState.ButtonState.IsEmpty())
                {
                    // check if the button state has been the same for at least 600ms
                    if ((currentTime - lastChangeTime).TotalMilliseconds >= 600)
                    {
                        // check if the function has been called within the last 25ms
                        if ((currentTime - lastCallTime).TotalMilliseconds >= 25)
                        {
                            // update the last call time
                            lastCallTime = currentTime;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                // update the last change time and the last call time
                lastChangeTime = currentTime;
                lastCallTime = currentTime;
                ButtonState.Overwrite(controllerState.ButtonState, prevButtonState);

                // If a suppress was requested (e.g. window just gained focus mid-press),
                // record the state as seen but do not act on this transition.
                if (_suppressNextInput)
                {
                    _suppressNextInput = false;
                    return;
                }
            }

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                try
                {
                    // clear any mouse/touch hover state so gamepad navigation is visually clean
                    gamepadWindow.ClearMouseHover();

                    // get current focused element
                    Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());

                    // If the focused control is gone (null), hidden/collapsed, or disabled,
                    // redirect focus to the nearest available control so gamepad navigation
                    // is not silently swallowed.
                    if (focusedElement is null || !focusedElement.IsVisible || !focusedElement.IsEnabled)
                    {
                        Control? fallback = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements);
                        if (fallback is not null)
                            Focus(fallback);
                        return;
                    }

                    string elementType = focusedElement.GetType().Name;

                    // set direction
                    WPFUtils.Direction direction = WPFUtils.Direction.None;

                    if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B1)
                        && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.B1))
                    {
                        ExecuteSelect(focusedElement);
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B2)
                             && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.B2))
                    {
                        if (HasFlyoutOpen && focusedElement is MenuItem focusedMenuItem && TryCloseFlyoutSubmenu(focusedMenuItem))
                            return;

                        // close flyout, if any
                        if (HasFlyoutOpen && gamepadWindow.currentFlyoutButton?.Flyout is { } openFlyout)
                        {
                            openFlyout.Hide();
                            return;
                        }

                        // hide dialog, if any
                        if (gamepadWindow.currentDialog is not null)
                        {
                            gamepadWindow.currentDialog.Hide();
                            return;
                        }

                        // If we're currently within a page-scoped (nested) NavigationView, pressing B should:
                        //  1) first bring focus back to the page NavigationViewItem
                        //  2) only on a subsequent press, leave the page
                        // This prevents accidental "page exit" when the user intended to go back to the page navigation pane.
                        if (focusedElement is Control focusedControl)
                        {
                            if (TryFocusLibraryBackTarget(focusedControl))
                                return;

                            NavigationView? focusedNavView = WPFUtils.FindParent<NavigationView>(focusedControl);
                            if (focusedNavView is not null && focusedNavView != windowNavigationView)
                            {
                                // First press: move focus to the (closest/selected) NavigationViewItem
                                if (focusedElement is not NavigationViewItem)
                                {
                                    List<Control> pageNavItems = GetNavigationItems(focusedNavView);
                                    Control? navItem = null;

                                    PageFocusState? state = TryGetCurrentPageFocusState();
                                    NavigationViewItem? prevPageNavigation = state?.LastEmbeddedNavigationItem as NavigationViewItem;
                                    NavigationView? parent = prevPageNavigation is null ? null : WPFUtils.FindParent<NavigationView>(prevPageNavigation);
                                    if (prevPageNavigation is not null && parent is not null && parent == focusedNavView)
                                        navItem = prevPageNavigation;

                                    navItem ??= pageNavItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.IsSelected) as Control;
                                    navItem ??= focusedNavView.SelectedItem as NavigationViewItem;

                                    if (navItem is null && pageNavItems.Count > 0)
                                    {
                                        // Find the closest NavigationViewItem towards the pane (Left or Top)
                                        Control? closest = WPFUtils.GetClosestControl<NavigationViewItem>(focusedControl, pageNavItems, GetDirectionTowardsPane(focusedNavView));

                                        // GetClosestControl returns the source if none found; guard against that.
                                        if (closest is NavigationViewItem)
                                            navItem = closest;
                                        else
                                            navItem = pageNavItems.FirstOrDefault();
                                    }

                                    if (navItem is not null)
                                    {
                                        state?.LastEmbeddedNavigationItem = navItem;
                                        Focus(navItem);
                                        return;
                                    }
                                }
                                else
                                {
                                    // Second press (already on a page NavigationViewItem): leave the page if possible.
                                    if (!IsQuicktools)
                                    {
                                        if (TryNavigateBackInHistory())
                                            return;

                                        if (TryFocusWindowNavigationAnchor())
                                            return;
                                    }
                                }
                            }
                        }

                        // lazy
                        // todo: implement proper RoutedEvent call
                        switch (elementType)
                        {
                            default:
                                {
                                    if (gamepadWindow.currentDialog is not null && gamepadPage is not null)
                                    {
                                        Control? control = ResolveStoredContentControl(gamepadPage, pageNavigationView ?? windowNavigationView)
                                            ?? ResolveStoredContentControl(gamepadPage, windowNavigationView);
                                        if (control is null)
                                            break;

                                        Focus(control);
                                        return;
                                    }
                                }
                                break;

                            case "ComboBox":
                                {
                                    ComboBox comboBox = (ComboBox)focusedElement;
                                    switch (comboBox.IsDropDownOpen)
                                    {
                                        case true:
                                            {
                                                comboBox.IsDropDownOpen = false;
                                                return;
                                            }
                                            break;
                                    }
                                }
                                break;

                            case "ComboBoxItem":
                                {
                                    if (ItemsControl.ItemsControlFromItemContainer(focusedElement) is ComboBox comboBox)
                                    {
                                        comboBox.IsDropDownOpen = false;
                                        return;
                                    }
                                }
                                break;

                            case "NavigationViewItem":
                                {
                                    if (gamepadPage is not null && focusedElement is NavigationViewItem navigationViewItem && IsEmbeddedNavigationItem(navigationViewItem))
                                    {
                                        if (TryFocusEmbeddedNavigationAnchor(gamepadPage))
                                            return;
                                    }

                                    if (gamepadWindow is OverlayQuickTools overlayQuickTools)
                                    {
                                        overlayQuickTools.ToggleVisibility();
                                        return;
                                    }
                                }
                                break;
                        }

                        // go back to previous page using navigation history
                        if (TryNavigateBackInHistory())
                            return;

                        TryFocusWindowNavigationAnchor();
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B3)
                             && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.B3))
                    {
                        ExecuteMore(focusedElement);
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B4)
                             && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.B4))
                    {
                        ExecuteToggle(focusedElement);
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.Back)
                             && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.Back))
                    {
                        ExecuteLike(focusedElement);
                    }
                    else if ((controllerState.ButtonState.Buttons.Contains(ButtonFlags.L1) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.L1))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.R1) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.R1))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.L2Full) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.L2))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.R2Full) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.R2)))
                    {
                        if (gamepadWindow.currentDialog is not null)
                            return;

                        bool isWindowScope = (controllerState.ButtonState.Buttons.Contains(ButtonFlags.L1) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.L1))
                                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.R1) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.R1));
                        bool isLeft = (controllerState.ButtonState.Buttons.Contains(ButtonFlags.L1) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.L1))
                                   || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.L2Full) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.L2));
                        NavigationView? targetNavView = isWindowScope ? FindWindowNavigationView() : FindActivePageNavigationView();

                        if (targetNavView is null)
                            return;

                        if (GetNavigableNavigationViewItems(targetNavView).Count == 0)
                            return;

                        if (FocusNextNavigationViewItem(targetNavView, isLeft))
                            return;
                    }
                    else if ((controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadUp) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.DPadUp))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickUp) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftStick))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickUp) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftPad)))
                    {
                        direction = WPFUtils.Direction.Up;
                    }
                    else if ((controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadDown) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.DPadDown))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickDown) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftStick))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickDown) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftPad)))
                    {
                        direction = WPFUtils.Direction.Down;
                    }
                    else if ((controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadLeft) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.DPadLeft))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickLeft) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftStick))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickLeft) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftPad)))
                    {
                        direction = WPFUtils.Direction.Left;
                    }
                    else if ((controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadRight) && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.DPadRight))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickRight) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftStick))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickRight) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.LeftPad)))
                    {
                        direction = WPFUtils.Direction.Right;
                    }
                    else if ((controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightStickUp) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.RightStick))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightPadClickUp) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.RightPad)))
                    {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 50);
                    }
                    else if ((controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightStickDown) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.RightStick))
                          || (controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightPadClickDown) && !ManagerFactory.layoutManager.IsAxisMappedToMouseKeyboard(AxisLayoutFlags.RightPad)))
                    {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 50);
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.Start)
                             && !ManagerFactory.layoutManager.IsButtonMappedToMouseKeyboard(ButtonFlags.Start))
                    {
                        if (gamepadWindow is MainWindow mainWindow)
                        {
                            // skip on top display mode
                            if (mainWindow.navView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
                                return;

                            switch (mainWindow.navView.IsPaneOpen)
                            {
                                case false:
                                    TryFocusWindowNavigationAnchor();
                                    break;
                                case true:
                                    {
                                        Control? control = gamepadPage is null
                                            ? null
                                            : ResolveStoredContentControl(gamepadPage, pageNavigationView ?? windowNavigationView)
                                                ?? ResolveStoredContentControl(gamepadPage, windowNavigationView);
                                        if (control is not null && control is not NavigationViewItem)
                                            Focus(control);
                                        else
                                        {
                                            // get the nearest non-navigation control
                                            focusedElement = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements);
                                            if (focusedElement is not null)
                                                Focus(focusedElement);
                                        }
                                    }
                                    break;
                            }

                            mainWindow.navView.IsPaneOpen = !mainWindow.navView.IsPaneOpen;
                            return;
                        }
                    }

                    // navigation
                    if (direction != WPFUtils.Direction.None)
                    {
                        switch (elementType)
                        {
                            case "NavigationViewItem":
                                {
                                    if (focusedElement is not null)
                                    {
                                        NavigationView? scope = FindOwningNavigationView(focusedElement) ?? windowNavigationView;

                                        if ((direction == WPFUtils.Direction.Left || direction == WPFUtils.Direction.Right)
                                            && FocusNextNavigationViewItem(scope, direction == WPFUtils.Direction.Left))
                                        {
                                            return;
                                        }

                                        List<Control> scopeItems = scope is not null
                                            ? GetNavigableNavigationViewItems(scope).Cast<Control>().ToList()
                                            : gamepadWindow.controlElements;

                                        Control? target = WPFUtils.GetClosestControl<NavigationViewItem>(focusedElement, scopeItems, direction);
                                        if (target is NavigationViewItem targetNavigationItem && TryEnterContentFromNavigationItem(targetNavigationItem))
                                            return;

                                        if (target is not null)
                                            Focus(target);
                                    }
                                }
                                return;

                            case "ListView":
                                {
                                    ListView listView = (ListView)focusedElement;
                                    int idx = listView.SelectedIndex;

                                    if (idx != -1)
                                    {
                                        focusedElement = (ListViewItem)listView.ItemContainerGenerator.ContainerFromIndex(idx);
                                        Focus(focusedElement, listView, true);
                                        return;
                                    }
                                }
                                break;

                            case "ListViewItem":
                                {
                                    if (focusedElement is ListViewItem listViewItem)
                                    {
                                        if (ItemsControl.ItemsControlFromItemContainer(focusedElement) is ListView listView)
                                        {
                                            int idx = listView.Items.IndexOf(listViewItem);
                                            if (idx == -1)
                                                idx = listView.Items.IndexOf(listViewItem.Content);

                                            while (true) // Loop to skip disabled items
                                            {
                                                switch (direction)
                                                {
                                                    case WPFUtils.Direction.Up:
                                                        idx--;
                                                        break;

                                                    case WPFUtils.Direction.Down:
                                                        idx++;
                                                        break;
                                                }

                                                // Ensure index is within bounds
                                                if (idx < 0 || idx >= listView.Items.Count)
                                                {
                                                    focusedElement = WPFUtils.GetClosestControl<Control>(listView, gamepadWindow.controlElements, direction, [typeof(Control)]);
                                                    Focus(focusedElement);
                                                    return;
                                                }

                                                // Get the ListViewItem at the new index
                                                focusedElement = (ListViewItem)listView.ItemContainerGenerator.ContainerFromIndex(idx);

                                                // Check if the focused element is enabled
                                                if (focusedElement != null && focusedElement.IsEnabled)
                                                {
                                                    // If the element is enabled, focus it and break out of the loop
                                                    Focus(focusedElement, listView, true);
                                                    break;
                                                }

                                                // If the element is not enabled, continue to the next item in the loop
                                            }
                                        }
                                        return;
                                    }
                                }
                                break;

                            case "ComboBox":
                                {
                                    ComboBox comboBox = (ComboBox)focusedElement;
                                    int idx = comboBox.SelectedIndex;

                                    if (comboBox.IsDropDownOpen)
                                    {
                                        if (idx != -1)
                                        {
                                            focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);
                                            Focus(focusedElement, comboBox, true);
                                        }
                                        else
                                        {
                                            // No item selected yet — jump to the first enabled item
                                            // so that subsequent Up/Down navigation works correctly.
                                            for (int i = 0; i < comboBox.Items.Count; i++)
                                            {
                                                if (comboBox.ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxItem ci && ci.IsEnabled && ci.IsVisible)
                                                {
                                                    Focus(ci, comboBox, true);
                                                    break;
                                                }
                                            }
                                        }
                                        return;
                                    }
                                }
                                break;

                            case "ComboBoxItem":
                                {
                                    if (focusedElement is ComboBoxItem comboBoxItem)
                                    {
                                        if (ItemsControl.ItemsControlFromItemContainer(focusedElement) is ComboBox comboBox)
                                        {
                                            if (comboBox.IsDropDownOpen)
                                            {
                                                int idx = comboBox.Items.IndexOf(comboBoxItem);
                                                if (idx == -1)
                                                    idx = comboBox.Items.IndexOf(comboBoxItem.Content);

                                                while (true) // Loop to skip disabled items
                                                {
                                                    switch (direction)
                                                    {
                                                        case WPFUtils.Direction.Up:
                                                            idx--;
                                                            break;

                                                        case WPFUtils.Direction.Down:
                                                            idx++;
                                                            break;
                                                    }

                                                    // Ensure index is within bounds
                                                    if (idx < 0 || idx >= comboBox.Items.Count)
                                                    {
                                                        // We've reached the top or bottom, so stop the loop
                                                        break;
                                                    }

                                                    // Get the ComboBoxItem at the new index
                                                    focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);

                                                    // Check if the focused element is enabled
                                                    if (focusedElement != null && focusedElement.IsEnabled && focusedElement.IsVisible)
                                                    {
                                                        // If the element is enabled, focus it and break out of the loop
                                                        Focus(focusedElement, comboBox, true);
                                                        break;
                                                    }

                                                    // If the element is not enabled, continue to the next item in the loop
                                                }
                                            }
                                        }
                                        return;
                                    }
                                }
                                break;

                            case "MenuItem":
                                {
                                    if (HasFlyoutOpen && focusedElement is MenuItem currentMenuItem)
                                    {
                                        switch (direction)
                                        {
                                            case WPFUtils.Direction.Left:
                                                TryCloseFlyoutSubmenu(currentMenuItem);
                                                return;
                                            case WPFUtils.Direction.Right:
                                                TryOpenFlyoutSubmenu(currentMenuItem);
                                                return;
                                        }

                                        flyoutMenuItems = WPFUtils.GetSiblingMenuItems(currentMenuItem);
                                        if (flyoutMenuItems.Count == 0 && gamepadWindow.currentFlyoutButton?.Flyout is MenuFlyout menuFlyout)
                                            flyoutMenuItems = WPFUtils.GetDirectMenuItems(menuFlyout);

                                        if (flyoutMenuItems.Count == 0)
                                            return;

                                        int idx = flyoutMenuItems.IndexOf(currentMenuItem);
                                        if (idx < 0) idx = 0;

                                        while (true)
                                        {
                                            int nextIdx = idx;
                                            switch (direction)
                                            {
                                                case WPFUtils.Direction.Up: nextIdx--; break;
                                                case WPFUtils.Direction.Down: nextIdx++; break;
                                                default:
                                                    // Left/Right: stay on current item
                                                    return;
                                            }

                                            if (nextIdx < 0 || nextIdx >= flyoutMenuItems.Count)
                                            {
                                                // Reached top or bottom edge — stay on current item
                                                return;
                                            }

                                            idx = nextIdx;
                                            var candidate = flyoutMenuItems[idx];
                                            if (candidate.IsEnabled && candidate.IsVisible)
                                            {
                                                FocusFlyoutMenuItem(candidate);
                                                return;
                                            }
                                            // disabled item — keep looping
                                        }
                                    }
                                    return;
                                }

                            case "Slider":
                                {
                                    switch (direction)
                                    {
                                        case WPFUtils.Direction.Left:
                                            ((Slider)focusedElement).Value -= ((Slider)focusedElement).TickFrequency;
                                            Focus(focusedElement);
                                            return;
                                        case WPFUtils.Direction.Right:
                                            ((Slider)focusedElement).Value += ((Slider)focusedElement).TickFrequency;
                                            Focus(focusedElement);
                                            return;
                                    }
                                }
                                break;
                        }

                        // default
                        if (focusedElement is not null)
                        {
                            focusedElement = WPFUtils.GetClosestControl<Control>(focusedElement, gamepadWindow.controlElements, direction, [typeof(NavigationViewItem)]);

                            if (focusedElement is ListView listView)
                            {
                                int idx = listView.SelectedIndex;
                                if (idx == -1 && listView.Items.Count != 0) idx = 0;

                                if (idx != -1)
                                    focusedElement = (ListViewItem)listView.ItemContainerGenerator.ContainerFromIndex(idx);
                            }

                            Focus(focusedElement);
                        }
                    }
                }
                catch { }
            }, DispatcherPriority.Normal);
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // Check if we have a pending profile to restore focus to
            if (!pendingFocusRestoreProfileGuid.HasValue)
                return;

            // Only handle the profile we're waiting for
            if (profile.Guid != pendingFocusRestoreProfileGuid.Value)
                return;

            // Clear the pending Guid
            Guid guidToRestore = pendingFocusRestoreProfileGuid.Value;
            pendingFocusRestoreProfileGuid = null;

            // Use Dispatcher to ensure UI has updated after the profile change
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                Control? control = FindProfileControl(guidToRestore);
                if (control is not null)
                {
                    if (gamepadPage is not null)
                        StoreFocusedControl(gamepadPage, control);

                    Focus(control);
                }
            }, DispatcherPriority.Loaded);
        }
    }
}
