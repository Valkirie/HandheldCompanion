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
using HandheldCompanion.Views.Pages.Library;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
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
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = System.Windows.Controls.Page;
using ProgressBar = iNKORE.UI.WPF.Modern.Controls.ProgressBar;
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
        private Timer embeddedNavTimer;

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
            public Dictionary<string, Control> LastContentControlsByView { get; } = [];
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

            embeddedNavTimer = new Timer(250) { AutoReset = false };
            embeddedNavTimer.Elapsed += EmbeddedContentRendered;

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
            if (control is null || !WPFUtils.CanTarget(control, gamepadWindow, includeContentRules: true))
                return;

            TrackFocusedControl(control);
        }

        private void SubscribeToAllFlyoutEvents()
        {
            // Find all buttons and drop-down buttons with flyouts and subscribe to their events
            var buttons = WPFUtils.FindVisualChildren<Button>(gamepadWindow).ToList();
            foreach (Button button in buttons)
            {
                if (button is DropDownButton) continue; // Handle DropDownButtons separately

                FlyoutBase? flyout = FlyoutService.GetFlyout(button);
                if (flyout is not null && !_subscribedFlyouts.Contains(flyout))
                {
                    SubscribeToFlyoutEvents(flyout, button);
                }
            }

            // Also handle DropDownButtons
            var dropDownButtons = WPFUtils.FindVisualChildren<DropDownButton>(gamepadWindow).ToList();
            foreach (DropDownButton dropDownButton in dropDownButtons)
            {
                FlyoutBase? flyout = dropDownButton.Flyout;
                if (flyout is not null && !_subscribedFlyouts.Contains(flyout))
                {
                    SubscribeToFlyoutEvents(flyout, dropDownButton);
                }
            }
        }

        private void SubscribeToFlyoutEvents(FlyoutBase flyout, Control button)
        {
            // Mark this flyout as subscribed to avoid duplicate handlers
            _subscribedFlyouts.Add(flyout);

            // Closed handler - fires every time the flyout closes
            // NOTE: We do NOT unsubscribe from this event, so it persists across multiple open/close cycles
            flyout.Closed += (s, e) =>
            {
                HasFlyoutOpen = false;
                gamepadWindow.currentFlyout = null;
                gamepadWindow.currentFlyoutButton = null;
                flyoutMenuItems.Clear();
                focusedFlyoutItem = null;
                UIHelper.TryInvoke(() =>
                {
                    if (_focused[windowName] && gamepadWindow.currentDialog is null)
                        Focus(button);
                });
            };

            // Opened handler - fires every time the flyout opens
            // NOTE: We do NOT unsubscribe from this event, so it persists across multiple open/close cycles
            flyout.Opened += (s, e) =>
            {
                HasFlyoutOpen = true;
                gamepadWindow.currentFlyout = flyout;
                gamepadWindow.currentFlyoutButton = button as DropDownButton;
                UIHelper.TryInvoke(() =>
                {
                    // For MenuFlyout (typically on DropDownButton), populate flyoutMenuItems
                    if (button is DropDownButton dropDownButton && dropDownButton.Flyout is MenuFlyout menuFlyout)
                    {
                        flyoutMenuItems.Clear();
                        flyoutMenuItems = WPFUtils.GetDirectMenuItems(menuFlyout);
                        var firstItem = flyoutMenuItems.FirstOrDefault(m => IsUsableFlyoutMenuItem(m));
                        if (firstItem is not null)
                            FocusFlyoutMenuItem(firstItem);
                    }
                    else
                    {
                        // For regular Flyout (on Button), focus the first focusable element
                        Control? firstElement = WPFUtils.GetTopLeftControl<Button>(gamepadWindow.controlElements);
                        if (firstElement is not null)
                            Focus(firstElement);
                    }
                });
            };
        }

        public void Loaded()
        {
            this.scrollViewer = WPFUtils.FindVisualChild<ScrollViewer>(gamepadWindow);
            this.windowNavigationView = FindWindowNavigationView();

            // will be resolved once the first Page is rendered
            this.pageNavigationView = null;

            // Subscribe to all flyout open/close events to track currentFlyout
            // This ensures currentFlyout is updated whether the flyout is opened by gamepad, mouse, or code
            SubscribeToAllFlyoutEvents();

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

        private Control? FindLibraryCollectionControlByKey(Page page, string? collectionKey, DependencyObject? contentRoot)
        {
            if (string.IsNullOrWhiteSpace(collectionKey))
                return null;

            return WPFUtils.FindVisualChildren<Button>(page).FirstOrDefault(button => WPFUtils.CanTarget(button, gamepadWindow, includeContentRules: true) && string.Equals(GetLibraryCollectionKey(button), collectionKey, StringComparison.Ordinal));
        }

        private Control? FindDefaultLibraryCollectionControl(Page page, DependencyObject? contentRoot)
        {
            return WPFUtils.GetTopLeftControl<Button>(WPFUtils.FindVisualChildren<Button>(page).Where(button => WPFUtils.CanTarget(button, gamepadWindow, includeContentRules: true) && !string.IsNullOrWhiteSpace(GetLibraryCollectionKey(button))).Cast<Control>().ToList());
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
            return WPFUtils.CanTarget(navigationViewItem)
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
            string? libraryKey = GetActiveLibraryNavigationKey(page, navigationView);
            if (!string.IsNullOrWhiteSpace(libraryKey))
                return libraryKey;

            navigationView ??= FindActivePageNavigationView(page);

            if (navigationView is not null)
                return GetPageFromNavigationViewItemTag(GetCurrentNavigationViewItem(navigationView));

            return null;
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

        private static Page? GetNavigationViewPage(NavigationView? navigationView)
        {
            Frame? embeddedFrame = FindEmbeddedNavFrame(navigationView);
            return embeddedFrame?.Content as Page;
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

        private Control? GetTopLeftFocusableContentControl(DependencyObject? scopeRoot, bool includeNavigationViewItems = false)
        {
            if (scopeRoot is null)
                return null;

            List<Control> allControls = WPFUtils.FindVisualChildren<Control>(scopeRoot).ToList();
            List<Control> controls = allControls
                .Where(control => WPFUtils.CanTarget(control, gamepadWindow, includeContentRules: true, includeNavigationViewItems: includeNavigationViewItems))
                .ToList();

            if (controls.Count == 0 && allControls.Count > 0)
            {
                // Log why the first few candidates were rejected
                foreach (Control c in allControls.Take(5))
                {
                    if (c is ItemsControl || IsTransientContainerControl(c)) continue;
                }
            }

            return WPFUtils.GetTopLeftControl<Control>(controls);
        }

        private Control? ResolveStoredContentControl(Page page, NavigationView? navigationView)
        {
            // Try the most recent focused control first.
            PageFocusState state = GetPageFocusState(page);
            DependencyObject? contentRoot = GetNavigationViewContentRoot(navigationView, page);
            string? viewKey = GetActivePageViewKey(page, navigationView);

            // Library pages need a small special-case for the collections view.
            if (page is LibraryPage)
            {
                DependencyObject? embeddedContentRoot = GetNavigationViewContentRoot(pageNavigationView ?? FindActivePageNavigationView(page), page);
                if (embeddedContentRoot is LibraryCollectionsOverviewPage && page.DataContext is LibraryPageViewModel libraryPageViewModel)
                {
                    Control? collectionsControl = ResolveLibraryCollectionsOverviewControl(page, libraryPageViewModel, contentRoot);
                    if (collectionsControl is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(viewKey))
                            state.LastContentControlsByView[viewKey] = collectionsControl;

                        state.LastContentControl = collectionsControl;
                        return collectionsControl;
                    }
                }
            }

            // Restore the last control used for this specific view.
            if (!string.IsNullOrWhiteSpace(viewKey) && state.LastContentControlsByView.TryGetValue(viewKey, out Control? storedViewControl))
            {
                Control? recoveredViewControl = RecoverStoredContentControl(page, storedViewControl, contentRoot);
                if (recoveredViewControl is not null)
                {
                    state.LastContentControlsByView[viewKey] = recoveredViewControl;
                    state.LastContentControl = recoveredViewControl;
                    return recoveredViewControl;
                }

                if (WPFUtils.CanTarget(storedViewControl, gamepadWindow, includeContentRules: true))
                    return storedViewControl;

                state.LastContentControlsByView.Remove(viewKey);
            }

            // Fall back to the page-wide last focused control.
            if (state.LastContentControl is not null)
            {
                Control? recoveredControl = RecoverStoredContentControl(page, state.LastContentControl, contentRoot);
                if (recoveredControl is not null)
                {
                    state.LastContentControl = recoveredControl;
                    if (!string.IsNullOrWhiteSpace(viewKey))
                        state.LastContentControlsByView[viewKey] = recoveredControl;

                    return recoveredControl;
                }

                if (WPFUtils.CanTarget(state.LastContentControl, gamepadWindow, includeContentRules: true))
                    return state.LastContentControl;
            }

            // Last known profile, if we have one.
            if (state.LastContentProfileGuid.HasValue)
            {
                Control? resolvedControl = FindProfileControl(state.LastContentProfileGuid.Value, page);
                if (WPFUtils.CanTarget(resolvedControl, gamepadWindow, includeContentRules: true))
                {
                    state.LastContentControl = resolvedControl;
                    return resolvedControl;
                }
            }

            // Final fallback for library content.
            if (page is LibraryPage)
            {
                DependencyObject? embeddedContentRoot = GetNavigationViewContentRoot(pageNavigationView ?? FindActivePageNavigationView(page), page);
                Control? embeddedFallback = FindFirstLibraryProfileControl(embeddedContentRoot);

                if (embeddedFallback is null && embeddedContentRoot is not null)
                    embeddedFallback = GetTopLeftFocusableContentControl(embeddedContentRoot);

                if (embeddedFallback is not null)
                {
                    state.LastContentControl = embeddedFallback;

                    if (!string.IsNullOrWhiteSpace(viewKey))
                        state.LastContentControlsByView[viewKey] = embeddedFallback;

                    return embeddedFallback;
                }
            }

            return null;
        }

        private Control? ResolveLibraryCollectionsOverviewControl(Page page, LibraryPageViewModel libraryPageViewModel, DependencyObject? contentRoot)
        {
            Control? collectionsControl = FindLibraryCollectionControlByKey(page, libraryPageViewModel.GetLastCollectionsOverviewItemKey(), contentRoot);
            if (collectionsControl is not null)
                return collectionsControl;

            Control? defaultCollectionControl = FindDefaultLibraryCollectionControl(page, contentRoot);
            if (defaultCollectionControl is not null)
                return defaultCollectionControl;

            return null;
        }

        private Control? FindFirstLibraryProfileControl(DependencyObject? searchRoot)
        {
            if (searchRoot is null)
                return null;

            return WPFUtils.FindVisualChildren<Button>(searchRoot).FirstOrDefault(button => WPFUtils.CanTarget(button, gamepadWindow, includeContentRules: true) && TryGetProfileGuid(button, out _));
        }

        private Control? RecoverStoredContentControl(Page page, Control storedControl, DependencyObject? contentRoot)
        {
            if (WPFUtils.CanTarget(storedControl, gamepadWindow, includeContentRules: true))
                return storedControl;

            if (storedControl is Button button && button.DataContext is CollectionGroupViewModel group)
            {
                Control? collectionControl = FindLibraryCollectionControlByKey(page, GetLibraryCollectionKey(button), contentRoot);
                if (collectionControl is not null)
                    return collectionControl;
            }

            if (TryGetProfileGuid(storedControl, out Guid profileGuid))
            {
                Control? profileControl = FindProfileControl(profileGuid, page);
                if (profileControl is not null)
                    return profileControl;
            }

            object? storedDataContext = storedControl.DataContext;
            object? storedTag = storedControl.Tag;

            if (storedDataContext is null && storedTag is null)
                return null;

            if (contentRoot is null)
                return null;

            return WPFUtils.FindVisualChildren<Control>(contentRoot)
                .FirstOrDefault(control => WPFUtils.CanTarget(control, gamepadWindow, includeContentRules: true)
                && !ReferenceEquals(control, storedControl)
                && (ReferenceEquals(control.DataContext, storedDataContext) || ReferenceEquals(control.Tag, storedTag)));
        }

        private bool RestoreOrFocusTopLeftElementInNavigationViewContent(NavigationView? navigationView)
        {
            if (gamepadPage is null)
                return false;

            try
            {
                _isNavigationViewContentRestoreInProgress = true;

                DependencyObject? contentRoot = GetNavigationViewContentRoot(navigationView, gamepadPage);

                Control? stored = ResolveStoredContentControl(gamepadPage, navigationView);
                Control? topLeft = stored is null ? GetTopLeftFocusableContentControl(contentRoot) : null;
                Control? topLeftWithNav = (stored is null && topLeft is null) ? GetTopLeftFocusableContentControl(contentRoot, includeNavigationViewItems: true) : null;
                Control? navItem = (stored is null && topLeft is null && topLeftWithNav is null) ? GetCurrentNavigationViewItem(navigationView) : null;
                Control? control = stored ?? topLeft ?? topLeftWithNav ?? navItem;

                if (control is not null)
                {
                    Focus(control);
                    return true;
                }

                Frame? embeddedFrame = FindEmbeddedNavFrame(navigationView);
                if (embeddedFrame is not null)
                {
                    return true;
                }

                return false;
            }
            finally
            {
                _isNavigationViewContentRestoreInProgress = false;
            }
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

                if (!IsCurrentNavigationViewTarget(navigationView, navigationTarget))
                {
                    if (!NavigateActivePageNavigationView(navigationTarget))
                        return false;

                    if (FindEmbeddedNavFrame(navigationView) is null)
                    {
                        Page pageRef = gamepadPage;
                        NavigationView navRef = navigationView;
                        gamepadWindow.Dispatcher.BeginInvoke(() =>
                        {
                            if (ReferenceEquals(gamepadPage, pageRef))
                                RestoreOrFocusTopLeftElementInNavigationViewContent(navRef);
                        }, DispatcherPriority.Loaded);
                    }

                    return true;
                }

                // No Frame means the nav only filters content (e.g. library page).
                // Defer so the content update/filtering can complete before we search for controls.
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

        private bool TryRestoreLastFocusedControl(Page? page)
        {
            if (page is null)
                return false;

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

            NavigationView? activeNavView = page is LibraryPage ? pageNavigationView ?? FindActivePageNavigationView(page) : windowNavigationView;
            Page focusPage = GetNavigationViewPage(activeNavView) ?? page;

            Control? control = ResolveStoredContentControl(focusPage, activeNavView) ?? GetTopLeftFocusableContentControl(GetNavigationViewContentRoot(activeNavView, focusPage));
            if (control is null)
                return false;

            Focus(control);
            return true;
        }

        private static bool IsLibraryPage(Page page)
        {
            return page is LibraryPage;
        }

        private string? GetActiveLibraryNavigationKey(Page page, NavigationView? navigationView = null)
        {
            if (!IsLibraryPage(page))
                return null;

            if (page is ILibraryRoutedPage routedPage)
                return routedPage.NavigationKey;

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
                || control is MessageBox
                || control is ContentDialog
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

        private bool HasFlyoutOpen = false;
        private List<MenuItem> flyoutMenuItems = new();  // populated from MenuFlyout.Items when open
        private MenuItem? focusedFlyoutItem = null;        // tracks which item is highlighted
        private HashSet<FlyoutBase> _subscribedFlyouts = new();  // tracks which flyouts we've already subscribed to

        private static bool IsUsableFlyoutMenuItem(MenuItem? menuItem)
        {
            return menuItem is not null
                && menuItem.IsEnabled
                && menuItem.Focusable;
        }

        private void FocusFlyoutMenuItem(MenuItem menuItem)
        {
            List<MenuItem> siblingMenuItems = WPFUtils.GetSiblingMenuItems(menuItem);

            if (siblingMenuItems.Count == 0 && gamepadWindow.currentFlyoutButton?.Flyout is MenuFlyout menuFlyout)
                siblingMenuItems = WPFUtils.GetDirectMenuItems(menuFlyout);

            flyoutMenuItems = siblingMenuItems;
            focusedFlyoutItem = menuItem;
            Focus(menuItem);
        }

        private bool TryOpenFlyoutSubmenu(MenuItem menuItem)
        {
            if (!menuItem.HasItems)
                return false;

            MenuItem? firstChild = WPFUtils.GetDirectMenuItems(menuItem)
                .FirstOrDefault(m => IsUsableFlyoutMenuItem(m));

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
            FocusTopLeftModalControl();
        }

        private void ContentDialogClosed(ContentDialog contentDialog)
        {
            RestoreFocusAfterModalClosed();
        }

        private void MessageBoxOpened(MessageBox messageBox)
        {
            FocusTopLeftModalControl();
        }

        private void MessageBoxClosed(MessageBox messageBox)
        {
            RestoreFocusAfterModalClosed();
        }

        private void FocusTopLeftModalControl()
        {
            // Defer: modal children are not always in the visual tree yet when the
            // open event fires (layout is still completing).
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                Control? control = GetTopLeftNavigableControl();
                if (control is not null)
                    Focus(control);
            }, DispatcherPriority.Loaded);
        }

        private void RestoreFocusAfterModalClosed()
        {
            if (gamepadPage is not null)
            {
                Control? control = ResolveStoredContentControl(gamepadPage, pageNavigationView ?? windowNavigationView) ?? ResolveStoredContentControl(gamepadPage, windowNavigationView);
                if (control is not null)
                    Focus(control);
            }
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

                    // pull embedded navigation view from page
                    UpdateEmbeddedNavigationFrame();

                    // reset page-scoped navigation view state
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
            RestoreCurrentPageFocus();
        }

        private void LibraryPageViewModel_NavigatedBackToCollections()
        {
            RestoreCurrentPageFocus();
        }

        private void RestoreCurrentPageFocus()
        {
            if (gamepadPage is null)
                return;

            Page pageRef = gamepadPage;
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                pageRef.UpdateLayout();
                if (pageRef is LibraryPage)
                    UpdateEmbeddedNavigationFrame();

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

                if (gamepadPage is not null)
                {
                    NavigationView? activeNavView = pageNavigationView ?? windowNavigationView;
                    RestoreFocusForCurrentPage(activeNavView, gamepadPage, justNavigated);
                    ResetBackNavigationAtHomePage();
                }

                // store selected navigation items (window and page)
                _lastWindowNavigationItem = GetCurrentNavigationViewItem(windowNavigationView);

                // set rendering state
                _rendered = true;

                // Subscribe to all flyout open/close events in case new buttons with flyouts were rendered
                SubscribeToAllFlyoutEvents();
            });
        }

        private void EmbeddedContentRendering(object? sender, EventArgs e)
        {
            if (gamepadPage is null || !HasFocus())
                return;

            embeddedNavTimer.Stop();
            embeddedNavTimer.Start();
        }

        private void EmbeddedContentRendered(object? sender, System.Timers.ElapsedEventArgs? e)
        {
            UIHelper.TryInvoke(() =>
            {
                if (gamepadPage is null)
                    return;

                if (pageNavigationView is null || _embeddedNavFrame is null)
                    UpdateEmbeddedNavigationFrame();

                RestoreFocusForCurrentPage(pageNavigationView, gamepadPage, justNavigated: false);
            });
        }

        private void UpdateEmbeddedNavigationFrame()
        {
            pageNavigationView = FindActivePageNavigationView(gamepadPage);

            Frame? nextEmbeddedNavFrame = FindEmbeddedNavFrame(pageNavigationView);
            if (ReferenceEquals(_embeddedNavFrame, nextEmbeddedNavFrame))
                return;

            if (_embeddedNavFrame is not null)
                _embeddedNavFrame.ContentRendered -= EmbeddedContentRendering;

            _embeddedNavFrame = nextEmbeddedNavFrame;

            if (_embeddedNavFrame is not null)
                _embeddedNavFrame.ContentRendered += EmbeddedContentRendering;

            // already loaded ?
            if (_embeddedNavFrame is not null && _embeddedNavFrame.IsLoaded)
                EmbeddedContentRendered(null, null);
        }

        private void RestoreFocusForCurrentPage(NavigationView? activeNavView, Page page, bool justNavigated = false)
        {
            if (page != gamepadPage)
                return;

            if (!TryRestoreLastFocusedControl(page))
            {
                if (!ShouldKeepFocusOnWindowNavigation(justNavigated))
                {
                    DependencyObject? contentRoot = GetNavigationViewContentRoot(activeNavView, page);
                    Control? control = ResolveStoredContentControl(page, activeNavView);

                    if (control is null)
                        control = GetTopLeftFocusableContentControl(contentRoot);

                    if (control is not null)
                        Focus(control);
                }
            }
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
            // When a MenuFlyout is open, return the tracked popup item directly.
            // Popup elements have no common visual ancestor with gamepadWindow,
            // so the FindCommonAncestor check below would incorrectly reset focus.
            if (HasFlyoutOpen && focusedFlyoutItem is not null)
                return focusedFlyoutItem;

            IInputElement FocusedElement = forcedFocus is not null ? forcedFocus : gamepadWindow.GetFocusedElement();

            // If a regular Flyout is open, verify the focused element is within it.
            // If not, reset focus to a control within the flyout.
            if (gamepadWindow.currentFlyout is not null && FocusedElement is DependencyObject focusedDO)
            {
                // Check if the focused element is actually inside the flyout's content
                bool isInFlyoutContent = false;
                if (gamepadWindow.currentFlyout is Flyout standardFlyout && standardFlyout.Content is DependencyObject contentRoot)
                {
                    List<FrameworkElement> flyoutElements = WPFUtils.FindChildren(contentRoot);
                    isInFlyoutContent = flyoutElements.Cast<DependencyObject>().Any(el => el == focusedDO);
                }

                if (!isInFlyoutContent)
                {
                    // Focused element is not in the flyout, reset to first element in flyout
                    Control? flyoutElement = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements);
                    if (flyoutElement is not null)
                    {
                        FocusedElement = flyoutElement;
                        forcedFocus = flyoutElement;
                    }
                }
            }

            DependencyObject commonAncestor = VisualTreeHelperExtensions.FindCommonAncestor((DependencyObject)FocusedElement, gamepadWindow);
            if (commonAncestor is null && forcedFocus is null && gamepadWindow.currentFlyout is null)
            {
                FocusManager.SetFocusedElement(gamepadWindow, GetTopLeftNavigableControl());
                FocusedElement = FocusManager.GetFocusedElement(gamepadWindow);
            }

            if (FocusedElement is null)
                FocusedElement = gamepadWindow;

            if (FocusedElement is Control controlFocused
                && WPFUtils.CanTarget(controlFocused))
            {
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
                return;
            }

            if (control is NavigationViewItem)
            {
                return;
            }

            // Normalize ComboBoxItem to its parent ComboBox — items are transient (hidden when closed).
            if (control is ComboBoxItem comboBoxItem)
            {
                if (ItemsControl.ItemsControlFromItemContainer(comboBoxItem) is ComboBox parentComboBox)
                {
                    control = parentComboBox;
                }
                else
                {
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
            }

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
                if (navigationView is not null && navigationView != windowNavigationView
                    && !IsNavigationViewFocusChangeInProgress())
                {
                    NavigateFromFocusedNavigationViewItem(navigationViewItem);
                }

                return;
            }

            StoreFocusedControl(gamepadPage, control);
        }

        private bool IsUsableStoredControl(Control? control)
        {
            return control is not null
                && !IsTransientContainerControl(control)
                && WPFUtils.CanTarget(control)
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

            return false;
        }

        private Control? FindProfileControl(Guid profileGuid, DependencyObject? searchRoot = null)
        {
            searchRoot ??= gamepadPage is not null ? gamepadPage : gamepadWindow;

            return WPFUtils.FindVisualChildren<Button>(searchRoot)
                .FirstOrDefault(button => WPFUtils.CanTarget(button, gamepadWindow, includeContentRules: true) && TryGetProfileGuid(button, out Guid guid) && guid == profileGuid);
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
                if (!WPFUtils.CanTarget(focusedElement))
                    return;
                ExecuteSelect(focusedElement);
            });
        }

        public void TryMore()
        {
            UIHelper.TryInvoke(() =>
            {
                Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());
                if (!WPFUtils.CanTarget(focusedElement))
                    return;
                ExecuteMore(focusedElement);
            });
        }

        public void TryToggle()
        {
            UIHelper.TryInvoke(() =>
            {
                Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());
                if (!WPFUtils.CanTarget(focusedElement))
                    return;
                ExecuteToggle(focusedElement);
            });
        }

        public void TryLike()
        {
            UIHelper.TryInvoke(() =>
            {
                Control? focusedElement = NormalizeNavigationViewFocus(GetFocusedElement());
                if (!WPFUtils.CanTarget(focusedElement))
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
                        if (comboBox.ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxItem ci && WPFUtils.CanTarget(ci))
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
                dropDownButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
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
                        if (mi is NavigationViewItem nvi && WPFUtils.CanTarget(nvi))
                            ordered.Add(nvi);
                    }
                }

                if (navView.FooterMenuItems is not null)
                {
                    foreach (var mi in navView.FooterMenuItems)
                    {
                        if (mi is NavigationViewItem nvi && WPFUtils.CanTarget(nvi))
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
                           .Where(i => i is Control c && WPFUtils.CanTarget(c))
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

            // UI thread (non-blocking to avoid deadlocks on high-frequency input events)
            UIHelper.TryBeginInvoke(() =>
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
                    if (!WPFUtils.CanTarget(focusedElement))
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

                        // close flyout, if any (DropDownButton)
                        if (HasFlyoutOpen && gamepadWindow.currentFlyoutButton?.Flyout is { } openFlyout)
                        {
                            openFlyout.Hide();
                            return;
                        }

                        // close regular Flyout, if any
                        if (gamepadWindow.currentFlyout is { } regularFlyout)
                        {
                            regularFlyout.Hide();
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
                                if (!IsQuicktools)
                                {
                                    if (TryNavigateBackInHistory())
                                        return;

                                    if (TryFocusWindowNavigationAnchor())
                                        return;
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
                                        Control? control = ResolveStoredContentControl(gamepadPage, pageNavigationView ?? windowNavigationView) ?? ResolveStoredContentControl(gamepadPage, windowNavigationView);
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
                                        Control? control = ResolveStoredContentControl(gamepadPage, pageNavigationView ?? windowNavigationView) ?? ResolveStoredContentControl(gamepadPage, windowNavigationView);
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
                                                if (WPFUtils.CanTarget(focusedElement))
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
                                                if (comboBox.ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxItem ci && WPFUtils.CanTarget(ci))
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
                                                    if (WPFUtils.CanTarget(focusedElement))
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
                                            // Keep helper-based usability check aligned with the flyout-open branch.
                                            if (IsUsableFlyoutMenuItem(candidate))
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
