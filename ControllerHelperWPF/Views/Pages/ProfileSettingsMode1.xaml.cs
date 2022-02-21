using ControllerCommon;
using System.Windows.Controls;

namespace ControllerHelperWPF.Views.Pages
{
    /// <summary>
    /// Interaction logic for ProfileSettingsMode1.xaml
    /// </summary>
    public partial class ProfileSettingsMode1 : Page
    {
        private Profile profileCurrent;

        public ProfileSettingsMode1()
        {
            InitializeComponent();
        }

        public ProfileSettingsMode1(Profile profileCurrent)
        {
            this.profileCurrent = profileCurrent;
        }
    }
}
