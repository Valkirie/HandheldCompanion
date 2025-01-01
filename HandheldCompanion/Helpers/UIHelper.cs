using System;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Helpers
{
    public static class UIHelper
    {
        public static void TryInvoke(Action callback)
        {
            if (Application.Current != null &&
                !Application.Current.Dispatcher.HasShutdownStarted &&
                !Application.Current.Dispatcher.HasShutdownFinished)
            {
                Application.Current.Dispatcher.Invoke(callback);
            }
        }

        public static TResult TryInvoke<TResult>(Func<TResult> func, TResult defaultValue = default)
        {
            if (Application.Current != null &&
                !Application.Current.Dispatcher.HasShutdownStarted &&
                !Application.Current.Dispatcher.HasShutdownFinished)
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(func);
                }
                catch (TaskCanceledException)
                {
                    // Gracefully handle dispatcher shutdown
                }
                catch (Exception ex)
                {
                    // Log the exception if needed
                    Console.WriteLine($"Dispatcher invoke failed: {ex.Message}");
                }
            }

            return defaultValue; // Return the default value if invoke is not possible
        }
    }
}
