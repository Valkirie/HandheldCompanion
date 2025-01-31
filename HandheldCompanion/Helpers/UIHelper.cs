using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HandheldCompanion.Helpers
{
    public static class UIHelper
    {
        public static void TryInvoke(Action callback)
        {
            if (Application.Current?.Dispatcher is Dispatcher dispatcher && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                try
                {
                    dispatcher.Invoke(callback);
                }
                catch (TaskCanceledException)
                {
                    // Gracefully handle dispatcher shutdown
                }
                catch { }
            }
        }

        public static TResult TryInvoke<TResult>(Func<TResult> func, TResult defaultValue = default)
        {
            if (Application.Current?.Dispatcher is Dispatcher dispatcher && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                try
                {
                    return dispatcher.Invoke(func);
                }
                catch (TaskCanceledException)
                {
                    // Gracefully handle dispatcher shutdown
                }
                catch { }
            }

            return defaultValue; // Return the default value if invoke is not possible
        }
    }
}
