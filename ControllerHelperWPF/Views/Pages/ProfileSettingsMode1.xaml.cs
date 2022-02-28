using ControllerCommon;
using System.Windows;
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

        public ProfileSettingsMode1(Profile profileCurrent, PipeClient pipeClient) : this()
        {
            this.profileCurrent = profileCurrent;

            SliderDeadzoneAngle.Value = profileCurrent.steering_deadzone;
            SliderDeadzoneCompensation.Value = profileCurrent.steering_deadzone_compensation;
            SliderPower.Value = profileCurrent.steering_power;
            SliderSteeringAngle.Value = profileCurrent.steering_max_angle;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void SliderSteeringAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_max_angle = (float)SliderSteeringAngle.Value;
        }

        private void SliderPower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_power = (float)SliderPower.Value;
        }

        private void SliderDeadzoneAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_deadzone = (float)SliderDeadzoneAngle.Value;
        }

        private void SliderDeadzoneCompensation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_deadzone_compensation = (float)SliderDeadzoneCompensation.Value;
        }
    }
}
