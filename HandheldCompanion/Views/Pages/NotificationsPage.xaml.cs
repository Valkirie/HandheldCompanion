using HandheldCompanion.Controls.Hints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Page = System.Windows.Controls.Page;
using HandheldCompanion.Utils;
using static HandheldCompanion.Views.Pages.NotificationsPage;
using System.Timers;

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
        private int prevStatus = -1;

        public NotificationsPage()
        {
            InitializeComponent();
        }

        public NotificationsPage(string Tag) : this()
        {
            this.Tag = Tag;

            timer = new(1000);
            timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                bool hasAnyVisible = Notifications.Children.OfType<IHint>().Any(element => element.Visibility == Visibility.Visible);
                NothingToSee.Visibility = hasAnyVisible ? Visibility.Collapsed : Visibility.Visible;

                int status = Convert.ToInt32(hasAnyVisible);
                if (status != prevStatus)
                {
                    StatusChanged?.Invoke(status);
                    prevStatus = status;
                }
            });
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        private void Page_LayoutUpdated(object sender, EventArgs e)
        {
            timer.Stop();
            timer.Start();
        }
    }
}
