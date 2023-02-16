using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages.Profiles.Controller
{
    /// <summary>
    /// Interaction logic for TriggersPage.xaml
    /// </summary>
    public partial class TriggersPage : Page
    {
        public TriggersPage()
        {
            InitializeComponent();
        }

        public TriggersPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }
    }
}
