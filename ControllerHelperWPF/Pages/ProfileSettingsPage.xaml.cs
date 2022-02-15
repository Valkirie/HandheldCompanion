using System.Windows;
using System.Windows.Controls;
using ControllerCommon;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for Profiles.xaml
    /// </summary>
    public partial class ProfileSettingsPage : Page
    {
        private MainWindow mainWindow;

        public ProfileSettingsPage()
        {
            InitializeComponent();
        }

        public ProfileSettingsPage(Profile profile) : this()
        {
            this.mainWindow = mainWindow;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
