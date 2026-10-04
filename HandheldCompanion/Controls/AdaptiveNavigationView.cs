using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Custom NavigationView that switches items to icon-only mode when width is insufficient,
    /// instead of showing an overflow menu.
    /// </summary>
    public class AdaptiveNavigationView : NavigationView
    {
        private const double TopNavModeHysteresisWidth = 12.0;
        private const string TopNavAreaPartName = "TopNavArea";
        private const string TopNavBorderPartName = "TopNavBorder";
        private const string TopNavGridPartName = "TopNavGrid";
        private const string TopNavMenuItemsHostPartName = "TopNavMenuItemsHost";
        private const string TopNavOverflowButtonPartName = "TopNavOverflowButton";

        public static readonly DependencyProperty TopNavItemsAlignmentProperty =
            DependencyProperty.Register(
                nameof(TopNavItemsAlignment),
                typeof(HorizontalAlignment),
                typeof(AdaptiveNavigationView),
                new PropertyMetadata(HorizontalAlignment.Left, OnTopNavItemsAlignmentChanged));

        public HorizontalAlignment TopNavItemsAlignment
        {
            get => (HorizontalAlignment)GetValue(TopNavItemsAlignmentProperty);
            set => SetValue(TopNavItemsAlignmentProperty, value);
        }

        private Border? _topNavBorder;
        private Grid? _topNavGrid;
        private FrameworkElement? _topNavArea;
        private ItemsRepeater m_topNavRepeater;
        private Button m_topNavOverflowButton;
        private bool _isTopNavIconOnly;
        private double _cachedExpandedTopNavWidth;
        private bool? _lastAppliedIconOnly;
        private bool _syncPending;
        private bool _syncPendingWithUpdateLayout;
        private readonly List<NavigationViewItem> _realizedTopNavItems = [];
        private readonly Dictionary<NavigationViewItem, NavigationViewItemPresenter> _realizedTopNavPresenters = [];
        private readonly HashSet<NavigationViewItem> _pendingPresenterItems = [];

        public AdaptiveNavigationView()
        {
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == PaneDisplayModeProperty)
            {
                _cachedExpandedTopNavWidth = 0.0;
                _isTopNavIconOnly = false;
                _lastAppliedIconOnly = null;
                InvalidateMeasure();
            }
        }

        private static void OnTopNavItemsAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AdaptiveNavigationView navigationView)
            {
                navigationView._cachedExpandedTopNavWidth = 0.0;
                navigationView._isTopNavIconOnly = false;
                navigationView._lastAppliedIconOnly = null;
                navigationView.ApplyTopNavAlignment();
                navigationView.SyncRealizedTopNavState(true);
                navigationView.InvalidateMeasure();
            }
        }

        public override void OnApplyTemplate()
        {
            DetachTopNavRepeaterHandlers();
            DetachTrackedTopNavItemHandlers();

            base.OnApplyTemplate();

            _topNavArea = GetTemplateChild(TopNavAreaPartName) as FrameworkElement;
            _topNavBorder = GetTemplateChild(TopNavBorderPartName) as Border;
            _topNavGrid = GetTemplateChild(TopNavGridPartName) as Grid;
            m_topNavRepeater = GetTemplateChild(TopNavMenuItemsHostPartName) as ItemsRepeater;
            m_topNavOverflowButton = GetTemplateChild(TopNavOverflowButtonPartName) as Button;
            _realizedTopNavItems.Clear();
            _realizedTopNavPresenters.Clear();
            _pendingPresenterItems.Clear();
            _cachedExpandedTopNavWidth = 0.0;
            _lastAppliedIconOnly = null;
            _syncPending = false;
            _syncPendingWithUpdateLayout = false;

            ApplyTopNavAlignment();
            SyncRealizedTopNavState(true);

            if (m_topNavRepeater is not null)
            {
                m_topNavRepeater.ElementPrepared += TopNavRepeater_ElementPrepared;
                m_topNavRepeater.ElementClearing += TopNavRepeater_ElementClearing;
            }
        }

        private static DependencyPropertyDescriptor? _iconPropertyDescriptor;
        private static DependencyPropertyDescriptor IconPropertyDescriptor =>
            _iconPropertyDescriptor ??= DependencyPropertyDescriptor.FromName(
                "Icon", typeof(NavigationViewItem), typeof(NavigationViewItem));

        private void TopNavRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is NavigationViewItem navItem)
            {
                if (!_realizedTopNavItems.Contains(navItem))
                    _realizedTopNavItems.Add(navItem);

                _realizedTopNavPresenters.Remove(navItem);
                _pendingPresenterItems.Add(navItem);
                IconPropertyDescriptor.AddValueChanged(navItem, NavItem_IconChanged);
            }

            // A new item arrived — invalidate cache (expanded mode only) and re-apply.
            if (!_isTopNavIconOnly)
                _cachedExpandedTopNavWidth = 0.0;
            _lastAppliedIconOnly = null;
            EnqueueSync(true);
        }

        private void TopNavRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            if (args.Element is not NavigationViewItem navItem)
                return;

            _realizedTopNavItems.Remove(navItem);
            _realizedTopNavPresenters.Remove(navItem);
            _pendingPresenterItems.Remove(navItem);
            IconPropertyDescriptor.RemoveValueChanged(navItem, NavItem_IconChanged);

            navItem.MouseEnter -= NavItem_ReapplyIconOnlyState;
            navItem.MouseLeave -= NavItem_ReapplyIconOnlyState;
            navItem.GotKeyboardFocus -= NavItem_ReapplyIconOnlyState;
            navItem.LostKeyboardFocus -= NavItem_ReapplyIconOnlyState;
            navItem.PreviewMouseLeftButtonDown -= NavItem_ReapplyIconOnlyState;
            navItem.PreviewMouseLeftButtonUp -= NavItem_ReapplyIconOnlyState;
        }

        private void NavItem_IconChanged(object? sender, EventArgs e)
        {
            // Icon binding updated — invalidate cache (expanded mode only) and re-apply.
            if (!_isTopNavIconOnly)
                _cachedExpandedTopNavWidth = 0.0;
            _lastAppliedIconOnly = null;
            EnqueueSync(true);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            bool wasIconOnly = _isTopNavIconOnly;

            if (IsTopNavigationEnabled())
                UpdateTopNavigationPresentation(availableSize);

            Size desiredSize = base.MeasureOverride(availableSize);

            // If we just switched into icon-only mode, base.MeasureOverride may have called
            // OnApplyTemplate on presenters for the first time, resetting them to "IconOnLeft"
            // and measuring them at full text width. Pass updateLayout=true so every presenter
            // gets InvalidateMeasure and re-measures at icon size on the follow-up pass.
            // On that second pass wasIconOnly==true so modeJustChanged==false and we stop.
            bool modeJustChanged = !wasIconOnly && _isTopNavIconOnly;
            SyncRealizedTopNavState(modeJustChanged);

            return desiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Size arrangedSize = base.ArrangeOverride(arrangeBounds);
            if (arrangeBounds.Width == arrangedSize.Width)
                return arrangedSize;

            SyncRealizedTopNavState(false);

            return arrangedSize;
        }

        private bool IsTopNavigationEnabled()
        {
            return PaneDisplayMode == NavigationViewPaneDisplayMode.Top && _topNavGrid is not null && m_topNavRepeater is not null;
        }

        private void UpdateTopNavigationPresentation(Size availableSize)
        {
            if (_topNavGrid is null)
                return;

            if (double.IsInfinity(availableSize.Width) || availableSize.Width <= 0)
            {
                SetTopNavIconOnly(false);
                return;
            }

            // Hysteresis exit: if we're already in icon-only mode, just check whether
            // the window has grown enough to switch back. No measure needed.
            if (_isTopNavIconOnly)
            {
                if (_cachedExpandedTopNavWidth > 0.0 && availableSize.Width >= _cachedExpandedTopNavWidth + TopNavModeHysteresisWidth)
                    SetTopNavIconOnly(false);

                return;
            }

            // Measure only when the cache is stale (first layout, items changed, icon changed).
            if (_cachedExpandedTopNavWidth <= 0.0)
            {
                double expandedWidth = MeasureCurrentTopNavigationWidth();
                if (expandedWidth > 0.0)
                    _cachedExpandedTopNavWidth = expandedWidth;
            }

            if (_cachedExpandedTopNavWidth > 0.0 && _cachedExpandedTopNavWidth > availableSize.Width)
                SetTopNavIconOnly(true);
        }

        private double MeasureCurrentTopNavigationWidth()
        {
            if (_topNavGrid is null)
                return 0.0;

            // Only collapse spacer columns for Left-aligned items (MainWindow case).
            // For Center/Right aligned items (LibraryPage), use standard measurement.
            if (TopNavItemsAlignment != HorizontalAlignment.Left)
            {
                SuppressOverflowButton();
                _topNavGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return _topNavGrid.DesiredSize.Width;
            }
            else
            {
                GridLength[]? previousWidths = null;
                try
                {
                    previousWidths = new GridLength[3]
                    {
                        _topNavGrid.ColumnDefinitions[4].Width,
                        _topNavGrid.ColumnDefinitions[5].Width,
                        _topNavGrid.ColumnDefinitions[6].Width,
                    };

                    _topNavGrid.ColumnDefinitions[4].Width = new GridLength(0, GridUnitType.Pixel);
                    _topNavGrid.ColumnDefinitions[5].Width = new GridLength(0, GridUnitType.Pixel);
                    _topNavGrid.ColumnDefinitions[6].Width = new GridLength(0, GridUnitType.Pixel);

                    SuppressOverflowButton();
                    _topNavGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    return _topNavGrid.DesiredSize.Width;
                }
                finally
                {
                    if (previousWidths is not null)
                    {
                        _topNavGrid.ColumnDefinitions[4].Width = previousWidths[0];
                        _topNavGrid.ColumnDefinitions[5].Width = previousWidths[1];
                        _topNavGrid.ColumnDefinitions[6].Width = previousWidths[2];
                    }
                }
            }
        }

        private void SetTopNavIconOnly(bool isIconOnly)
        {
            if (_isTopNavIconOnly == isIconOnly)
                return;

            _isTopNavIconOnly = isIconOnly;
            ApplyTopNavItemPresentation(isIconOnly, true);
        }

        private void ApplyTopNavItemPresentation(bool isIconOnly, bool updateLayout)
        {
            if (m_topNavRepeater is null)
                return;

            if (!updateLayout && _lastAppliedIconOnly == isIconOnly)
                return;

            bool rewireEvents = _lastAppliedIconOnly != isIconOnly;

            foreach (NavigationViewItem item in _realizedTopNavItems)
            {
                NavigationViewItemPresenter? presenter = GetItemPresenter(item);
                if (presenter is null)
                    continue;

                string stateName;
                if (item.Icon is null)
                    stateName = "ContentOnly";
                else
                    stateName = isIconOnly ? "IconOnly" : "IconOnLeft";

                VisualStateManager.GoToState(presenter, stateName, false);
                if (isIconOnly)
                {
                    item.Width = 40;
                    item.Height = 48;
                    item.MinWidth = 40;
                    item.MinHeight = 48;
                    presenter.Width = 40;
                    presenter.Height = 48;
                    presenter.MinWidth = 40;
                    presenter.MinHeight = 48;
                }
                else
                {
                    item.ClearValue(WidthProperty);
                    item.ClearValue(HeightProperty);
                    item.ClearValue(MinWidthProperty);
                    item.ClearValue(MinHeightProperty);
                    presenter.ClearValue(WidthProperty);
                    presenter.ClearValue(HeightProperty);
                    presenter.ClearValue(MinWidthProperty);
                    presenter.ClearValue(MinHeightProperty);
                }

                // Only rewire the hover/focus events when the mode actually changes —
                // the rewire itself is cheap but the add/remove on every pass adds up.
                if (rewireEvents)
                {
                    item.MouseEnter -= NavItem_ReapplyIconOnlyState;
                    item.MouseLeave -= NavItem_ReapplyIconOnlyState;
                    item.GotKeyboardFocus -= NavItem_ReapplyIconOnlyState;
                    item.LostKeyboardFocus -= NavItem_ReapplyIconOnlyState;
                    item.PreviewMouseLeftButtonDown -= NavItem_ReapplyIconOnlyState;
                    item.PreviewMouseLeftButtonUp -= NavItem_ReapplyIconOnlyState;

                    if (isIconOnly && item.Icon is not null)
                    {
                        item.MouseEnter += NavItem_ReapplyIconOnlyState;
                        item.MouseLeave += NavItem_ReapplyIconOnlyState;
                        item.GotKeyboardFocus += NavItem_ReapplyIconOnlyState;
                        item.LostKeyboardFocus += NavItem_ReapplyIconOnlyState;
                        item.PreviewMouseLeftButtonDown += NavItem_ReapplyIconOnlyState;
                        item.PreviewMouseLeftButtonUp += NavItem_ReapplyIconOnlyState;
                    }
                }

                if (updateLayout)
                {
                    presenter.InvalidateMeasure();
                    presenter.InvalidateArrange();
                }
            }

            _lastAppliedIconOnly = isIconOnly;
        }

        private void DetachTopNavRepeaterHandlers()
        {
            if (m_topNavRepeater is null)
                return;

            m_topNavRepeater.ElementPrepared -= TopNavRepeater_ElementPrepared;
            m_topNavRepeater.ElementClearing -= TopNavRepeater_ElementClearing;
        }

        private void DetachTrackedTopNavItemHandlers()
        {
            foreach (NavigationViewItem item in _realizedTopNavItems)
            {
                IconPropertyDescriptor.RemoveValueChanged(item, NavItem_IconChanged);

                item.MouseEnter -= NavItem_ReapplyIconOnlyState;
                item.MouseLeave -= NavItem_ReapplyIconOnlyState;
                item.GotKeyboardFocus -= NavItem_ReapplyIconOnlyState;
                item.LostKeyboardFocus -= NavItem_ReapplyIconOnlyState;
                item.PreviewMouseLeftButtonDown -= NavItem_ReapplyIconOnlyState;
                item.PreviewMouseLeftButtonUp -= NavItem_ReapplyIconOnlyState;
            }

            foreach (NavigationViewItem item in _pendingPresenterItems)
            {
                IconPropertyDescriptor.RemoveValueChanged(item, NavItem_IconChanged);
            }

            _realizedTopNavItems.Clear();
            _realizedTopNavPresenters.Clear();
            _pendingPresenterItems.Clear();
        }

        private void NavItem_ReapplyIconOnlyState(object sender, RoutedEventArgs e)
        {
            if (sender is not NavigationViewItem item)
                return;

            // Defer so our GoToState runs after the presenter's own UpdateVisualState,
            // which fires synchronously during the same event. Normal priority runs
            // before the render pass, preventing any visible flash.
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                NavigationViewItemPresenter? presenter = GetItemPresenter(item);
                if (presenter is not null)
                    VisualStateManager.GoToState(presenter, "IconOnly", false);
            });
        }

        private void ApplyTopNavAlignment()
        {
            if (_topNavArea is null || _topNavBorder is null || _topNavGrid is null || m_topNavRepeater is null)
                return;

            _topNavArea.HorizontalAlignment = HorizontalAlignment.Stretch;
            _topNavBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            _topNavGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            m_topNavRepeater.HorizontalAlignment = TopNavItemsAlignment;

            if (_topNavGrid.ColumnDefinitions.Count <= 5)
                return;

            _topNavGrid.ColumnDefinitions[3].Width = new GridLength(1, GridUnitType.Star);
            _topNavGrid.ColumnDefinitions[5].Width = GridLength.Auto;
        }

        private void SuppressOverflowButton()
        {
            if (m_topNavOverflowButton is null)
                return;

            m_topNavOverflowButton.Visibility = Visibility.Collapsed;
            m_topNavOverflowButton.IsEnabled = false;
        }

        private void EnqueueSync(bool updateLayout)
        {
            if (updateLayout)
                _syncPendingWithUpdateLayout = true;

            if (_syncPending)
                return;

            _syncPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                bool withLayout = _syncPendingWithUpdateLayout;
                _syncPending = false;
                _syncPendingWithUpdateLayout = false;

                RefreshTrackedPresenters();

                SyncRealizedTopNavState(withLayout);
            });
        }

        private void RefreshTrackedPresenters()
        {
            if (_pendingPresenterItems.Count == 0)
                return;

            List<NavigationViewItem> pendingItems = [.. _pendingPresenterItems];
            foreach (NavigationViewItem item in pendingItems)
            {
                TryTrackPresenter(item);
            }
        }

        private void SyncRealizedTopNavState(bool updateLayout)
        {
            if (!IsTopNavigationEnabled())
                return;

            SuppressOverflowButton();
            ApplyTopNavItemPresentation(_isTopNavIconOnly, updateLayout);
        }

        private NavigationViewItemPresenter? GetItemPresenter(NavigationViewItem item)
        {
            if (!_realizedTopNavPresenters.TryGetValue(item, out NavigationViewItemPresenter? presenter) && TryTrackPresenter(item))
                _realizedTopNavPresenters.TryGetValue(item, out presenter);

            return presenter;
        }

        private bool TryTrackPresenter(NavigationViewItem item)
        {
            if (!_realizedTopNavItems.Contains(item))
            {
                _pendingPresenterItems.Remove(item);
                _realizedTopNavPresenters.Remove(item);
                return false;
            }

            NavigationViewItemPresenter? presenter = FindTrackedPresenter(item);
            if (presenter is null)
                return false;

            _realizedTopNavPresenters[item] = presenter;
            _pendingPresenterItems.Remove(item);
            return true;
        }

        private static NavigationViewItemPresenter? FindTrackedPresenter(DependencyObject root)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is NavigationViewItemPresenter presenter)
                    return presenter;

                NavigationViewItemPresenter? nestedPresenter = FindTrackedPresenter(child);
                if (nestedPresenter is not null)
                    return nestedPresenter;
            }

            return null;
        }
    }
}
