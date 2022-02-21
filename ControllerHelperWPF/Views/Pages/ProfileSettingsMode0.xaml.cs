using ControllerCommon;
using System.Windows;
using System.Windows.Controls;

namespace ControllerHelperWPF.Views.Pages
{
    /// <summary>
    /// Interaction logic for ProfileSettingsMode0.xaml
    /// </summary>
    public partial class ProfileSettingsMode0 : Page
    {
        private Profile profileCurrent;

        public ProfileSettingsMode0()
        {
            InitializeComponent();
        }

        public ProfileSettingsMode0(Profile profileCurrent) : this()
        {
            this.profileCurrent = profileCurrent;
            this.Title += $" > {profileCurrent.name}";
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
