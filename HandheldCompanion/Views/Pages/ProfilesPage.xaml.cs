using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Microsoft.Win32;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using GamepadButtonFlagsExt = ControllerCommon.Utils.GamepadButtonFlagsExt;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for Profiles.xaml
    /// </summary>
    public partial class ProfilesPage : Page
    {
        private Profile currentProfile;

        private Dictionary<GamepadButtonFlagsExt, CheckBox> activators = new();

        public ProfilesPage()
        {
            InitializeComponent();
        }

        public ProfilesPage(string Tag) : this()
        {
            this.Tag = Tag;

            MainWindow.pipeClient.ServerMessage += OnServerMessage;

            MainWindow.profileManager.Deleted += ProfileDeleted;
            MainWindow.profileManager.Updated += ProfileUpdated;
            MainWindow.profileManager.Ready += ProfileLoaded;

            // draw gamepad activators
            foreach (GamepadButtonFlagsExt button in (GamepadButtonFlagsExt[])Enum.GetValues(typeof(GamepadButtonFlagsExt)))
            {
                // create panel
                SimpleStackPanel panel = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // create icon
                FontIcon icon = new FontIcon() { Glyph = "" };
                icon.Glyph = InputUtils.GamepadButtonToGlyph(button);

                if (icon.Glyph != "")
                    panel.Children.Add(icon);

                // create textblock
                string description = EnumUtils.GetDescriptionFromEnumValue(button);
                TextBlock text = new TextBlock() { Text = description };
                panel.Children.Add(text);

                // create checkbox
                CheckBox checkbox = new CheckBox() { Tag = button, Content = panel, Width = 170 };
                cB_Buttons.Children.Add(checkbox);
                activators.Add(button, checkbox);
            }

            // draw input modes
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

            // draw output modes
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

        private void OnServerMessage(object sender, PipeMessage e)
        {
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            MainWindow.pipeClient.ServerMessage -= OnServerMessage;
        }

        #region UI
        public void ProfileUpdated(Profile profile, bool backgroundtask, bool isCurrent)
        {
            this.Dispatcher.Invoke(async () =>
            {
                int idx = -1;
                foreach (Profile pr in cB_Profiles.Items)
                {
                    if (pr.executable == profile.executable)
                    {
                        idx = cB_Profiles.Items.IndexOf(pr);
                        break;
                    }
                }

                if (idx != -1)
                    cB_Profiles.Items[idx] = profile;
                else
                    cB_Profiles.Items.Add(profile);

                cB_Profiles.SelectedItem = profile;
            });
        }

        public void ProfileDeleted(Profile profile)
        {
            this.Dispatcher.Invoke(() =>
            {
                int idx = -1;
                foreach (Profile pr in cB_Profiles.Items)
                    if (pr.executable == profile.executable)
                    {
                        idx = cB_Profiles.Items.IndexOf(pr);
                        break;
                    }
                cB_Profiles.Items.RemoveAt(idx);
            });
        }

        private void ProfileLoaded()
        {
            cB_Profiles.SelectedItem = MainWindow.profileManager.GetDefault();
        }
        #endregion

        private async void b_CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var path = openFileDialog.FileName;
                    var folder = Path.GetDirectoryName(path);

                    var file = openFileDialog.SafeFileName;
                    var ext = Path.GetExtension(file);

                    switch (ext)
                    {
                        default:
                        case ".exe":
                            break;
                        case ".xml":
                            try
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.Load(path);

                                XmlNodeList Applications = doc.GetElementsByTagName("Applications");
                                foreach (XmlNode node in Applications)
                                {
                                    foreach (XmlNode child in node.ChildNodes)
                                    {
                                        if (child.Name.Equals("Application"))
                                        {
                                            if (child.Attributes != null)
                                            {
                                                foreach (XmlAttribute attribute in child.Attributes)
                                                {
                                                    switch (attribute.Name)
                                                    {
                                                        case "Executable":
                                                            path = Path.Combine(folder, attribute.InnerText);
                                                            file = Path.GetFileName(path);
                                                            break;
                                                    }
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogError(ex.Message, true);
                            }
                            break;
                    }

                    Profile profile = new Profile(path);

                    bool exists = false;

                    if (MainWindow.profileManager.Contains(profile))
                    {
                        Task<ContentDialogResult> result = Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_AreYouSureOverwrite1} \"{profile.name}\"?",
                                                                            $"{Properties.Resources.ProfilesPage_AreYouSureOverwrite2}",
                                                                            ContentDialogButton.Primary,
                                                                            $"{Properties.Resources.ProfilesPage_Cancel}",
                                                                            $"{Properties.Resources.ProfilesPage_Yes}");
                        await result; // sync call

                        switch (result.Result)
                        {
                            case ContentDialogResult.Primary:
                                exists = false;
                                break;
                            default:
                                exists = true;
                                break;
                        }
                    }

                    if (!exists)
                    {
                        MainWindow.profileManager.UpdateOrCreateProfile(profile, false);
                        MainWindow.profileManager.SerializeProfile(profile);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError(ex.Message);
                }
            }
        }

        private void b_AdditionalSettings_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null)
                return;

            Page page;
            switch ((Input)cB_Input.SelectedIndex)
            {
                default:
                case Input.JoystickCamera:
                case Input.PlayerSpace:
                    page = new ProfileSettingsMode0("ProfileSettingsMode0", currentProfile);
                    break;
                case Input.JoystickSteering:
                    page = new ProfileSettingsMode1("ProfileSettingsMode1", currentProfile);
                    break;
            }
            MainWindow.GetDefault().NavView_Navigate(page);
        }

        private void cB_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_Profiles.SelectedItem == null)
                return;

            currentProfile = (Profile)cB_Profiles.SelectedItem;
            DrawProfile();
        }

        private void DrawProfile()
        {
            if (currentProfile == null)
                return;

            Dispatcher.BeginInvoke(() =>
            {
                // enable all expanders
                ProfileDetails.IsEnabled = true;
                MotionSettings.IsEnabled = true;
                UniversalSettings.IsEnabled = true;

                // disable button if is default profile
                b_DeleteProfile.IsEnabled = !currentProfile.isDefault;
                // prevent user from renaming default profile
                tB_ProfileName.IsEnabled = !currentProfile.isDefault;
                // prevent user from setting power settings on default profile
                PowerSettings.IsEnabled = !currentProfile.isDefault;
                // disable global settings on default profile
                GlobalSettings.IsEnabled = !currentProfile.isDefault;

                // Profile info
                tB_ProfileName.Text = currentProfile.name;
                tB_ProfilePath.Text = currentProfile.fullpath;
                Toggle_EnableProfile.IsOn = currentProfile.isEnabled;

                // Global settings
                cB_Whitelist.IsChecked = currentProfile.whitelisted;
                cB_Wrapper.IsChecked = currentProfile.use_wrapper;

                // Motion control settings
                tb_ProfileGyroValue.Value = currentProfile.gyrometer;
                tb_ProfileAcceleroValue.Value = currentProfile.accelerometer;

                cB_GyroSteering.SelectedIndex = currentProfile.steering;
                cB_InvertHorizontal.IsChecked = currentProfile.inverthorizontal;
                cB_InvertVertical.IsChecked = currentProfile.invertvertical;

                // Power settings
                TDPToggle.IsOn = currentProfile.TDP_override;

                // Sustained TDP settings (slow, stapm, long)
                double[] TDP = currentProfile.TDP_value != null ? currentProfile.TDP_value : MainWindow.handheldDevice.nTDP;
                TDPSlider.Value = TDP[0];

                // UMC settings
                Toggle_UniversalMotion.IsOn = currentProfile.umc_enabled;
                cB_Input.SelectedIndex = (int)currentProfile.umc_input;
                cB_Output.SelectedIndex = (int)currentProfile.umc_output;
                tb_ProfileAntiDeadzone.Value = currentProfile.antideadzone;

                foreach (GamepadButtonFlagsExt button in (GamepadButtonFlagsExt[])Enum.GetValues(typeof(GamepadButtonFlagsExt)))
                    if (currentProfile.umc_trigger.HasFlag(button))
                        activators[button].IsChecked = true;
                    else
                        activators[button].IsChecked = false;

                // display warnings
                ProfileErrorCode currentError = currentProfile.error;
                if (currentProfile.isRunning)
                    currentError = ProfileErrorCode.IsRunning;

                switch (currentError)
                {
                    default:
                    case ProfileErrorCode.None:
                        WarningBorder.Visibility = Visibility.Collapsed;
                        cB_Whitelist.IsEnabled = true;
                        cB_Wrapper.IsEnabled = true;
                        break;

                    case ProfileErrorCode.MissingExecutable:
                    case ProfileErrorCode.MissingPath:
                    case ProfileErrorCode.MissingPermission:
                    case ProfileErrorCode.IsDefault:
                        WarningBorder.Visibility = Visibility.Visible;
                        WarningContent.Text = EnumUtils.GetDescriptionFromEnumValue(currentError);
                        cB_Whitelist.IsEnabled = false;     // you can't whitelist an application without path
                        cB_Wrapper.IsEnabled = false;       // you can't deploy wrapper on an application without path
                        break;

                    case ProfileErrorCode.IsRunning:
                        WarningBorder.Visibility = Visibility.Visible;
                        WarningContent.Text = EnumUtils.GetDescriptionFromEnumValue(currentError);
                        cB_Whitelist.IsEnabled = true; // you can't whitelist an application without path
                        cB_Wrapper.IsEnabled = false;   // you can't deploy wrapper on a running application
                        break;
                }
            });
        }

        private async void b_DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null)
                return;

            Task<ContentDialogResult> result = Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_AreYouSureDelete1} \"{currentProfile.name}\"?",
                                                                $"{Properties.Resources.ProfilesPage_AreYouSureDelete2}",
                                                                ContentDialogButton.Primary,
                                                                $"{Properties.Resources.ProfilesPage_Cancel}",
                                                                $"{Properties.Resources.ProfilesPage_Delete}");
            await result; // sync call

            switch (result.Result)
            {
                case ContentDialogResult.Primary:
                    MainWindow.profileManager.DeleteProfile(currentProfile);
                    cB_Profiles.SelectedIndex = 0;
                    break;
                default:
                    break;
            }
        }

        private void b_ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null)
                return;

            Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_ProfileUpdated1}",
                             $"{currentProfile.name} {Properties.Resources.ProfilesPage_ProfileUpdated2}",
                             ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");

            // Profile
            currentProfile.name = tB_ProfileName.Text;
            currentProfile.fullpath = tB_ProfilePath.Text;
            currentProfile.isEnabled = (bool)Toggle_EnableProfile.IsOn;

            // Global settings
            currentProfile.whitelisted = (bool)cB_Whitelist.IsChecked;
            currentProfile.use_wrapper = (bool)cB_Wrapper.IsChecked;

            // Motion control settings
            currentProfile.gyrometer = (float)tb_ProfileGyroValue.Value;
            currentProfile.accelerometer = (float)tb_ProfileAcceleroValue.Value;

            currentProfile.steering = cB_GyroSteering.SelectedIndex;
            currentProfile.invertvertical = (bool)cB_InvertVertical.IsChecked;
            currentProfile.inverthorizontal = (bool)cB_InvertHorizontal.IsChecked;

            // UMC settings
            currentProfile.umc_enabled = (bool)Toggle_UniversalMotion.IsOn;
            currentProfile.umc_input = (Input)cB_Input.SelectedIndex;
            currentProfile.umc_output = (Output)cB_Output.SelectedIndex;
            currentProfile.antideadzone = (float)tb_ProfileAntiDeadzone.Value;

            // Power settings
            currentProfile.TDP_override = (bool)TDPToggle.IsOn;
            currentProfile.TDP_value[0] = (int)TDPSlider.Value;

            currentProfile.umc_trigger = 0;

            foreach (GamepadButtonFlagsExt button in (GamepadButtonFlagsExt[])Enum.GetValues(typeof(GamepadButtonFlagsExt)))
                if ((bool)activators[button].IsChecked)
                    currentProfile.umc_trigger |= button;

            MainWindow.profileManager.UpdateOrCreateProfile(currentProfile, false);
            MainWindow.profileManager.SerializeProfile(currentProfile);
        }

        private void cB_Whitelist_Checked(object sender, RoutedEventArgs e)
        {
            // todo : move me to WPF
            UniversalSettings.IsEnabled = (bool)!cB_Whitelist.IsChecked;
        }

        private void cB_Overlay_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void cB_Wrapper_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void cB_EnableHook_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void cB_ExclusiveHook_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void Toggle_UniversalMotion_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null)
                return;

            cB_Whitelist.IsEnabled = !(bool)Toggle_UniversalMotion.IsOn && !currentProfile.isDefault;
        }

        private void Scrolllock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = false;
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void Toggle_EnableProfile_Toggled(object sender, RoutedEventArgs e)
        {
            // do something
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
                case Input.AutoRollYawSwap:
                    cB_Output.SelectedIndex = (int)Output.RightStick;
                    break;
                case Input.JoystickSteering:
                    cB_Output.SelectedIndex = (int)Output.LeftStick;
                    break;
            }

            Text_InputHint.Text = Profile.InputDescription[input];
        }

        private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // do something
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // do something
        }
    }
}
