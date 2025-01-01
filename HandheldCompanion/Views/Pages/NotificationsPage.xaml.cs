using HandheldCompanion.Controls.Hints;
using HandheldCompanion.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for NotificationsPage.xaml
    /// </summary>
    public partial class NotificationsPage : Page
    {
        public delegate void StatusChangedEventHandler(int status);
        public event StatusChangedEventHandler StatusChanged;

        private Timer timer;
        private int prevNotifications = -1;

        public NotificationsPage()
        {
            InitializeComponent();
        }

        public NotificationsPage(string Tag) : this()
        {
            this.Tag = Tag;

            timer = new(150) { AutoReset = false };
            timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Calculate the number of visible notifications on a background thread
            int notifications = UIHelper.TryInvoke(() => Notifications.Children.OfType<IHint>().Count(element => element.Visibility == Visibility.Visible));
            if (notifications != prevNotifications)
            {
                // UI thread (async)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    NothingToSee.Visibility = notifications != 0 ? Visibility.Collapsed : Visibility.Visible;
                });

                // Raise the event outside the UI thread
                StatusChanged?.Invoke(notifications);

                // Update previous notification count
                prevNotifications = notifications;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                IEnumerable<IHint> notifications = Notifications.Children.OfType<IHint>();
                foreach (IHint hint in notifications)
                    hint?.Stop();
            });
        }

        private void Page_LayoutUpdated(object sender, EventArgs e)
        {
            timer.Stop();
            timer.Start();
        }
    }
}
