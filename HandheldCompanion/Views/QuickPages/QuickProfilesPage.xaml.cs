using ControllerCommon;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using PrecisionTiming;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickProfilesPage.xaml
    /// </summary>
    public partial class QuickProfilesPage : Page
    {
        private ProcessEx currentProcess;
        private Profile currentProfile;
        private Hotkey ProfilesPageHotkey;

        private object updateLock = new();

        public QuickProfilesPage()
        {
            InitializeComponent();

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            ProfileManager.Updated += ProfileUpdated;
            ProfileManager.Deleted += ProfileDeleted;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;

            HotkeysManager.HotkeyCreated += TriggerCreated;
            InputsManager.TriggerUpdated += TriggerUpdated;

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
        }

        private void HotkeysManager_CommandExecuted(string listener)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (listener)
                {
                    case "increaseTDP":
                        {
                            if (currentProfile == null || currentProfile.isDefault || !currentProfile.TDP_override)
                                return;

                            TDPSustainedSlider.Value++;
                            TDPBoostSlider.Value++;
                        }
                        break;
                    case "decreaseTDP":
                        {
                            if (currentProfile == null || currentProfile.isDefault || !currentProfile.TDP_override)
                                return;

                            TDPSustainedSlider.Value--;
                            TDPBoostSlider.Value--;
                        }
                        break;
                }
            });
        }

        public void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "ConfigurableTDPOverrideUp":
                    TDPSustainedSlider.Maximum = Convert.ToInt32(value);
                    TDPBoostSlider.Maximum = Convert.ToInt32(value);
                    break;
                case "ConfigurableTDPOverrideDown":
                    TDPSustainedSlider.Minimum = Convert.ToInt32(value);
                    TDPBoostSlider.Minimum = Convert.ToInt32(value);
                    break;
            }
        }

        private void ProfileDeleted(Profile profile)
        {
            if (currentProfile is null)
                return;

            if (profile.executable == currentProfile.executable)
                ProcessManager_ForegroundChanged(currentProcess, null);
        }

        private void ProfileUpdated(Profile profile, bool backgroundtask, bool isCurrent)
        {
            if (!isCurrent || profile.isDefault)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                this.Dispatcher.Invoke(() =>
                {
                    b_CreateProfile.Visibility = Visibility.Collapsed;
                    GridProfile.Visibility = Visibility.Visible;

                    ProfileToggle.IsEnabled = true;
                    ProfileToggle.IsOn = profile.isEnabled;
                    UMCToggle.IsOn = profile.umc_enabled;
                    cB_Input.SelectedIndex = (int)profile.umc_input;
                    cB_Output.SelectedIndex = (int)profile.umc_output;
                    cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)profile.umc_motion_defaultoffon;

                    // Sustained TDP settings (slow, stapm, long)
                    double[] TDP = profile.TDP_value != null ? profile.TDP_value : MainWindow.handheldDevice.nTDP;
                    TDPSustainedSlider.Value = TDP[(int)PowerType.Slow];
                    TDPBoostSlider.Value = TDP[(int)PowerType.Fast];

                    TDPToggle.IsOn = profile.TDP_override;

                    // Slider settings
                    SliderSensitivityX.Value = profile.aiming_sensitivity_x;
                    SliderSensitivityY.Value = profile.aiming_sensitivity_y;
                    SliderAntiDeadzone.Value = profile.antideadzone;

                    // todo: improve me ?
                    ProfilesPageHotkey.inputsChord.GamepadButtons = currentProfile.umc_trigger;
                    ProfilesPageHotkey.Refresh();

                    if (backgroundtask)
                        return;

                    _ = Dialog.ShowAsync($"{Properties.Resources.ProfilesPage_ProfileUpdated1}",
                                        $"{profile.name} {Properties.Resources.ProfilesPage_ProfileUpdated2}",
                                        ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
                });

                Monitor.Exit(updateLock);
            }
        }

        private void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            currentProcess = processEx;
            currentProfile = ProfileManager.GetProfileFromExec(currentProcess.Name);

            this.Dispatcher.Invoke(() =>
            {
                ProcessName.Text = currentProcess.Name;
                ProcessPath.Text = currentProcess.Path;

                // disable create button if process is bypassed
                b_CreateProfile.IsEnabled = processEx.Filter == ProcessEx.ProcessFilter.None;

                if (currentProfile is null)
                {
                    b_CreateProfile.Visibility = Visibility.Visible;
                    GridProfile.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ProfileUpdated(currentProfile, true, true);
                }
            });
        }

        private void UpdateProfile()
        {
            ProfileManager.UpdateOrCreateProfile(currentProfile, true);
        }

        private void ProfileToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.isEnabled = ProfileToggle.IsOn;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void UMCToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.umc_enabled = UMCToggle.IsOn;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
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
                    GridAntiDeadzone.Visibility = Visibility.Visible;
                    break;
                case Input.JoystickSteering:
                    cB_Output.SelectedIndex = (int)Output.LeftStick;
                    GridSensivity.Visibility = Visibility.Collapsed;
                    GridAntiDeadzone.Visibility = Visibility.Collapsed;
                    break;
            }

            Text_InputHint.Text = Profile.InputDescription[input];

            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.umc_input = (Input)cB_Input.SelectedIndex;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.umc_output = (Output)cB_Output.SelectedIndex;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void b_CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProcess is null)
                return;

            // create profile
            currentProfile = new Profile(currentProcess.Path);
            currentProfile.TDP_value = MainWindow.handheldDevice.nTDP;

            // update current profile
            ProfileManager.currentProfile = currentProfile;

            UpdateProfile();
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.TDP_override = (bool)TDPToggle.IsOn;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void TDPSustainedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!TDPToggle.IsOn)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.TDP_value[0] = (int)TDPSustainedSlider.Value;
                currentProfile.TDP_value[1] = (int)TDPSustainedSlider.Value;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!TDPToggle.IsOn)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.TDP_value[2] = (int)TDPBoostSlider.Value;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.aiming_sensitivity_x = (float)SliderSensitivityX.Value;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.aiming_sensitivity_y = (float)SliderSensitivityY.Value;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void SliderAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.antideadzone = (float)SliderAntiDeadzone.Value;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }

        private void TriggerCreated(Hotkey hotkey)
        {
            switch (hotkey.inputsHotkey.Listener)
            {
                case "shortcutProfilesPage2":
                    {
                        Border hotkeyBorder = hotkey.GetHotkey();
                        if (hotkeyBorder is null || hotkeyBorder.Parent != null)
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
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                switch (listener)
                {
                    case "shortcutProfilesPage1":
                    case "shortcutProfilesPage2":
                        currentProfile.umc_trigger = inputs.GamepadButtons;
                        UpdateProfile();
                        break;
                }

                Monitor.Exit(updateLock);
            }
        }

        private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1 || currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.umc_motion_defaultoffon = (UMC_Motion_Default)cB_UMC_MotionDefaultOffOn.SelectedIndex;
                UpdateProfile();

                Monitor.Exit(updateLock);
            }
        }
    }
}
