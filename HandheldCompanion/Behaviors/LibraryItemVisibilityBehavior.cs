using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HandheldCompanion.Behaviors;

public static class LibraryItemVisibilityBehavior
{
    public static readonly DependencyProperty TrackVisibilityProperty = DependencyProperty.RegisterAttached(
        "TrackVisibility",
        typeof(bool),
        typeof(LibraryItemVisibilityBehavior),
        new PropertyMetadata(false, OnTrackVisibilityChanged));

    private static readonly DependencyProperty RegistrationProperty = DependencyProperty.RegisterAttached(
        "Registration",
        typeof(Registration),
        typeof(LibraryItemVisibilityBehavior),
        new PropertyMetadata(null));

    private static readonly Dictionary<ScrollViewer, ScrollViewerCoordinator> coordinators = [];

    public static bool GetTrackVisibility(DependencyObject element)
    {
        return (bool)element.GetValue(TrackVisibilityProperty);
    }

    public static void SetTrackVisibility(DependencyObject element, bool value)
    {
        element.SetValue(TrackVisibilityProperty, value);
    }

    private static void OnTrackVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        GetRegistration(element)?.Dispose();
        element.ClearValue(RegistrationProperty);

        if ((bool)e.NewValue)
            element.SetValue(RegistrationProperty, new Registration(element));
    }

    private static Registration? GetRegistration(DependencyObject dependencyObject)
    {
        return dependencyObject.GetValue(RegistrationProperty) as Registration;
    }

    private static ScrollViewerCoordinator GetCoordinator(ScrollViewer scrollViewer)
    {
        if (!coordinators.TryGetValue(scrollViewer, out ScrollViewerCoordinator? coordinator))
        {
            coordinator = new ScrollViewerCoordinator(scrollViewer);
            coordinators.Add(scrollViewer, coordinator);
        }

        return coordinator;
    }

    private static ScrollViewer? ResolveScrollViewer(FrameworkElement element)
    {
        return WPFUtils.FindParent<ScrollViewer>(element)
               ?? WPFUtils.FindVisualChild<ScrollViewer>(element);
    }

    private static void SetVisualVisibility(object? dataContext, bool isVisible)
    {
        if (dataContext is ProfileViewModel profileViewModel)
            profileViewModel.SetVisualsVisible(isVisible);
    }

    private static bool IsElementVisible(FrameworkElement element, Visual viewportHost, Rect viewportBounds)
    {
        if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            return false;

        try
        {
            Rect elementBounds = element.TransformToAncestor(viewportHost).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return elementBounds.IntersectsWith(viewportBounds);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class Registration : IDisposable
    {
        private readonly FrameworkElement element;
        private ScrollViewerCoordinator? coordinator;
        private bool disposed;

        public Registration(FrameworkElement element)
        {
            this.element = element;
            element.Loaded += Element_Loaded;
            element.Unloaded += Element_Unloaded;
            element.DataContextChanged += Element_DataContextChanged;
            element.IsVisibleChanged += Element_IsVisibleChanged;
            element.SizeChanged += Element_SizeChanged;

            if (element.IsLoaded)
                AttachToCoordinator();
        }

        public void UpdateVisibility(Rect viewportBounds, ScrollViewer scrollViewer)
        {
            SetVisualVisibility(element.DataContext, IsElementVisible(element, scrollViewer, viewportBounds));
        }

        private void Element_Loaded(object sender, RoutedEventArgs e)
        {
            AttachToCoordinator();
        }

        private void Element_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachFromCoordinator();
            SetVisualVisibility(element.DataContext, false);
        }

        private void Element_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetVisualVisibility(e.OldValue, false);

            if (element.IsLoaded)
                AttachToCoordinator(immediate: true);
        }

        private void Element_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            coordinator?.ScheduleRefresh(immediate: (bool)e.NewValue == false);
        }

        private void Element_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged || e.HeightChanged)
                coordinator?.ScheduleRefresh();
        }

        private void AttachToCoordinator(bool immediate = true)
        {
            ScrollViewer? scrollViewer = ResolveScrollViewer(element);
            if (scrollViewer is null)
            {
                DetachFromCoordinator();
                SetVisualVisibility(element.DataContext, true);
                return;
            }

            ScrollViewerCoordinator nextCoordinator = GetCoordinator(scrollViewer);
            if (!ReferenceEquals(coordinator, nextCoordinator))
            {
                DetachFromCoordinator();
                coordinator = nextCoordinator;
                coordinator.Register(this);
            }

            coordinator.ScheduleRefresh(immediate);
        }

        private void DetachFromCoordinator()
        {
            coordinator?.Unregister(this);
            coordinator = null;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            element.Loaded -= Element_Loaded;
            element.Unloaded -= Element_Unloaded;
            element.DataContextChanged -= Element_DataContextChanged;
            element.IsVisibleChanged -= Element_IsVisibleChanged;
            element.SizeChanged -= Element_SizeChanged;
            DetachFromCoordinator();
            SetVisualVisibility(element.DataContext, false);
        }
    }

    private sealed class ScrollViewerCoordinator : IDisposable
    {
        private readonly ScrollViewer scrollViewer;
        private readonly HashSet<Registration> registrations = [];
        private readonly DispatcherTimer refreshTimer;
        private bool refreshQueued;
        private bool disposed;

        public ScrollViewerCoordinator(ScrollViewer scrollViewer)
        {
            this.scrollViewer = scrollViewer;
            refreshTimer = new DispatcherTimer(DispatcherPriority.Background, scrollViewer.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(75)
            };

            refreshTimer.Tick += RefreshTimer_Tick;
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            scrollViewer.SizeChanged += ScrollViewer_SizeChanged;
        }

        public void Register(Registration registration)
        {
            registrations.Add(registration);
        }

        public void Unregister(Registration registration)
        {
            registrations.Remove(registration);
            if (registrations.Count == 0)
            {
                coordinators.Remove(scrollViewer);
                Dispose();
            }
        }

        public void ScheduleRefresh(bool immediate = false)
        {
            if (disposed)
                return;

            refreshTimer.Stop();

            if (immediate)
            {
                QueueRefresh();
                return;
            }

            refreshTimer.Start();
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange == 0 && e.HorizontalChange == 0 && e.ViewportHeightChange == 0 && e.ViewportWidthChange == 0)
                return;

            ScheduleRefresh();
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged || e.HeightChanged)
                ScheduleRefresh(immediate: true);
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            refreshTimer.Stop();
            QueueRefresh();
        }

        private void QueueRefresh()
        {
            if (refreshQueued || disposed)
                return;

            refreshQueued = true;
            scrollViewer.Dispatcher.BeginInvoke(new Action(() =>
            {
                refreshQueued = false;
                Refresh();
            }), DispatcherPriority.Background);
        }

        private void Refresh()
        {
            if (disposed)
                return;

            Rect viewportBounds = new(
                0,
                0,
                scrollViewer.ViewportWidth > 0 ? scrollViewer.ViewportWidth : scrollViewer.ActualWidth,
                scrollViewer.ViewportHeight > 0 ? scrollViewer.ViewportHeight : scrollViewer.ActualHeight);

            foreach (Registration registration in new List<Registration>(registrations))
                registration.UpdateVisibility(viewportBounds, scrollViewer);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            refreshTimer.Stop();
            refreshTimer.Tick -= RefreshTimer_Tick;
            scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            scrollViewer.SizeChanged -= ScrollViewer_SizeChanged;
            registrations.Clear();
        }
    }
}
