using HandheldCompanion.Shared;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace HandheldCompanion.Managers
{
    public static class ToastManager
    {
        private const int Interval = 1000; // ms (unused)
        private const string Group = "HandheldCompanion";
        private const string ToastTag = "LatestToast";

        private static readonly ConcurrentQueue<(string Title, string Content, string Img, bool IsHero)> ToastQueue = new();
        private static volatile int _isProcessing; // 0 = idle, 1 = processing

        private static ToastNotification CurrentToastNotification;

        public static bool IsEnabled => ManagerFactory.settingsManager.GetBoolean("ToastEnable");
        private static bool IsInitialized { get; set; }

        static ToastManager() { }

        /// <summary>
        /// Enqueue a new toast.  Because every toast uses the same Tag+Group,
        /// calling Show(…) on the new toast automatically replaces (kills) the old one.
        /// Any existing queue entries are cleared first, so only the newest toast ever shows.
        /// </summary>
        public static bool SendToast(string title, string content = "", string img = "icon", bool isHero = false)
        {
            if (!IsEnabled)
                return false;

            // Flush any pending items in the queue:
            while (ToastQueue.TryDequeue(out _)) { }

            // Forcibly remove the previous toast from Action Center,
            if (CurrentToastNotification != null)
            {
                try
                {
                    ToastNotificationManager.History.Remove(ToastTag, Group);
                }
                catch
                {
                    // If removal fails, ignore it—Show(...) will still replace the on-screen content.
                }
                finally
                {
                    CurrentToastNotification = null;
                }
            }

            // Enqueue only this new toast, and process the queue immediately:
            ToastQueue.Enqueue((title, content, img, isHero));
            _ = ProcessToastQueue();

            return true;
        }

        // 2) Replace ProcessToastQueue() with this:
        private static async Task ProcessToastQueue()
        {
            // if already processing, bail quickly (the active loop will drain the queue)
            if (Interlocked.Exchange(ref _isProcessing, 1) == 1)
                return;

            try
            {
                while (ToastQueue.TryDequeue(out var toast))
                {
                    // Always build & show on the UI thread to avoid COM/WinRT deadlocks
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher is null || dispatcher.CheckAccess())
                    {
                        DisplayToast(toast.Title, toast.Content, toast.Img, toast.IsHero);
                    }
                    else
                    {
                        // ApplicationIdle reduces contention during hot startup
                        await dispatcher.InvokeAsync(
                            () => DisplayToast(toast.Title, toast.Content, toast.Img, toast.IsHero),
                            System.Windows.Threading.DispatcherPriority.ApplicationIdle
                        );
                    }
                }
            }
            catch
            {
                // never take the app down because of a toast
            }
            finally
            {
                Volatile.Write(ref _isProcessing, 0);

                // In case something enqueued while we were releasing the flag
                if (!ToastQueue.IsEmpty)
                    _ = ProcessToastQueue();
            }
        }

        private static void DisplayToast(string title, string content, string img, bool isHero)
        {
            // Build the path to the image (e.g. "Resources\\icon.png"):
            string imagePath = $"{AppDomain.CurrentDomain.BaseDirectory}Resources\\{img}.png";
            Uri imageUri = null;

            if (File.Exists(imagePath))
                imageUri = new Uri($"file:///{imagePath}");

            // Construct the toast content
            var toastBuilder = new ToastContentBuilder()
                .AddText(title)
                .AddText(content)
                .AddAudio(new ToastAudio
                {
                    Silent = true,
                    Src = new Uri("ms-winsoundevent:Notification.Default")
                })
                .SetToastScenario(ToastScenario.Default);

            if (imageUri != null)
            {
                if (isHero)
                    toastBuilder.AddHeroImage(imageUri);
                else
                    toastBuilder.AddAppLogoOverride(imageUri, ToastGenericAppLogoCrop.Default);
            }

            // Show the new toast. Because Tag = ToastTag and Group = Group are identical to any previous toast,
            // WindowsX will immediately replace the visible toast (killing the old one).
            toastBuilder.Show(toastNotification =>
            {
                toastNotification.Tag = ToastTag;
                toastNotification.Group = Group;

                // Keep a reference so we can also remove it from History if/when needed:
                CurrentToastNotification = toastNotification;
            });
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            LogManager.LogInformation("{0} has started", nameof(ToastManager));
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            // Clear any queued toasts
            ToastQueue.Clear();

            // Remove any currently displayed toast (from screen + Action Center)
            if (CurrentToastNotification != null)
            {
                try
                {
                    ToastNotificationManager.History.Remove(ToastTag, Group);
                }
                catch { /* ignore */ }
                finally
                {
                    CurrentToastNotification = null;
                }
            }

            IsInitialized = false;
            LogManager.LogInformation("{0} has stopped", nameof(ToastManager));
        }
    }
}