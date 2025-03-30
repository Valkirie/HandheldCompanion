using HandheldCompanion.ViewModels.Pages;
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

        public NotificationsPage()
        {
            DataContext = new NotificationPageViewModel();
            InitializeComponent();
        }

        public NotificationsPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        { }

        public void Page_Closed()
        { }
    }
}
