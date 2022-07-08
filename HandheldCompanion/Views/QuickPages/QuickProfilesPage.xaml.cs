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

        public QuickProfilesPage()
        {
            InitializeComponent();
            Initialized = true;

            MainWindow.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;

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

        Profile profile;
        private void ProcessManager_ForegroundChanged(ProcessEx processEx)
        {
            profile = MainWindow.profileManager.GetProfileFromExec(processEx.Name);

            if (profile == null)
                profile = new Profile(processEx.Path);

            this.Dispatcher.Invoke(() =>
            {
                IgnoreMe = true;

                ProfileName.Text = profile.name;
                ProfilePath.Text = profile.path;

                ProfileToggle.IsEnabled = true;
                ProfileToggle.IsOn = profile.isEnabled;
                UMCToggle.IsOn = profile.umc_enabled;
                cB_Input.SelectedIndex = (int)profile.umc_input;
                cB_Output.SelectedIndex = (int)profile.umc_output;

                IgnoreMe = false;
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

        private void SaveProfile()
        {
            if (profile is null || IgnoreMe)
                return;

            MainWindow.profileManager.UpdateOrCreateProfile(profile, false);
            MainWindow.profileManager.SerializeProfile(profile);

            // inform service
            MainWindow.pipeClient.SendMessage(new PipeClientProfile { profile = profile });
        }

        private void ProfileToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (profile is null || IgnoreMe)
                return;

            profile.isEnabled = ProfileToggle.IsOn;
            SaveProfile();
        }

        private void UMCToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (profile is null || IgnoreMe)
                return;

            profile.umc_enabled = UMCToggle.IsOn;
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

            if (profile is null || IgnoreMe)
                return;

            profile.umc_input = (Input)cB_Input.SelectedIndex;
            SaveProfile();
        }

        private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profile is null || IgnoreMe)
                return;

            profile.umc_output = (Output)cB_Output.SelectedIndex;
            SaveProfile();
        }
    }
}
