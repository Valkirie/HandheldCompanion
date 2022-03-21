using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Threading;

namespace ControllerCommon
{
    public class ToastManager
    {
        private const int m_Interval = 5000;
        private int m_Timer;

        private string m_Group;
        public bool Enabled;

        public ToastManager(string group)
        {
            m_Group = group;
        }

        public void SendToast(string title, string content, string img = "Toast")
        {
            if (!Enabled)
                return;

            string url = $"file:///{AppDomain.CurrentDomain.BaseDirectory}Resources\\{img}.png";
            var uri = new Uri(url);

            DateTimeOffset DeliveryTime = new DateTimeOffset(DateTime.Now.AddMilliseconds(100));

            new ToastContentBuilder()
                .AddText(title)
                .AddText(content)
                .AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle)
                .SetToastScenario(ToastScenario.Default)
                .Show(toast =>
                {
                    toast.Tag = title;
                    toast.Group = m_Group;
                });

            m_Timer += m_Interval; // remove toast after 5 seconds (incremental)

            var thread = new Thread(ClearHistory);
            thread.Start(new string[] { title, m_Group });
        }

        private void ClearHistory(object obj)
        {
            Thread.Sleep(m_Timer);
            string[] array = (string[])obj;
            string tag = array[0];
            string group = array[1];
            ToastNotificationManagerCompat.History.Remove(tag, group);

            m_Timer -= m_Interval;
        }
    }
}
