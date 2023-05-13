using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Views.Pages.Profiles;
using Microsoft.Win32;
using ModernWpf.Controls;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Layout = ControllerCommon.Layout;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for Profiles.xaml
    /// </summary>
    public partial class ProfilesPage : Page
    {
        public static Profile currentProfile;
        private Hotkey ProfilesPageHotkey = new(60);

        private SettingsMode0 page0 = new SettingsMode0("SettingsMode0");
        private SettingsMode1 page1 = new SettingsMode1("SettingsMode1");

        public ProfilesPage()
        {
            InitializeComponent();
        }

        public ProfilesPage(string Tag) : this()
        {
            this.Tag = Tag;

            ProfileManager.Deleted += ProfileDeleted;
            ProfileManager.Updated += ProfileUpdated;
            ProfileManager.Applied += ProfileApplied;

            ProfileManager.Initialized += ProfileManagerLoaded;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

            HotkeysManager.HotkeyCreated += TriggerCreated;
            InputsManager.TriggerUpdated += TriggerUpdated;

            MainWindow.performanceManager.ProcessorStatusChanged += PowerManager_StatusChanged;

            // draw input modes
            foreach (MotionInput mode in (MotionInput[])Enum.GetValues(typeof(MotionInput)))
            {
                // create panel
                SimpleStackPanel panel = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // create icon
                FontIcon icon = new FontIcon() { Glyph = "" };

                switch (mode)
                {
                    default:
                    case MotionInput.PlayerSpace:
                        icon.Glyph = "\uF119";
                        break;
                    case MotionInput.JoystickCamera:
                        icon.Glyph = "\uE714";
                        break;
                    case MotionInput.AutoRollYawSwap:
                        icon.Glyph = "\uE7F8";
                        break;
                    case MotionInput.JoystickSteering:
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
            foreach (MotionOutput mode in (MotionOutput[])Enum.GetValues(typeof(MotionOutput)))
            {
                // create panel
                SimpleStackPanel panel = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // create icon
                FontIcon icon = new FontIcon() { Glyph = "" };

                switch (mode)
                {
                    default:
                    case MotionOutput.RightStick:
                        icon.Glyph = "\uF109";
                        break;
                    case MotionOutput.LeftStick:
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

            // auto-sort
            cB_Profiles.Items.SortDescriptions.Add(new SortDescription("", ListSortDirection.Descending));

            // device settings
            GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
            GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];
        }

        private void PowerManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PowerSettings.IsEnabled = CanChangeTDP;
                // StackProfileGPU.IsEnabled = CanChangeGPU;
            });
        }

        public void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "ConfigurableTDPOverrideDown":
                        TDPSlider.Minimum = (double)value;
                        break;
                    case "ConfigurableTDPOverrideUp":
                        TDPSlider.Maximum = (double)value;
                        break;
                }
            });
        }

        private void DesktopManager_DisplaySettingsChanged(ScreenResolution resolution)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                ScreenFrequency screenFrequency = SystemManager.GetDesktopScreen().GetFrequency();

                FramerateQuarter.Text = Convert.ToString(screenFrequency.GetFrequency(Frequency.Quarter));
                FramerateThird.Text = Convert.ToString(screenFrequency.GetFrequency(Frequency.Third));
                FramerateHalf.Text = Convert.ToString(screenFrequency.GetFrequency(Frequency.Half));
                FramerateFull.Text = Convert.ToString(screenFrequency.GetFrequency(Frequency.Full));
            });
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        #region UI
        private void ProfileApplied(Profile profile)
        {
            if (profile.Default)
                return;

            ProfileUpdated(profile, ProfileUpdateSource.Background, true);
        }

        public void ProfileUpdated(Profile profile, ProfileUpdateSource source, bool isCurrent)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                int idx = -1;
                foreach (Profile pr in cB_Profiles.Items)
                {
                    bool isCurrent = pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase);
                    if (isCurrent)
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

            switch (source)
            {
                case ProfileUpdateSource.Background:
                case ProfileUpdateSource.Creation:
                case ProfileUpdateSource.Serializer:
                case ProfileUpdateSource.QuickProfilesPage:
                    return;
            }

            _ = Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_ProfileUpdated1}",
                             $"{profile.Name} {Properties.Resources.ProfilesPage_ProfileUpdated2}",
                             ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
        }

        public void ProfileDeleted(Profile profile)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                int idx = -1;
                foreach (Profile pr in cB_Profiles.Items)
                {
                    bool isCurrent = pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase);
                    if (isCurrent)
                    {
                        idx = cB_Profiles.Items.IndexOf(pr);
                        break;
                    }
                }
                cB_Profiles.Items.RemoveAt(idx);
            });
        }

        private void ProfileManagerLoaded()
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
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
                    profile.Layout = LayoutTemplate.DefaultLayout.Layout.Clone() as Layout;
                    profile.LayoutTitle = LayoutTemplate.DefaultLayout.Name;
                    profile.TDPOverrideValues = MainWindow.CurrentDevice.nTDP;

                    bool exists = false;

                    // check on path rather than profile
                    if (ProfileManager.Contains(path))
                    {
                        Task<ContentDialogResult> result = Dialog.ShowAsync(
                            String.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite1, profile.Name),
                            String.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite2, profile.Name),
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
                        ProfileManager.UpdateOrCreateProfile(profile, ProfileUpdateSource.Creation);
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

            switch ((MotionInput)cB_Input.SelectedIndex)
            {
                default:
                case MotionInput.JoystickCamera:
                case MotionInput.PlayerSpace:
                    page0.SetProfile();
                    MainWindow.NavView_Navigate(page0);
                    break;
                case MotionInput.JoystickSteering:
                    page1.SetProfile();
                    MainWindow.NavView_Navigate(page1);
                    break;
            }
        }

        private void cB_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_Profiles.SelectedItem is null)
                return;

            // update current profile
            Profile profile = (Profile)cB_Profiles.SelectedItem;
            currentProfile = profile.Clone() as Profile;

            // todo: find a way to avoid a useless circle of drawing when profile was update from ProfilesPage
            DrawProfile();
        }

        private void DrawProfile()
        {
            if (currentProfile is null)
                return;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // enable all expanders
                // todo: move me to XAML ?
                ProfileDetails.IsEnabled = true;
                MotionSettings.IsEnabled = true;
                UniversalSettings.IsEnabled = true;

                // disable button if is default profile or application is running
                b_DeleteProfile.IsEnabled = !currentProfile.ErrorCode.HasFlag(ProfileErrorCode.Default & ProfileErrorCode.Running);

                // prevent user from renaming default profile
                tB_ProfileName.IsEnabled = !currentProfile.Default;
                // disable global settings on default profile
                GlobalSettings.IsEnabled = !currentProfile.Default;
                // prevent user from disabling default profile
                Toggle_EnableProfile.IsEnabled = !currentProfile.Default;
                // prevent user from disabling default profile layout
                Toggle_ControllerLayout.IsEnabled = !currentProfile.Default;

                // Profile info
                tB_ProfileName.Text = currentProfile.Name;
                tB_ProfilePath.Text = currentProfile.Path;
                Toggle_EnableProfile.IsOn = currentProfile.Enabled;

                // Global settings
                cB_Whitelist.IsChecked = currentProfile.Whitelisted;
                cB_Wrapper.IsChecked = currentProfile.XInputPlus;

                // Motion control settings
                tb_ProfileGyroValue.Value = currentProfile.GyrometerMultiplier;
                tb_ProfileAcceleroValue.Value = currentProfile.AccelerometerMultiplier;

                cB_GyroSteering.SelectedIndex = currentProfile.SteeringAxis;
                cB_InvertHorizontal.IsChecked = currentProfile.MotionInvertHorizontal;
                cB_InvertVertical.IsChecked = currentProfile.MotionInvertVertical;

                // Sustained TDP settings (slow, stapm, long)
                TDPToggle.IsOn = currentProfile.TDPOverrideEnabled;
                double[] TDP = currentProfile.TDPOverrideValues is not null ? currentProfile.TDPOverrideValues : MainWindow.CurrentDevice.nTDP;
                TDPSlider.Value = TDP[(int)PowerType.Slow];

                // define slider(s) min and max values based on device specifications
                TDPSlider.Minimum = SettingsManager.GetInt("ConfigurableTDPOverrideDown");
                TDPSlider.Maximum = SettingsManager.GetInt("ConfigurableTDPOverrideUp");

                // Automatic TDP
                AutoTDPToggle.IsOn = currentProfile.AutoTDPEnabled;
                AutoTDPSlider.Value = (int)currentProfile.AutoTDPRequestedFPS;

                // GPU Clock control
                GPUToggle.IsOn = currentProfile.GPUOverrideEnabled;
                GPUSlider.Value = currentProfile.GPUOverrideValue != 0 ? currentProfile.GPUOverrideValue : (255 * 50);

                // Framerate limit
                FramerateToggle.IsOn = currentProfile.FramerateEnabled;
                FramerateSlider.Value = currentProfile.FramerateValue;

                // Layout settings
                Toggle_ControllerLayout.IsOn = currentProfile.LayoutEnabled;
                
                // UMC settings
                Toggle_UniversalMotion.IsOn = currentProfile.MotionEnabled;
                cB_Input.SelectedIndex = (int)currentProfile.MotionInput;
                cB_Output.SelectedIndex = (int)currentProfile.MotionOutput;
                tb_ProfileUMCAntiDeadzone.Value = currentProfile.MotionAntiDeadzone;
                cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)currentProfile.MotionMode;

                // todo: improve me ?
                ProfilesPageHotkey.inputsChord.State = currentProfile.MotionTrigger.Clone() as ButtonState;
                ProfilesPageHotkey.DrawInput();

                // display warnings
                switch (currentProfile.ErrorCode)
                {
                    default:
                    case ProfileErrorCode.None:
                        WarningBorder.Visibility = Visibility.Collapsed;
                        cB_Whitelist.IsEnabled = true;
                        cB_Wrapper.IsEnabled = true;
                        break;

                    case ProfileErrorCode.Running:
                    case ProfileErrorCode.MissingExecutable:
                    case ProfileErrorCode.MissingPath:
                    case ProfileErrorCode.MissingPermission:
                    case ProfileErrorCode.Default:
                        WarningBorder.Visibility = Visibility.Visible;
                        WarningContent.Text = EnumUtils.GetDescriptionFromEnumValue(currentProfile.ErrorCode);
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

            Task<ContentDialogResult> result = Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_AreYouSureDelete1} \"{currentProfile.Name}\"?",
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
            currentProfile.Name = tB_ProfileName.Text;
            currentProfile.Path = tB_ProfilePath.Text;
            currentProfile.Enabled = (bool)Toggle_EnableProfile.IsOn;

            // Global settings
            currentProfile.Whitelisted = (bool)cB_Whitelist.IsChecked;
            currentProfile.XInputPlus = (bool)cB_Wrapper.IsChecked;

            // Motion control settings
            currentProfile.GyrometerMultiplier = (float)tb_ProfileGyroValue.Value;
            currentProfile.AccelerometerMultiplier = (float)tb_ProfileAcceleroValue.Value;

            currentProfile.SteeringAxis = cB_GyroSteering.SelectedIndex;
            currentProfile.MotionInvertVertical = (bool)cB_InvertVertical.IsChecked;
            currentProfile.MotionInvertHorizontal = (bool)cB_InvertHorizontal.IsChecked;

            // UMC settings
            currentProfile.MotionEnabled = (bool)Toggle_UniversalMotion.IsOn;
            currentProfile.MotionInput = (MotionInput)cB_Input.SelectedIndex;
            currentProfile.MotionOutput = (MotionOutput)cB_Output.SelectedIndex;
            currentProfile.MotionAntiDeadzone = (float)tb_ProfileUMCAntiDeadzone.Value;
            currentProfile.MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;

            // Power settings
            currentProfile.TDPOverrideValues[(int)PowerType.Slow] = (int)TDPSlider.Value;
            currentProfile.TDPOverrideValues[(int)PowerType.Stapm] = (int)TDPSlider.Value;
            currentProfile.TDPOverrideValues[(int)PowerType.Fast] = (int)TDPSlider.Value;
            currentProfile.TDPOverrideEnabled = (bool)TDPToggle.IsOn;

            currentProfile.AutoTDPEnabled = (bool)AutoTDPToggle.IsOn;
            currentProfile.AutoTDPRequestedFPS = (int)AutoTDPSlider.Value;

            currentProfile.GPUOverrideEnabled = (bool)GPUToggle.IsOn;
            currentProfile.GPUOverrideValue = (int)GPUSlider.Value;

            currentProfile.FramerateEnabled = (bool)FramerateToggle.IsOn;
            currentProfile.FramerateValue = (int)FramerateSlider.Value;

            // Layout settings
            currentProfile.LayoutEnabled = (bool)Toggle_ControllerLayout.IsOn;

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

            cB_Whitelist.IsEnabled = !(bool)Toggle_UniversalMotion.IsOn && !currentProfile.Default;
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

            MotionInput input = (MotionInput)cB_Input.SelectedIndex;

            // Check which input type is selected and automatically
            // set the most used output joystick accordingly.
            switch (input)
            {
                case MotionInput.PlayerSpace:
                case MotionInput.JoystickCamera:
                case MotionInput.AutoRollYawSwap:
                    cB_Output.SelectedIndex = (int)MotionOutput.RightStick;
                    break;
                case MotionInput.JoystickSteering:
                    cB_Output.SelectedIndex = (int)MotionOutput.LeftStick;
                    break;
            }

            Text_InputHint.Text = Profile.InputDescription[input];
        }

        private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1)
                return;
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // TDP and AutoTDP are mutually exclusive
            bool toggled = TDPToggle.IsOn;
            if (toggled)
                AutoTDPToggle.IsOn = false;
        }

        private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!TDPSlider.IsInitialized)
                return;
        }

        private void FramerateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // Do something
        }

        private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!FramerateSlider.IsInitialized)
                return;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                int value = (int)FramerateSlider.Value;

                foreach (TextBlock tb in FramerateModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)FramerateModeGrid.Children[value];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });
        }

        private void AutoTDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // TDP and AutoTDP are mutually exclusive
            bool toggled = AutoTDPToggle.IsOn;
            if (toggled)
                TDPToggle.IsOn = false;            
        }

        private void AutoTDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!AutoTDPSlider.IsInitialized)
                return;
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // Do something
        }

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Do something
        }

        private void TriggerCreated(Hotkey hotkey)
        {
            switch (hotkey.inputsHotkey.Listener)
            {
                case "shortcutProfilesPage@":
                    {
                        HotkeyControl hotkeyBorder = hotkey.GetControl();
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
                    currentProfile.MotionTrigger = inputs.State.Clone() as ButtonState;
                    break;
            }
        }

        private void ControllerSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // prepare layout template
            // todo: localize me
            LayoutTemplate layoutTemplate = new LayoutTemplate(currentProfile.LayoutTitle, "Your modified layout for this executable.", Environment.UserName, false, true)
            {
                Layout = currentProfile.Layout.Clone() as Layout,
                Executable = currentProfile.Executable,
                Product = currentProfile.Name,
            };
            layoutTemplate.Layout.Updated += Layout_Updated;

            // update layout page with layout template
            MainWindow.layoutPage.UpdateLayout(layoutTemplate);

            // navigate
            MainWindow.NavView_Navigate(MainWindow.layoutPage);
        }

        private void Layout_Updated(Layout layout)
        {
            currentProfile.Layout = layout;
        }

        private void FramerateRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}