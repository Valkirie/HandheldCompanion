using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CoreAudio;
using System;
using ControllerCommon.Utils;
using ControllerCommon;
using ModernWpf.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickProfilesPage.xaml
    /// </summary>
    public partial class QuickProfilesPage : Page
    {
        private bool Initialized;
        private bool IgnoreMe;
        private Profile currentProfile;

        public QuickProfilesPage()
        {
            InitializeComponent();
            Initialized = true;

            MainWindow.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            MainWindow.profileManager.Updated += ProfileUpdated;

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
        }

        private void ProfileUpdated(Profile profile, bool backgroundtask)
        {
            if (backgroundtask)
                return;

            if (currentProfile is null)
                return;

            if (profile.executable != currentProfile.executable)
                return;

            this.Dispatcher.Invoke(() =>
            {
                IgnoreMe = true;

                ProfileName.Text = currentProfile.name;
                ProfilePath.Text = currentProfile.path;

                ProfileToggle.IsEnabled = true;
                ProfileToggle.IsOn = currentProfile.isEnabled;
                UMCToggle.IsOn = currentProfile.umc_enabled;
                cB_Input.SelectedIndex = (int)currentProfile.umc_input;
                cB_Output.SelectedIndex = (int)currentProfile.umc_output;

                IgnoreMe = false;
            });
        }

        private void ProcessManager_ForegroundChanged(ProcessEx processEx)
        {
            currentProfile = MainWindow.profileManager.GetProfileFromExec(processEx.Name);

            if (currentProfile == null)
                currentProfile = new Profile(processEx.Path);

            ProfileUpdated(currentProfile, false);
        }

        private void Scrolllock_MouseEnter(object sender, MouseEventArgs e)
        {
            QuickTools.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, MouseEventArgs e)
        {
            QuickTools.scrollLock = false;
        }

        private void SaveProfile()
        {
            if (currentProfile is null || IgnoreMe)
                return;

            MainWindow.profileManager.UpdateOrCreateProfile(currentProfile, false);
            MainWindow.profileManager.SerializeProfile(currentProfile);

            // inform service
            MainWindow.pipeClient.SendMessage(new PipeClientProfile { profile = currentProfile });
        }

        private void ProfileToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null || IgnoreMe)
                return;

            currentProfile.isEnabled = ProfileToggle.IsOn;
            SaveProfile();
        }

        private void UMCToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null || IgnoreMe)
                return;

            currentProfile.umc_enabled = UMCToggle.IsOn;
            SaveProfile();
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
                    break;
                case Input.JoystickSteering:
                    cB_Output.SelectedIndex = (int)Output.LeftStick;
                    break;
            }

            Text_InputHint.Text = Profile.InputDescription[input];

            if (currentProfile is null || IgnoreMe)
                return;

            currentProfile.umc_input = (Input)cB_Input.SelectedIndex;
            SaveProfile();
        }

        private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentProfile is null || IgnoreMe)
                return;

            currentProfile.umc_output = (Output)cB_Output.SelectedIndex;
            SaveProfile();
        }
    }
}
