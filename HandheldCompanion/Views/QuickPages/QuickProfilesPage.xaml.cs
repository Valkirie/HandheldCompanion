using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using ModernWpf.Controls;
using System;
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
        private Profile realProfile;

        private Hotkey ProfilesPageHotkey = new(61);

        private const int UpdateInterval = 500;
        private readonly Timer UpdateTimer;

        private bool isDrawing;

        public QuickProfilesPage()
        {
            InitializeComponent();

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            ProfileManager.Updated += ProfileUpdated;
            ProfileManager.Deleted += ProfileDeleted;
            ProfileManager.Applied += ProfileApplied;

            SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;
            HotkeysManager.HotkeyCreated += TriggerCreated;

            InputsManager.TriggerUpdated += TriggerUpdated;

            MainWindow.performanceManager.ProcessorStatusChanged += PowerManager_StatusChanged;

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

            // device settings
            GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
            GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

            FramerateToggle.IsEnabled = PlatformManager.RTSS.IsInstalled;

            UpdateTimer = new Timer(UpdateInterval);
            UpdateTimer.AutoReset = false;
            UpdateTimer.Elapsed += (sender, e) => SubmitProfile();
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

        private void PowerManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                StackProfileTDP.IsEnabled = CanChangeTDP;
                StackProfileAutoTDP.IsEnabled = CanChangeTDP;

                StackProfileGPU.IsEnabled = CanChangeGPU;
            });
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
                            if (currentProfile is null || !currentProfile.TDPOverrideEnabled)
                                return;

                            TDPSlider.Value++;
                        }
                        break;
                    case "decreaseTDP":
                        {
                            if (currentProfile is null || !currentProfile.TDPOverrideEnabled)
                                return;

                            TDPSlider.Value--;
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
                        TDPSlider.Maximum = Convert.ToInt32(value);
                        break;
                    case "ConfigurableTDPOverrideDown":
                        TDPSlider.Minimum = Convert.ToInt32(value);
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
            if (!isCurrent)
                return;

            if (true)
            {
                switch (source)
                {
                    // self update, unlock and exit
                    case ProfileUpdateSource.QuickProfilesPage:
                        return;
                }

                // if an update is pending, execute it and stop timer
                if (UpdateTimer.Enabled)
                {
                    UpdateTimer.Stop();
                    SubmitProfile();
                }

                // update profile
                currentProfile = profile.Clone() as Profile;

                // UI thread
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // set lock
                    isDrawing = true;

                    UMCToggle.IsOn = currentProfile.MotionEnabled;
                    cB_Input.SelectedIndex = (int)currentProfile.MotionInput;
                    cB_Output.SelectedIndex = (int)currentProfile.MotionOutput;
                    cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)currentProfile.MotionMode;

                    // TDP
                    TDPToggle.IsOn = currentProfile.TDPOverrideEnabled;
                    double[] TDP = currentProfile.TDPOverrideValues is not null ? currentProfile.TDPOverrideValues : MainWindow.CurrentDevice.nTDP;
                    TDPSlider.Value = TDP[(int)PowerType.Slow];

                    // GPU
                    GPUToggle.IsOn = currentProfile.GPUOverrideEnabled;
                    GPUSlider.Value = currentProfile.GPUOverrideValue != 0 ? currentProfile.GPUOverrideValue : (255 * 50);

                    // Framerate
                    FramerateToggle.IsOn = currentProfile.FramerateEnabled;
                    FramerateSlider.Value = currentProfile.FramerateValue;

                    // AutoTDP
                    AutoTDPToggle.IsOn = currentProfile.AutoTDPEnabled;
                    AutoTDPRequestedFPSSlider.Value = currentProfile.AutoTDPRequestedFPS;

                    // Slider settings
                    SliderUMCAntiDeadzone.Value = currentProfile.MotionAntiDeadzone;
                    SliderSensitivityX.Value = currentProfile.MotionSensivityX;
                    SliderSensitivityY.Value = currentProfile.MotionSensivityY;

                    // todo: improve me ?
                    ProfilesPageHotkey.inputsChord.State = currentProfile.MotionTrigger.Clone() as ButtonState;
                    ProfilesPageHotkey.DrawInput();

                    // release lock
                    isDrawing = false;
                });
            }
        }

        private void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            // update current process
            currentProcess = processEx;

            // update real profile
            realProfile = ProfileManager.GetProfileFromPath(processEx.Path, true);

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // set lock
                isDrawing = true;

                ProfileToggle.IsOn = realProfile.Enabled;
                ProfileIcon.Source = processEx.imgSource;

                if (processEx.MainWindowHandle != IntPtr.Zero)
                {
                    string MainWindowTitle = ProcessUtils.GetWindowTitle(processEx.MainWindowHandle);

                    ProfileToggle.IsEnabled = true;
                    ProcessName.Text = currentProcess.Executable;
                    ProcessPath.Text = currentProcess.Path;
                }
                else
                {
                    ProfileIcon.Source = null;

                    ProfileToggle.IsEnabled = false;
                    ProcessName.Text = Properties.Resources.QuickProfilesPage_Waiting;
                    ProcessPath.Text = string.Empty;
                }

                // release lock
                isDrawing = false;
            });
        }

        private void RequestUpdate()
        {
            UpdateTimer.Stop();
            UpdateTimer.Start();
        }

        private void ProfileToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // update real profile
            realProfile = ProfileManager.GetProfileFromPath(realProfile.Path, true);
            if (realProfile is null)
                return;

            if (!isDrawing)
            {
                if (realProfile.Default)
                {
                    CreateProfile();
                }
                else
                {
                    realProfile.Enabled = ProfileToggle.IsOn;
                    ProfileManager.UpdateOrCreateProfile(realProfile, ProfileUpdateSource.QuickProfilesPage);
                }
            }
        }

        private void CreateProfile()
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

        private void UMCToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.MotionEnabled = UMCToggle.IsOn;
                RequestUpdate();
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

            if (!isDrawing)
            {
                currentProfile.MotionInput = (MotionInput)cB_Input.SelectedIndex;
                RequestUpdate();
            }
        }

        private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.MotionOutput = (MotionOutput)cB_Output.SelectedIndex;
                RequestUpdate();
            }
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                // TDP and AutoTDP are mutually exclusive
                bool toggled = TDPToggle.IsOn;
                if (toggled)
                    AutoTDPToggle.IsOn = false;

                currentProfile.TDPOverrideEnabled = toggled;
                RequestUpdate();
            }
        }

        private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.TDPOverrideValues[(int)PowerType.Slow] = (int)TDPSlider.Value;
                currentProfile.TDPOverrideValues[(int)PowerType.Stapm] = (int)TDPSlider.Value;
                currentProfile.TDPOverrideValues[(int)PowerType.Fast] = (int)TDPSlider.Value;
                RequestUpdate();
            }
        }

        private void AutoTDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                // TDP and AutoTDP are mutually exclusive
                bool toggled = AutoTDPToggle.IsOn;
                if (toggled)
                    TDPToggle.IsOn = false;

                currentProfile.AutoTDPEnabled = toggled;
                AutoTDPRequestedFPSSlider.Value = currentProfile.AutoTDPRequestedFPS;

                RequestUpdate();
            }
        }

        private void AutoTDPRequestedFPSSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.AutoTDPRequestedFPS = (int)AutoTDPRequestedFPSSlider.Value;
                RequestUpdate();
            }
        }

        private void SliderUMCAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.MotionAntiDeadzone = (float)SliderUMCAntiDeadzone.Value;
                RequestUpdate();
            }
        }

        private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
                RequestUpdate();
            }
        }

        private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
                RequestUpdate();
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

            if (!isDrawing)
            {
                currentProfile.MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;
                RequestUpdate();
            }
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.GPUOverrideEnabled = (bool)GPUToggle.IsOn;
                RequestUpdate();
            }
        }

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.GPUOverrideValue = (int)GPUSlider.Value;
                RequestUpdate();
            }
        }

        private void FramerateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.FramerateEnabled = (bool)FramerateToggle.IsOn;

                RequestUpdate();
            }
        }

        private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                int value = (int)FramerateSlider.Value;

                foreach (TextBlock tb in FramerateModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)FramerateModeGrid.Children[value];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });

            if (currentProfile is null)
                return;

            if (!isDrawing)
            {
                currentProfile.FramerateValue = (int)FramerateSlider.Value;
                RequestUpdate();
            }
        }
    }
}
