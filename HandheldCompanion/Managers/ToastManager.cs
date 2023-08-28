using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Concurrent;
using System.Threading;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ToastManager
{
    private const int m_Interval = 5000;
    private const string m_Group = "HandheldCompanion";

    private static readonly ConcurrentDictionary<string, Timer> m_Threads = new();

    public static bool IsEnabled;
    private static bool IsInitialized;

    static ToastManager()
    {
    }

    public static void SendToast(string title, string content = "", string img = "Toast")
    {
        if (!IsEnabled)
            return;

        var url = $"file:///{AppDomain.CurrentDomain.BaseDirectory}Resources\\{img}.png";
        var uri = new Uri(url);

        // capricious ToastContentBuilder...
        var ToastThread = new Thread(() =>
        {
            try
            {
                var toast = new ToastContentBuilder()
                    .AddText(title)
                    .AddText(content)
                    .AddAudio(new ToastAudio { Silent = true, Src = new Uri("ms-winsoundevent:Notification.Default") })
                    .AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle)
                    .SetToastScenario(ToastScenario.Default);

                if (toast is null)
                    return;

                toast.Show(toast =>
                {
                    toast.Tag = title;
                    toast.Group = m_Group;
                });

                var timer = new Timer(m_Interval)
                {
                    Enabled = true,
                    AutoReset = false
                };

                timer.Elapsed += (s, e) =>
                {
                    ToastNotificationManagerCompat.History.Remove(title, m_Group);
                    m_Threads.TryRemove(title, out _);
                };

                // add timer to bag
                m_Threads.TryAdd(title, timer);
            }
            catch (AccessViolationException)
            {
            }
            catch (Exception)
            {
            }
        });

        ToastThread.Start();
    }

    public static void Start()
    {
        IsInitialized = true;

        LogManager.LogInformation("{0} has started", "ToastManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;

        foreach (var timer in m_Threads.Values)
            timer.Stop();

        LogManager.LogInformation("{0} has stopped", "ToastManager");
    }
}