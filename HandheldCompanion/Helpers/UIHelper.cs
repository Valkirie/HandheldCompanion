using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HandheldCompanion.Helpers
{
    public static class UIHelper
    {
        public static bool TryInvoke(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return false;

                if (dispatcher.CheckAccess())
                {
                    action();
                    return true;
                }

                // Synchronous marshal (use sparingly; can block callers)
                dispatcher.Invoke(action, priority);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch
            {
                // Keep "best effort UI update" semantics (no throw)
                return false;
            }
        }

        public static bool TryBeginInvoke(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return false;

                if (dispatcher.CheckAccess())
                {
                    action();
                    return true;
                }

                dispatcher.BeginInvoke(action, priority);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Non-blocking UI marshal helper  intended for
        /// high-frequency/background callers to avoid stalls/deadlocks.
        /// </summary>
        public static TResult TryInvoke<TResult>(Func<TResult> action, TResult defaultValue = default!, DispatcherPriority priority = DispatcherPriority.Background)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return defaultValue;

                if (dispatcher.CheckAccess())
                    return action();

                return dispatcher.Invoke(action, priority);
            }
            catch (TaskCanceledException)
            {
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Async UI marshal that never blocks a background thread; safe alternative to TryInvoke for
        /// call paths where blocking could contribute to deadlocks/freezes.
        /// </summary>
        public static Task<bool> TryInvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return Task.FromResult(false);

                if (dispatcher.CheckAccess())
                {
                    action();
                    return Task.FromResult(true);
                }

                // InvokeAsync posts work and returns a task that completes when done
                return dispatcher.InvokeAsync(action, priority).Task.ContinueWith(
                    t => t.Status == TaskStatus.RanToCompletion,
                    TaskScheduler.Default);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
