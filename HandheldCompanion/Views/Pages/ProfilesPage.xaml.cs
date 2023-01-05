using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using Microsoft.Win32;
using ModernWpf.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for Profiles.xaml
    /// </summary>
    public partial class ProfilesPage : Page
    {
        private Profile currentProfile;
        private Hotkey ProfilesPageHotkey;

        ProfileSettingsMode0 page0 = new ProfileSettingsMode0("ProfileSettingsMode0");
        ProfileSettingsMode0 page1 = new ProfileSettingsMode0("ProfileSettingsMode1");

        public ProfilesPage()
        {
            InitializeComponent();
        }

        public ProfilesPage(string Tag) : this()
        {
            this.Tag = Tag;

            PipeClient.ServerMessage += OnServerMessage;

            ProfileManager.Deleted += ProfileDeleted;
            ProfileManager.Updated += ProfileUpdated;
            ProfileManager.Initialized += ProfileManagerLoaded;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            HotkeysManager.HotkeyCreated += TriggerCreated;
            InputsManager.TriggerUpdated += TriggerUpdated;

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

        public void SettingsManager_SettingValueChanged(string name, object value)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (name)
                {
                    case "ConfigurableTDPOverrideDown":
                        TDPBoostSlider.Minimum = TDPSustainedSlider.Minimum = (double)value;
                        break;
                    case "ConfigurableTDPOverrideUp":
                        TDPBoostSlider.Maximum = TDPSustainedSlider.Maximum = (double)value;
                        break;
                }
            });
        }

        private void OnServerMessage(PipeMessage e)
        {
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            PipeClient.ServerMessage -= OnServerMessage;
        }

        #region UI
        public void ProfileUpdated(Profile profile, ProfileUpdateSource source, bool isCurrent)
        {
            this.Dispatcher.Invoke(() =>
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

                cB_Profiles.Items.Refresh();
                cB_Profiles.SelectedItem = profile;
            });

            if (source == ProfileUpdateSource.Background)
                return;

            _ = Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_ProfileUpdated1}",
                             $"{currentProfile.name} {Properties.Resources.ProfilesPage_ProfileUpdated2}",
                             ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
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

        private void ProfileManagerLoaded()
        {
            this.Dispatcher.Invoke(() =>
            {
                cB_Profiles.SelectedItem = ProfileManager.GetDefault();
            });
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
                                            if (child.Attributes is not null)
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

                    // set default value(s)
                    profile.TDP_value = MainWindow.handheldDevice.nTDP;

                    bool exists = false;

                    if (ProfileManager.Contains(profile))
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
                        ProfileManager.UpdateOrCreateProfile(profile, ProfileUpdateSource.ProfilesPage);
                }
                catch (Exception ex)
                {
                    LogManager.LogError(ex.Message);
                }
            }
        }

        private void b_AdditionalSettings_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            switch ((Input)cB_Input.SelectedIndex)
            {
                default:
                case Input.JoystickCamera:
                case Input.PlayerSpace:
                    page0.Update(currentProfile);
                    MainWindow.NavView_Navigate(page0);
                    page1.Update(currentProfile);
                    break;
                case Input.JoystickSteering:
                    page1.Update(currentProfile);
                    MainWindow.NavView_Navigate(page1);
                    break;
            }
        }

        private void cB_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_Profiles.SelectedItem is null)
                return;

            // update current profile
            currentProfile = (Profile)cB_Profiles.SelectedItem;

            // todo: find a way to avoid a useless circle of drawing when profile was update from ProfilesPage
            DrawProfile();
        }

        private void DrawProfile()
        {
            if (currentProfile is null)
                return;

            Dispatcher.BeginInvoke(() =>
            {
                // enable all expanders
                ProfileDetails.IsEnabled = true;
                ControllerSettings.IsEnabled = true;
                MotionSettings.IsEnabled = true;
                UniversalSettings.IsEnabled = true;

                // disable button if is default profile or application is running
                b_DeleteProfile.IsEnabled = !currentProfile.isDefault && !currentProfile.isRunning;
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

                // Controller settings
                tb_ProfileAntiDeadzoneLeft.Value = currentProfile.antideadzoneL;
                tb_ProfileAntiDeadzoneRight.Value = currentProfile.antideadzoneR;

                // Motion control settings
                tb_ProfileGyroValue.Value = currentProfile.gyrometer;
                tb_ProfileAcceleroValue.Value = currentProfile.accelerometer;

                cB_GyroSteering.SelectedIndex = currentProfile.steering;
                cB_InvertHorizontal.IsChecked = currentProfile.inverthorizontal;
                cB_InvertVertical.IsChecked = currentProfile.invertvertical;

                // Sustained TDP settings (slow, stapm, long)
                double[] TDP = currentProfile.TDP_value is not null ? currentProfile.TDP_value : MainWindow.handheldDevice.nTDP;
                TDPSustainedSlider.Value = TDP[(int)PowerType.Slow];
                TDPBoostSlider.Value = TDP[(int)PowerType.Fast];

                TDPToggle.IsOn = currentProfile.TDP_override;

                // define slider(s) min and max values based on device specifications
                var TDPdown = SettingsManager.GetInt("ConfigurableTDPOverrideDown");
                TDPBoostSlider.Minimum = TDPSustainedSlider.Minimum = TDPdown;

                var TDPup = SettingsManager.GetInt("ConfigurableTDPOverrideUp");
                TDPBoostSlider.Maximum = TDPSustainedSlider.Maximum = TDPup;

                // UMC settings
                Toggle_UniversalMotion.IsOn = currentProfile.umc_enabled;
                cB_Input.SelectedIndex = (int)currentProfile.umc_input;
                cB_Output.SelectedIndex = (int)currentProfile.umc_output;
                tb_ProfileUMCAntiDeadzone.Value = currentProfile.umc_anti_deadzone;
                cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)currentProfile.umc_motion_defaultoffon;

                // todo: improve me ?
                ProfilesPageHotkey.inputsChord.GamepadButtons = currentProfile.umc_trigger;
                ProfilesPageHotkey.Refresh();

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

                    case ProfileErrorCode.IsRunning:
                    case ProfileErrorCode.MissingExecutable:
                    case ProfileErrorCode.MissingPath:
                    case ProfileErrorCode.MissingPermission:
                    case ProfileErrorCode.IsDefault:
                        WarningBorder.Visibility = Visibility.Visible;
                        WarningContent.Text = EnumUtils.GetDescriptionFromEnumValue(currentError);
                        cB_Whitelist.IsEnabled = false;     // you can't whitelist an application without path
                        cB_Wrapper.IsEnabled = false;       // you can't deploy wrapper on an application without path
                        break;
                }
            });
        }

        private async void b_DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
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
                    ProfileManager.DeleteProfile(currentProfile);
                    cB_Profiles.SelectedIndex = 0;
                    break;
                default:
                    break;
            }
        }

        private void b_ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            // Profile
            currentProfile.name = tB_ProfileName.Text;
            currentProfile.fullpath = tB_ProfilePath.Text;
            currentProfile.isEnabled = (bool)Toggle_EnableProfile.IsOn;

            // Global settings
            currentProfile.whitelisted = (bool)cB_Whitelist.IsChecked;
            currentProfile.use_wrapper = (bool)cB_Wrapper.IsChecked;

            // Controller settings
            currentProfile.antideadzoneL = (float)tb_ProfileAntiDeadzoneLeft.Value;
            currentProfile.antideadzoneR = (float)tb_ProfileAntiDeadzoneRight.Value;

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
            currentProfile.umc_anti_deadzone = (float)tb_ProfileUMCAntiDeadzone.Value;
            currentProfile.umc_motion_defaultoffon = (UMC_Motion_Default)cB_UMC_MotionDefaultOffOn.SelectedIndex;

            // Power settings
            currentProfile.TDP_value[0] = (int)TDPSustainedSlider.Value;
            currentProfile.TDP_value[1] = (int)TDPSustainedSlider.Value;
            currentProfile.TDP_value[2] = (int)TDPBoostSlider.Value;
            currentProfile.TDP_override = (bool)TDPToggle.IsOn;

            ProfileManager.UpdateOrCreateProfile(currentProfile, ProfileUpdateSource.ProfilesPage);
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
            if (currentProfile is null)
                return;

            cB_Whitelist.IsEnabled = !(bool)Toggle_UniversalMotion.IsOn && !currentProfile.isDefault;
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

        private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1)
                return;
        }

        private void TDPSustainedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // do something
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // do something
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void TriggerCreated(Hotkey hotkey)
        {
            switch (hotkey.inputsHotkey.Listener)
            {
                case "shortcutProfilesPage@":
                    {
                        Border hotkeyBorder = hotkey.GetHotkey();
                        if (hotkeyBorder is null || hotkeyBorder.Parent is not null)
                            return;

                        // pull hotkey
                        ProfilesPageHotkey = hotkey;

                        this.UMC_Activator.Children.Add(hotkeyBorder);
                    }
                    break;
            }
        }

        private void TriggerUpdated(string listener, InputsChord inputs, InputsManager.ListenerType type)
        {
            switch (listener)
            {
                case "shortcutProfilesPage@":
                case "shortcutProfilesPage@@":
                    currentProfile.umc_trigger = inputs.GamepadButtons;
                    break;
            }
        }
    }
}
