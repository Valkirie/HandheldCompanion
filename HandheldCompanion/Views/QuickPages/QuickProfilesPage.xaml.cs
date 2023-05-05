using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Layout = ControllerCommon.Layout;
using Page = System.Windows.Controls.Page;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickProfilesPage.xaml
    /// </summary>
    public partial class QuickProfilesPage : Page
    {
        private ProcessEx currentProcess;
        private Profile currentProfile;
        private Hotkey ProfilesPageHotkey = new(61);

        private const int UpdateInterval = 500;
        private Timer UpdateTimer;

        private object updateLock = new();

        public QuickProfilesPage()
        {
            InitializeComponent();

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            ProcessManager.ProcessStopped += ProcessManager_ProcessStopped;

            ProfileManager.Updated += ProfileUpdated;
            ProfileManager.Deleted += ProfileDeleted;
            ProfileManager.Applied += ProfileApplied;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;

            HotkeysManager.HotkeyCreated += TriggerCreated;
            InputsManager.TriggerUpdated += TriggerUpdated;

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

            UpdateTimer = new Timer(UpdateInterval);
            UpdateTimer.AutoReset = false;
            UpdateTimer.Elapsed += (sender, e) => SubmitProfile();
        }

        public void SubmitProfile(ProfileUpdateSource source = ProfileUpdateSource.QuickProfilesPage)
        {
            if (currentProfile is null)
                return;

            ProfileManager.UpdateOrCreateProfile(currentProfile, source);
        }

        private void HotkeysManager_CommandExecuted(string listener)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (listener)
                {
                    case "increaseTDP":
                        {
                            if (currentProfile is null || currentProfile.Default || !currentProfile.TDPOverrideEnabled)
                                return;

                            TDPBoostSlider.Value++;
                            TDPSustainedSlider.Value++;
                        }
                        break;
                    case "decreaseTDP":
                        {
                            if (currentProfile is null || currentProfile.Default || !currentProfile.TDPOverrideEnabled)
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
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
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
            });
        }

        private void ProfileApplied(Profile profile)
        {
            ProfileUpdated(profile, ProfileUpdateSource.Background, true);
        }

        private void ProfileDeleted(Profile profile)
        {
            if (currentProfile is null)
                return;

            bool isCurrent = profile.Path.Equals(currentProfile.Path, StringComparison.InvariantCultureIgnoreCase);
            if (isCurrent)
                ProcessManager_ForegroundChanged(currentProcess, null);

            currentProfile = null;
        }

        private void ProfileUpdated(Profile profile, ProfileUpdateSource source, bool isCurrent)
        {
            if (!isCurrent || profile.Default)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                switch (source)
                {
                    // self update, unlock and exit
                    case ProfileUpdateSource.QuickProfilesPage:
                        Monitor.Exit(updateLock);
                        return;
                }

                // if an update is pending, execute it and stop timer
                if (UpdateTimer.Enabled)
                {
                    UpdateTimer.Stop();
                    SubmitProfile();
                }

                // update current profile
                currentProfile = profile.Clone() as Profile;

                // UI thread (async)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // manage visibility here too...
                    b_CreateProfile.Visibility = Visibility.Collapsed;
                    GridProfile.Visibility = Visibility.Visible;

                    ProfileToggle.IsEnabled = !profile.Default;
                    ProfileToggle.IsOn = profile.Enabled;
                    UMCToggle.IsOn = profile.MotionEnabled;
                    cB_Input.SelectedIndex = (int)profile.MotionInput;
                    cB_Output.SelectedIndex = (int)profile.MotionOutput;
                    cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)profile.MotionMode;

                    // Sustained TDP settings (slow, stapm, long)
                    double[] TDP = profile.TDPOverrideValues is not null ? profile.TDPOverrideValues : MainWindow.CurrentDevice.nTDP;
                    TDPSustainedSlider.Value = TDP[(int)PowerType.Slow];
                    TDPBoostSlider.Value = TDP[(int)PowerType.Fast];

                    TDPToggle.IsOn = profile.TDPOverrideEnabled;

                    AutoTDPToggle.IsOn = profile.AutoTDPEnabled;
                    AutoTDPRequestedFPSSlider.Value = profile.AutoTDPRequestedFPS;

                    // Slider settings
                    SliderUMCAntiDeadzone.Value = profile.MotionAntiDeadzone;
                    SliderSensitivityX.Value = profile.MotionSensivityX;
                    SliderSensitivityY.Value = profile.MotionSensivityY;

                    // todo: improve me ?
                    ProfilesPageHotkey.inputsChord.State = profile.MotionTrigger.Clone() as ButtonState;
                    ProfilesPageHotkey.DrawInput();
                });

                // release lock
                Monitor.Exit(updateLock);
            }
        }

        private void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            // update current process
            currentProcess = processEx;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (processEx.MainWindowHandle != IntPtr.Zero)
                {
                    string MainWindowTitle = ProcessUtils.GetWindowTitle(processEx.MainWindowHandle);

                    ProcessName.Text = currentProcess.Executable;
                    ProcessPath.Text = currentProcess.Path;
                }
                else
                {
                    ProcessManager_ProcessStopped(processEx);
                }

                // disable create button if process is bypassed
                b_CreateProfile.IsEnabled = processEx.Filter == ProcessEx.ProcessFilter.Allowed;

                Profile profile = ProfileManager.GetProfileFromPath(currentProcess.Path);
                if (profile is null || profile.Default)
                {
                    b_CreateProfile.Visibility = Visibility.Visible;
                    GridProfile.Visibility = Visibility.Collapsed;
                }
                else
                {
                    b_CreateProfile.Visibility = Visibility.Collapsed;
                    GridProfile.Visibility = Visibility.Visible;
                }
            });
        }

        private void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (currentProcess == processEx)
                {
                    ProcessName.Text = Properties.Resources.QuickProfilesPage_Waiting;
                    ProcessPath.Text = string.Empty;

                    b_CreateProfile.Visibility = Visibility.Collapsed;
                    GridProfile.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void RequestUpdate()
        {
            UpdateTimer.Stop();
            UpdateTimer.Start();
        }

        private void ProfileToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.Enabled = ProfileToggle.IsOn;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void UMCToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.MotionEnabled = UMCToggle.IsOn;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
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
                    cB_Output.SelectedIndex = (int)MotionOutput.RightStick;
                    GridSensivity.Visibility = Visibility.Visible;
                    break;
                case MotionInput.JoystickSteering:
                    cB_Output.SelectedIndex = (int)MotionOutput.LeftStick;
                    GridSensivity.Visibility = Visibility.Collapsed;
                    break;
            }

            Text_InputHint.Text = Profile.InputDescription[input];

            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.MotionInput = (MotionInput)cB_Input.SelectedIndex;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.MotionOutput = (MotionOutput)cB_Output.SelectedIndex;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void b_CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (currentProcess is null)
                return;

            // create profile
            currentProfile = new Profile(currentProcess.Path);
            currentProfile.Layout = LayoutTemplate.DefaultLayout.Layout.Clone() as Layout;
            currentProfile.LayoutTitle = LayoutTemplate.DesktopLayout.Name;
            currentProfile.TDPOverrideValues = MainWindow.CurrentDevice.nTDP;

            // if an update is pending, execute it and stop timer
            if (UpdateTimer.Enabled)
                UpdateTimer.Stop();

            SubmitProfile(ProfileUpdateSource.Creation);
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.TDPOverrideEnabled = (bool)TDPToggle.IsOn;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void TDPSustainedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!TDPToggle.IsOn)
                return;

            if (!TDPSustainedSlider.IsInitialized || !TDPBoostSlider.IsInitialized)
                return;

            // Prevent sustained value being higher then boost
            if (TDPSustainedSlider.Value > TDPBoostSlider.Value)
            {
                TDPBoostSlider.Value = TDPSustainedSlider.Value;
            }

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.TDPOverrideValues[0] = (int)TDPSustainedSlider.Value;
                currentProfile.TDPOverrideValues[1] = (int)TDPSustainedSlider.Value;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!TDPToggle.IsOn)
                return;

            if (!TDPSustainedSlider.IsInitialized || !TDPBoostSlider.IsInitialized)
                return;

            // Prevent boost value being lower then sustained
            if (TDPBoostSlider.Value < TDPSustainedSlider.Value)
            {
                TDPSustainedSlider.Value = TDPBoostSlider.Value;
            }

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.TDPOverrideValues[2] = (int)TDPBoostSlider.Value;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }
        private void AutoTDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.AutoTDPEnabled = (bool)AutoTDPToggle.IsOn;
                AutoTDPRequestedFPSSlider.Value = currentProfile.AutoTDPRequestedFPS;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void AutoTDPRequestedFPSSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!AutoTDPToggle.IsOn)
                return;

            if (!AutoTDPRequestedFPSSlider.IsInitialized)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.AutoTDPRequestedFPS = (int)AutoTDPRequestedFPSSlider.Value;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }



        private void SliderUMCAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.MotionAntiDeadzone = (float)SliderUMCAntiDeadzone.Value;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }

        private void TriggerCreated(Hotkey hotkey)
        {
            switch (hotkey.inputsHotkey.Listener)
            {
                case "shortcutProfilesPage@@":
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
            if (currentProfile is null)
                return;

            // no Monitor on threaded calls ?
            switch (listener)
            {
                case "shortcutProfilesPage@":
                case "shortcutProfilesPage@@":
                    currentProfile.MotionTrigger = inputs.State.Clone() as ButtonState;
                    RequestUpdate();
                    break;
            }
        }

        private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1 || currentProfile is null)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                currentProfile.MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;
                RequestUpdate();

                Monitor.Exit(updateLock);
            }
        }
    }
}
