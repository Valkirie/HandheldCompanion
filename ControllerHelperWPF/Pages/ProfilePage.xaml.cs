using System.Windows;
using System.Windows.Controls;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for Profiles.xaml
    /// </summary>
    public partial class ProfilePage : Page
    {
        private MainWindow mainWindow;

        public ProfilePage()
        {
            InitializeComponent();
        }

        public ProfilePage(MainWindow mainWindow) : this()
        {
            this.mainWindow = mainWindow;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
