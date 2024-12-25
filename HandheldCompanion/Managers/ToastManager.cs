using HandheldCompanion.Shared;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers
{
    public static class ToastManager
    {
        private const int Interval = 5000; // Milliseconds
        private const string Group = "HandheldCompanion";

        private static readonly ConcurrentDictionary<string, CancellationTokenSource> ToastCancellationTokens = new();

        public static bool IsEnabled { get; set; }
        private static bool IsInitialized { get; set; }

        static ToastManager() { }

        public static void SendToast(string title, string content = "", string img = "icon", bool isHero = false)
        {
            if (!IsEnabled) return;

            string imagePath = $"{AppDomain.CurrentDomain.BaseDirectory}Resources\\{img}.png";
            Uri imageUri = new Uri($"file:///{imagePath}");

            // Use Task to avoid manual thread management
            Task.Run(async () =>
            {
                try
                {
                    ToastContentBuilder toast = new ToastContentBuilder()
                        .AddText(title)
                        .AddText(content)
                        .AddAudio(new ToastAudio { Silent = true, Src = new Uri("ms-winsoundevent:Notification.Default") })
                        .SetToastScenario(ToastScenario.Default);

                    if (File.Exists(imagePath))
                    {
                        if (isHero)
                            toast.AddHeroImage(imageUri);
                        else
                            toast.AddAppLogoOverride(imageUri, ToastGenericAppLogoCrop.Default);
                    }

                    if (toast == null) return;

                    // Show the toast
                    toast.Show(toastNotification =>
                    {
                        toastNotification.Tag = title;
                        toastNotification.Group = Group;
                    });

                    // Manage toast lifetime
                    CancellationTokenSource cts = new CancellationTokenSource();
                    if (!ToastCancellationTokens.TryAdd(title, cts)) return;

                    await Task.Delay(Interval, cts.Token);

                    if (!cts.Token.IsCancellationRequested)
                    {
                        ToastNotificationManagerCompat.History.Remove(title, Group);
                        ToastCancellationTokens.TryRemove(title, out _);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Task was canceled - safe to ignore
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Error in SendToast: {0}", ex.Message);
                }
            });
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            IsInitialized = true;
            LogManager.LogInformation("{0} has started", nameof(ToastManager));
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            foreach (CancellationTokenSource cts in ToastCancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            ToastCancellationTokens.Clear();

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", nameof(ToastManager));
        }

        private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "ToastEnable":
                    IsEnabled = Convert.ToBoolean(value);
                    break;
            }
        }
    }
}