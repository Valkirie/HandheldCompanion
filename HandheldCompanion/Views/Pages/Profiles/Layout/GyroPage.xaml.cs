using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages.Profiles.Controller
{
    /// <summary>
    /// Interaction logic for GyroPage.xaml
    /// </summary>
    public partial class GyroPage : Page
    {
        public GyroPage()
        {
            InitializeComponent();
        }

        public GyroPage(string Tag) : this()
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
