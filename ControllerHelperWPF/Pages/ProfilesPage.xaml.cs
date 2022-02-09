using System.Windows;
using System.Windows.Controls;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for Profiles.xaml
    /// </summary>
    public partial class ProfilesPage : Page
    {
        private MainWindow mainWindow;

        public ProfilesPage()
        {
            InitializeComponent();
        }

        public ProfilesPage(MainWindow mainWindow) : this()
        {
            this.mainWindow = mainWindow;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // temp
            mainWindow.ContentFrame.Navigate(typeof(ProfilePage));
        }
    }
}
