using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Threading;
using Timer = System.Timers.Timer;

namespace ControllerCommon.Managers
{
    public class ToastManager : Manager
    {
        private const int m_Interval = 5000;

        private Dictionary<string, Timer> m_Threads = new();

        private string m_Group;
        public bool Enabled;

        public ToastManager()
        { }

        public ToastManager(string group)
        {
            m_Group = group;
        }

        public void SendToast(string title, string content = "", string img = "Toast")
        {
            if (!Enabled)
                return;

            string url = $"file:///{AppDomain.CurrentDomain.BaseDirectory}Resources\\{img}.png";
            var uri = new Uri(url);

            // capricious ToastContentBuilder...
            try
            {
                new Thread(() =>
                {
                    var toast = new ToastContentBuilder()
                    .AddText(title)
                    .AddText(content)
                    .AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle)
                    .SetToastScenario(ToastScenario.Default);

                    if (toast != null)
                    {
                        toast.Show(toast =>
                        {
                            toast.Tag = title;
                            toast.Group = m_Group;
                        });

                        Timer timer = new Timer(m_Interval)
                        {
                            Enabled = true,
                            AutoReset = false
                        };

                        timer.Elapsed += (s, e) => { ToastNotificationManagerCompat.History.Remove(title, m_Group); };
                    }
                }).Start();
            }
            catch (Exception) { }
        }

        public override void Stop()
        {
            foreach (KeyValuePair<string, Timer> pair in m_Threads)
            {
                m_Threads[pair.Key].Stop();
                m_Threads[pair.Key] = null;
            }

            base.Stop();
        }
    }
}
