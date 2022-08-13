using ControllerCommon;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using ModernWpf.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickProfilesPage.xaml
    /// </summary>
    public partial class QuickProfilesPage : Page
    {
        private bool Initialized;

        private ProcessEx currentProcess;
        private Profile currentProfile;

        public QuickProfilesPage()
        {
            InitializeComponent();
            Initialized = true;

            MainWindow.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            MainWindow.profileManager.Updated += ProfileUpdated;
            MainWindow.profileManager.Deleted += ProfileDeleted;

            foreach (Input mode in (Input[])Enum.GetValues(typeof(Input)))
            {
                // create panel
                SimpleStackPanel panel = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // create icon
                FontIcon icon = new FontIcon() { Glyph = "" };

                switch (mode)
                {
                    default:
                    case Input.PlayerSpace:
                        icon.Glyph = "\uF119";
                        break;
                    case Input.JoystickCamera:
                        icon.Glyph = "\uE714";
                        break;
                    case Input.AutoRollYawSwap:
                        icon.Glyph = "\uE7F8";
                        break;
                    case Input.JoystickSteering:
                        icon.Glyph = "\uEC47";
                        break;
                }

                if (icon.Glyph != "")
                    panel.Children.Add(icon);

                // create textblock
                string description = EnumUtils.GetDescriptionFromEnumValue(mode);
                TextBlock text = new TextBlock() { Text = description };
                panel.Children.Add(text);

                cB_Input.Items.Add(panel);
            }

            foreach (Output mode in (Output[])Enum.GetValues(typeof(Output)))
            {
                // create panel
                SimpleStackPanel panel = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // create icon
                FontIcon icon = new FontIcon() { Glyph = "" };

                switch (mode)
                {
                    default:
                    case Output.RightStick:
                        icon.Glyph = "\uF109";
                        break;
                    case Output.LeftStick:
                        icon.Glyph = "\uF108";
                        break;
                }

                if (icon.Glyph != "")
                    panel.Children.Add(icon);

                // create textblock
                string description = EnumUtils.GetDescriptionFromEnumValue(mode);
                TextBlock text = new TextBlock() { Text = description };
                panel.Children.Add(text);

                cB_Output.Items.Add(panel);
            }

            // define slider(s) min and max values based on device specifications
            var TDPdown = Properties.Settings.Default.ConfigurableTDPOverride ? Properties.Settings.Default.ConfigurableTDPOverrideDown : MainWindow.handheldDevice.cTDP[0];
            var TDPup = Properties.Settings.Default.ConfigurableTDPOverride ? Properties.Settings.Default.ConfigurableTDPOverrideUp : MainWindow.handheldDevice.cTDP[1];
            TDPBoostSlider.Minimum = TDPSustainedSlider.Minimum = TDPdown;
            TDPBoostSlider.Maximum = TDPSustainedSlider.Maximum = TDPup;
        }

        public void SettingsPage_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "configurabletdp_down":
                    TDPBoostSlider.Minimum = TDPSustainedSlider.Minimum = (double)value;
                    break;
                case "configurabletdp_up":
                    TDPBoostSlider.Maximum = TDPSustainedSlider.Maximum = (double)value;
                    break;
            }
        }

        private void ProfileDeleted(Profile profile)
        {
            if (currentProfile is null)
                return;

            if (profile.executable == currentProfile.executable)
            {
                currentProcess = null;
                currentProfile = null;
                ProfileUpdated(profile, false, true);
            }
        }

        private void ProfileUpdated(Profile profile, bool backgroundtask, bool isCurrent)
        {
            if (!isCurrent || profile.isDefault)
                return;

            this.Dispatcher.Invoke(() =>
            {
                b_CreateProfile.Visibility = Visibility.Collapsed;

                b_UpdateProfile.Visibility = Visibility.Visible;
                GridProfile.Visibility = Visibility.Visible;

                ProfileToggle.IsEnabled = true;
                ProfileToggle.IsOn = profile.isEnabled;
                UMCToggle.IsOn = profile.umc_enabled;
                cB_Input.SelectedIndex = (int)profile.umc_input;
                cB_Output.SelectedIndex = (int)profile.umc_output;

                // Sustained TDP settings (slow, stapm, long)
                double[] TDP = profile.TDP_value != null ? profile.TDP_value : MainWindow.handheldDevice.nTDP;
                TDPSustainedSlider.Value = TDP[0];
                TDPBoostSlider.Value = TDP[2];

                TDPToggle.IsOn = profile.TDP_override;

                // Sensivity settings
                SliderSensivity.Value = profile.aiming_sensivity;

                if (backgroundtask)
                    return;

                _ = Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_ProfileUpdated1}",
                                    $"{profile.name} {Properties.Resources.ProfilesPage_ProfileUpdated2}",
                                    ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
            });
        }

        private void ProcessManager_ForegroundChanged(ProcessEx processEx, bool display)
        {
            if (!display)
                return;

            currentProcess = processEx;
            currentProfile = MainWindow.profileManager.GetProfileFromExec(currentProcess.Name);

            this.Dispatcher.Invoke(() =>
            {
                ProcessName.Text = currentProcess.Name;
                ProcessPath.Text = currentProcess.Path;

                if (currentProfile is null)
                {
                    b_CreateProfile.Visibility = Visibility.Visible;

                    b_UpdateProfile.Visibility = Visibility.Collapsed;
                    GridProfile.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void Scrolllock_MouseEnter(object sender, MouseEventArgs e)
        {
            QuickTools.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, MouseEventArgs e)
        {
            QuickTools.scrollLock = false;
        }

        private void UpdateProfile()
        {
            if (currentProfile is null)
                return;

            MainWindow.profileManager.UpdateOrCreateProfile(currentProfile, false);
            MainWindow.profileManager.SerializeProfile(currentProfile);
        }

        private void ProfileToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            currentProfile.isEnabled = ProfileToggle.IsOn;
        }

        private void UMCToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            currentProfile.umc_enabled = UMCToggle.IsOn;
        }

        private void cB_Input_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_Input.SelectedIndex == -1)
                return;

            Input input = (Input)cB_Input.SelectedIndex;

            // Check which input type is selected and automatically
            // set the most used output joystick accordingly.
            switch (input)
            {
                case Input.PlayerSpace:
                case Input.JoystickCamera:
                    cB_Output.SelectedIndex = (int)Output.RightStick;
                    GridSensivity.Visibility = Visibility.Visible;
                    break;
                case Input.JoystickSteering:
                    cB_Output.SelectedIndex = (int)Output.LeftStick;
                    GridSensivity.Visibility = Visibility.Collapsed;
                    break;
            }

            Text_InputHint.Text = Profile.InputDescription[input];

            if (currentProfile is null)
                return;

            currentProfile.umc_input = (Input)cB_Input.SelectedIndex;
        }

        private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentProfile is null)
                return;

            currentProfile.umc_output = (Output)cB_Output.SelectedIndex;
        }

        private void b_CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProcess is null)
                return;

            // create profile
            currentProfile = new Profile(currentProcess.Path);
            currentProfile.TDP_value = MainWindow.handheldDevice.nTDP;

            // update current profile
            MainWindow.profileManager.currentProfile = currentProfile;

            UpdateProfile();
        }

        private void b_UpdateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProcess is null)
                return;

            UpdateProfile();
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            // Power settings
            currentProfile.TDP_override = (bool)TDPToggle.IsOn;
        }

        private void TDPSustainedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            // Power settings
            currentProfile.TDP_value[0] = (int)TDPSustainedSlider.Value;
            currentProfile.TDP_value[1] = (int)TDPSustainedSlider.Value;
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            // Power settings
            currentProfile.TDP_value[2] = (int)TDPBoostSlider.Value;
        }

        private void SliderSensivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            // Sensivity settings
            currentProfile.aiming_sensivity = (float)SliderSensivity.Value;
        }
    }
}
