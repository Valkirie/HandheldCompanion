using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
<<<<<<< HEAD
using HandheldCompanion.Platforms;
using HandheldCompanion.Processors;
=======
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
<<<<<<< HEAD
=======
using System.Windows.Media;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
using Page = System.Windows.Controls.Page;
using Separator = System.Windows.Controls.Separator;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickProfilesPage.xaml
/// </summary>
public partial class QuickProfilesPage : Page
{
    private const int UpdateInterval = 500;
    private readonly Timer UpdateTimer;
    private ProcessEx currentProcess;
    private Profile selectedProfile;

    private LockObject updateLock = new();

    private Hotkey GyroHotkey = new(61);
    private Profile realProfile;

    public QuickProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickProfilesPage()
    {
        InitializeComponent();

        ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

        ProfileManager.Applied += ProfileApplied;

<<<<<<< HEAD
        SystemManager.DisplaySettingsChanged += SystemManager_DisplaySettingsChanged;
        SystemManager.RSRStateChanged += SystemManager_RSRStateChanged;
=======
        PowerProfileManager.Updated += PowerProfileManager_Updated;
        PowerProfileManager.Deleted += PowerProfileManager_Deleted;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

        SystemManager.DisplaySettingsChanged += SystemManager_DisplaySettingsChanged;
        SystemManager.RSRStateChanged += SystemManager_RSRStateChanged;

        HotkeysManager.HotkeyCreated += TriggerCreated;

        InputsManager.TriggerUpdated += TriggerUpdated;

        foreach (var mode in (MotionOuput[])Enum.GetValues(typeof(MotionOuput)))
        {
            // create panel
            ComboBoxItem comboBoxItem = new ComboBoxItem()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };

            SimpleStackPanel simpleStackPanel = new SimpleStackPanel
            {
                Spacing = 6,
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // create icon
            var icon = new FontIcon();

            switch (mode)
            {
                case MotionOuput.Disabled:
                    icon.Glyph = "\uE8D8";
                    break;
                case MotionOuput.RightStick:
                    icon.Glyph = "\uF109";
                    break;
                case MotionOuput.LeftStick:
                    icon.Glyph = "\uF108";
                    break;
                case MotionOuput.MoveCursor:
                    icon.Glyph = "\uE962";
                    break;
                case MotionOuput.ScrollWheel:
                    icon.Glyph = "\uEC8F";
                    break;
            }

            if (!string.IsNullOrEmpty(icon.Glyph))
                simpleStackPanel.Children.Add(icon);

            // create textblock
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };

            simpleStackPanel.Children.Add(text);

            comboBoxItem.Content = simpleStackPanel;
            cB_Output.Items.Add(comboBoxItem);
        }

        foreach (var mode in (MotionOuput[])Enum.GetValues(typeof(MotionOuput)))
        {
            // create panel
            ComboBoxItem comboBoxItem = new ComboBoxItem()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };

            SimpleStackPanel simpleStackPanel = new SimpleStackPanel
            {
                Spacing = 6,
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // create icon
            var icon = new FontIcon();

            switch (mode)
            {
                case MotionOuput.Disabled:
                    icon.Glyph = "\uE8D8";
                    break;
                case MotionOuput.RightStick:
                    icon.Glyph = "\uF109";
                    break;
                case MotionOuput.LeftStick:
                    icon.Glyph = "\uF108";
                    break;
                case MotionOuput.MoveCursor:
                    icon.Glyph = "\uE962";
                    break;
                case MotionOuput.ScrollWheel:
                    icon.Glyph = "\uEC8F";
                    break;
            }

            if (!string.IsNullOrEmpty(icon.Glyph))
                simpleStackPanel.Children.Add(icon);

            // create textblock
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };

            simpleStackPanel.Children.Add(text);

            comboBoxItem.Content = simpleStackPanel;
            cB_Output.Items.Add(comboBoxItem);
        }

        foreach (var mode in (MotionInput[])Enum.GetValues(typeof(MotionInput)))
        {
            // create panel
            ComboBoxItem comboBoxItem = new ComboBoxItem()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };

            SimpleStackPanel simpleStackPanel = new SimpleStackPanel
            {
                Spacing = 6,
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // create icon
            var icon = new FontIcon();

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

            if (!string.IsNullOrEmpty(icon.Glyph))
                simpleStackPanel.Children.Add(icon);

            // create textblock
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };

            simpleStackPanel.Children.Add(text);
<<<<<<< HEAD

            comboBoxItem.Content = simpleStackPanel;
            cB_Input.Items.Add(comboBoxItem);
        }

        // device settings
        GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
        GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

        // motherboard settings
        CPUCoreSlider.Maximum = MotherboardInfo.NumberOfCores;

=======

            comboBoxItem.Content = simpleStackPanel;
            cB_Input.Items.Add(comboBoxItem);
        }

>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();

        PlatformManager.RTSS.Updated += RTSS_Updated;

        // force call
        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    private void SystemManager_RSRStateChanged(int RSRState, int RSRSharpness)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (RSRState)
            {
                case -1:
                    StackProfileRSR.IsEnabled = false;
                    break;
                case 0:
                    StackProfileRSR.IsEnabled = true;
                    RSRToggle.IsOn = false;
                    RSRSlider.Value = RSRSharpness;
                    break;
                case 1:
                    StackProfileRSR.IsEnabled = true;
                    RSRToggle.IsOn = true;
                    RSRSlider.Value = RSRSharpness;
                    break;
            }
        });
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    var Processor = MainWindow.performanceManager.GetProcessor();
                    StackProfileFramerate.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    // StackProfileFramerate.IsEnabled = false;
                    // StackProfileAutoTDP.IsEnabled = false;
                    break;
            }
        });
    }

    private void SystemManager_DisplaySettingsChanged(ScreenResolution resolution)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var screenFrequency = SystemManager.GetDesktopScreen().GetFrequency();

            FramerateQuarter.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Quarter));
            FramerateThird.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Third));
            FramerateHalf.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Half));
            FramerateFull.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Full));
        });
    }

    public void SubmitProfile(UpdateSource source = UpdateSource.QuickProfilesPage)
    {
        if (selectedProfile is null)
            return;

        ProfileManager.UpdateOrCreateProfile(selectedProfile, source);
    }

    private void PowerProfileManager_Deleted(PowerProfile powerProfile)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            int idx = -1;
            foreach (var item in ProfileStack.Children)
            {
<<<<<<< HEAD
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
=======
                if (item is not Button)
                    continue;

                // get power profile
                var parent = (Button)item;
                if (parent.Tag is not PowerProfile)
                    continue;

                PowerProfile pr = (PowerProfile)parent.Tag;

                bool isCurrent = pr.Guid == powerProfile.Guid;
                if (isCurrent)
                {
                    idx = ProfileStack.Children.IndexOf(parent);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                    break;
                }
            }

            if (idx != -1)
            {
                // remove profile
                ProfileStack.Children.RemoveAt(idx);

                // remove separator
                if (idx >= ProfileStack.Children.Count)
                    idx = ProfileStack.Children.Count - 1;
                ProfileStack.Children.RemoveAt(idx);
            }
        });
    }

    private void PowerProfileManager_Updated(PowerProfile powerProfile, UpdateSource source)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            int idx = -1;
            foreach (var item in ProfileStack.Children)
            {
<<<<<<< HEAD
                case "ConfigurableTDPOverrideDown":
                    {
                        using (new ScopedLock(updateLock))
                        {
                            TDPSlider.Minimum = (double)value;
                        }
                    }
                    break;
                case "ConfigurableTDPOverrideUp":
                    {
                        using (new ScopedLock(updateLock))
                        {
                            TDPSlider.Maximum = (double)value;
                        }
                    }
                    break;
=======
                if (item is not Button)
                    continue;

                // get power profile
                var parent = (Button)item;
                if (parent.Tag is not PowerProfile)
                    continue;

                PowerProfile pr = (PowerProfile)parent.Tag;

                bool isCurrent = pr.Guid == powerProfile.Guid;
                if (isCurrent)
                {
                    idx = ProfileStack.Children.IndexOf(parent);
                    break;
                }
            }

            if (idx != -1)
            {
                // found it
                return;
            }
            else
            {
                // draw UI elements
                powerProfile.DrawUI(this);

                idx = ProfileStack.Children.Count;
                if (idx != 0)
                {
                    // Create a separator
                    Separator separator = new Separator();
                    separator.Margin = new Thickness(-16, 0, -16, 0);
                    separator.BorderBrush = (Brush)FindResource("SystemControlBackgroundChromeMediumBrush");
                    separator.BorderThickness = new Thickness(0, 1, 0, 0);
                    ProfileStack.Children.Add(separator);
                }

                Button button = powerProfile.GetButton(this);
                button.Click += (sender, e) => PowerProfile_Clicked(powerProfile);

                RadioButton radioButton = powerProfile.GetRadioButton(this);
                radioButton.Checked += (sender, e) => PowerProfile_Selected(powerProfile);

                // add new entry
                ProfileStack.Children.Add(button);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            }
        });
    }

    private void PowerProfile_Clicked(PowerProfile powerProfile)
    {
        RadioButton radioButton = powerProfile.GetRadioButton(this);
        if (radioButton.IsMouseOver)
            return;

        MainWindow.overlayquickTools.performancePage.SelectionChanged(powerProfile.Guid);
        MainWindow.overlayquickTools.NavView_Navigate("QuickPerformancePage");
    }

    private void PowerProfile_Selected(PowerProfile powerProfile)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // update UI
            SelectedPowerProfileName.Text = powerProfile.Name;

            // update profile
            selectedProfile.PowerProfile = powerProfile.Guid;
            UpdateProfile();
        });
    }

    private void ProfileApplied(Profile profile, UpdateSource source)
    {
        if (true)
        {
            switch (source)
            {
                // self update, unlock and exit
                case UpdateSource.QuickProfilesPage:
                case UpdateSource.Serializer:
                    return;
            }

            // if an update is pending, execute it and stop timer
            if (UpdateTimer.Enabled)
            {
                UpdateTimer.Stop();
                SubmitProfile();
            }

            // update profile
<<<<<<< HEAD
            currentProfile = profile;
=======
            selectedProfile = profile;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                using (new ScopedLock(updateLock))
                {
                    // update profile name
<<<<<<< HEAD
                    CurrentProfileName.Text = currentProfile.Name;
                    Toggle_ControllerLayout.IsEnabled = currentProfile.Default ? false : true;
                    Toggle_ControllerLayout.IsOn = currentProfile.LayoutEnabled;

                    // gyro layout
                    if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
                    {
                        // no gyro layout available, mark as disabled
                        cB_Output.SelectedIndex = (int)MotionOuput.Disabled;
                    }
                    else
                    {
                        // IActions
                        GridAntiDeadzone.Visibility = currentAction is AxisActions ? Visibility.Visible : Visibility.Collapsed;
                        GridGyroWeight.Visibility = currentAction is AxisActions ? Visibility.Visible : Visibility.Collapsed;

                        if (currentAction is AxisActions)
                        {
                            cB_Output.SelectedIndex = (int)((AxisActions)currentAction).Axis;
                            SliderUMCAntiDeadzone.Value = ((AxisActions)currentAction).AxisAntiDeadZone;
                            Slider_GyroWeight.Value = ((AxisActions)currentAction).gyroWeight;
                        }
                        else if (currentAction is MouseActions)
                        {
                            cB_Output.SelectedIndex = (int)((MouseActions)currentAction).MouseType - 1;
                        }

                        // GyroActions
                        cB_Input.SelectedIndex = (int)((GyroActions)currentAction).MotionInput;
                        cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)((GyroActions)currentAction).MotionMode;

                        // todo: move me to layout ?
                        SliderSensitivityX.Value = currentProfile.MotionSensivityX;
                        SliderSensitivityY.Value = currentProfile.MotionSensivityY;

                        // todo: improve me ?
                        GyroHotkey.inputsChord.State = ((GyroActions)currentAction).MotionTrigger.Clone() as ButtonState;
                        GyroHotkey.DrawInput();
                    }

                    // TDP
                    TDPToggle.IsOn = currentProfile.TDPOverrideEnabled;
                    var TDP = currentProfile.TDPOverrideValues is not null
                        ? currentProfile.TDPOverrideValues
                        : MainWindow.CurrentDevice.nTDP;
                    TDPSlider.Value = TDP[(int)PowerType.Slow];

                    // GPU
                    GPUToggle.IsOn = currentProfile.GPUOverrideEnabled;
                    GPUSlider.Value = currentProfile.GPUOverrideValue != 0 ? currentProfile.GPUOverrideValue : 255 * 50;

                    // Framerate
                    FramerateToggle.IsOn = currentProfile.FramerateEnabled;
                    FramerateSlider.Value = currentProfile.FramerateValue;

                    // AutoTDP
                    AutoTDPToggle.IsOn = currentProfile.AutoTDPEnabled;
                    AutoTDPRequestedFPSSlider.Value = currentProfile.AutoTDPRequestedFPS;

                    // EPP
                    EPPToggle.IsOn = currentProfile.EPPOverrideEnabled;
                    EPPSlider.Value = currentProfile.EPPOverrideValue;

                    // RSR
                    RSRToggle.IsOn = currentProfile.RSREnabled;
                    RSRSlider.Value = currentProfile.RSRSharpness;

                    // CPU Core Count
                    CPUCoreToggle.IsOn = currentProfile.CPUCoreEnabled;
                    CPUCoreSlider.Value = currentProfile.CPUCoreCount;
=======
                    CurrentProfileName.Text = selectedProfile.Name;
                    Toggle_ControllerLayout.IsEnabled = selectedProfile.Default ? false : true;
                    Toggle_ControllerLayout.IsOn = selectedProfile.LayoutEnabled;

                    // power profile
                    PowerProfile powerProfile = PowerProfileManager.GetProfile(profile.PowerProfile);
                    powerProfile.Check(this);

                    // gyro layout
                    if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
                    {
                        // no gyro layout available, mark as disabled
                        cB_Output.SelectedIndex = (int)MotionOuput.Disabled;
                    }
                    else
                    {
                        // IActions
                        GridAntiDeadzone.Visibility = currentAction is AxisActions ? Visibility.Visible : Visibility.Collapsed;
                        GridGyroWeight.Visibility = currentAction is AxisActions ? Visibility.Visible : Visibility.Collapsed;

                        if (currentAction is AxisActions)
                        {
                            cB_Output.SelectedIndex = (int)((AxisActions)currentAction).Axis;
                            SliderUMCAntiDeadzone.Value = ((AxisActions)currentAction).AxisAntiDeadZone;
                            Slider_GyroWeight.Value = ((AxisActions)currentAction).gyroWeight;
                        }
                        else if (currentAction is MouseActions)
                        {
                            cB_Output.SelectedIndex = (int)((MouseActions)currentAction).MouseType - 1;
                        }

                        // GyroActions
                        cB_Input.SelectedIndex = (int)((GyroActions)currentAction).MotionInput;
                        cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)((GyroActions)currentAction).MotionMode;

                        // todo: move me to layout ?
                        SliderSensitivityX.Value = selectedProfile.MotionSensivityX;
                        SliderSensitivityY.Value = selectedProfile.MotionSensivityY;

                        // todo: improve me ?
                        GyroHotkey.inputsChord.State = ((GyroActions)currentAction).MotionTrigger.Clone() as ButtonState;
                        GyroHotkey.DrawInput();
                    }

                    // Framerate
                    FramerateToggle.IsOn = selectedProfile.FramerateEnabled;
                    FramerateSlider.Value = selectedProfile.FramerateValue;

                    // RSR
                    RSRToggle.IsOn = selectedProfile.RSREnabled;
                    RSRSlider.Value = selectedProfile.RSRSharpness;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                }
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
            using (new ScopedLock(updateLock))
            {
                ProfileToggle.IsOn = !realProfile.Default && realProfile.Enabled;
                ProfileIcon.Source = processEx.imgSource;

                if (processEx.MainWindowHandle != IntPtr.Zero)
                {
                    var MainWindowTitle = ProcessUtils.GetWindowTitle(processEx.MainWindowHandle);

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
            }
        });
    }

    private void UpdateProfile()
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

        if (updateLock)
            return;

        if (realProfile.Default)
        {
            CreateProfile();
        }
        else
        {
            realProfile.Enabled = ProfileToggle.IsOn;
<<<<<<< HEAD
            ProfileManager.UpdateOrCreateProfile(realProfile, ProfileUpdateSource.Creation);
=======
            ProfileManager.UpdateOrCreateProfile(realProfile, UpdateSource.Creation);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        }
    }

    private void CreateProfile()
    {
        if (currentProcess is null)
            return;

        // create profile
<<<<<<< HEAD
        currentProfile = new Profile(currentProcess.Path);
        currentProfile.Layout = (ProfileManager.GetProfileWithDefaultLayout()?.Layout ?? LayoutTemplate.DefaultLayout.Layout).Clone() as Layout;
        currentProfile.LayoutTitle = LayoutTemplate.DesktopLayout.Name;
        currentProfile.TDPOverrideValues = MainWindow.CurrentDevice.nTDP;
=======
        selectedProfile = new Profile(currentProcess.Path);
        selectedProfile.Layout = (ProfileManager.GetProfileWithDefaultLayout()?.Layout ?? LayoutTemplate.DefaultLayout.Layout).Clone() as Layout;
        selectedProfile.LayoutTitle = LayoutTemplate.DesktopLayout.Name;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

        // if an update is pending, execute it and stop timer
        if (UpdateTimer.Enabled)
            UpdateTimer.Stop();

<<<<<<< HEAD
        SubmitProfile(ProfileUpdateSource.Creation);
=======
        SubmitProfile(UpdateSource.Creation);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void cB_Input_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Input.SelectedIndex == -1)
            return;

<<<<<<< HEAD
        var input = (MotionInput)cB_Input.SelectedIndex;
        Text_InputHint.Text = Profile.InputDescription[input];

        if (currentProfile is null)
=======
        MotionInput input = (MotionInput)cB_Input.SelectedIndex;
        Text_InputHint.Text = EnumUtils.GetDescriptionFromEnumValue(input, string.Empty, "Desc");

        if (selectedProfile is null)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            return;

        if (updateLock)
            return;

<<<<<<< HEAD
        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        ((GyroActions)currentAction).MotionInput = (MotionInput)cB_Input.SelectedIndex;
        RequestUpdate();
=======
        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        ((GyroActions)currentAction).MotionInput = (MotionInput)cB_Input.SelectedIndex;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
<<<<<<< HEAD
            return;

        IActions newActions = null;

        MotionOuput motionOuput = (MotionOuput)cB_Output.SelectedIndex;
        switch (motionOuput)
        {
            case MotionOuput.Disabled:
                currentProfile.Layout.RemoveLayout(AxisLayoutFlags.Gyroscope);
                break;
            case MotionOuput.LeftStick:
            case MotionOuput.RightStick:
                newActions = new AxisActions()
                {
                    Axis = GyroActions.DefaultAxisLayoutFlags,
                    AxisAntiDeadZone = GyroActions.DefaultAxisAntiDeadZone,
                    MotionTrigger = GyroHotkey.inputsChord.State.Clone() as ButtonState
                };
                break;
            case MotionOuput.MoveCursor:
            case MotionOuput.ScrollWheel:
                newActions = new MouseActions()
                {
                    MouseType = GyroActions.DefaultMouseActionsType,
                    Sensivity = GyroActions.DefaultSensivity,
                    Deadzone = GyroActions.DefaultDeadzone,
                    MotionTrigger = GyroHotkey.inputsChord.State.Clone() as ButtonState
                };
                break;
        }

        // proper layout update
        if (newActions is not null)
            currentProfile.Layout.UpdateLayout(AxisLayoutFlags.Gyroscope, newActions);

        RequestUpdate();
    }

    private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.TDPOverrideEnabled = TDPToggle.IsOn;
        RequestUpdate();
    }
=======
            return;

        // try get current actions, if exists
        selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions gyroActions);

        MotionOuput motionOuput = (MotionOuput)cB_Output.SelectedIndex;
        switch (motionOuput)
        {
            case MotionOuput.Disabled:
                selectedProfile.Layout.RemoveLayout(AxisLayoutFlags.Gyroscope);
                gyroActions = null;
                break;

            case MotionOuput.LeftStick:
            case MotionOuput.RightStick:
                {
                    if (gyroActions is null || gyroActions is not AxisActions)
                    {
                        gyroActions = new AxisActions()
                        {
                            AxisAntiDeadZone = GyroActions.DefaultAxisAntiDeadZone
                        };
                    }

                    ((AxisActions)gyroActions).Axis = motionOuput == MotionOuput.LeftStick ? AxisLayoutFlags.LeftStick : AxisLayoutFlags.RightStick;
                    ((AxisActions)gyroActions).MotionTrigger = GyroHotkey.inputsChord.State.Clone() as ButtonState;
                }
                break;

            case MotionOuput.MoveCursor:
            case MotionOuput.ScrollWheel:
                {
                    if (gyroActions is null || gyroActions is not MouseActions)
                    {
                        gyroActions = new MouseActions()
                        {
                            MouseType = GyroActions.DefaultMouseActionsType,
                            Sensivity = GyroActions.DefaultSensivity,
                            Deadzone = GyroActions.DefaultDeadzone
                        };
                    }

                    ((MouseActions)gyroActions).MotionTrigger = GyroHotkey.inputsChord.State.Clone() as ButtonState;
                }
                break;
        }
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

        // proper layout update
        if (gyroActions is not null)
            selectedProfile.Layout.UpdateLayout(AxisLayoutFlags.Gyroscope, gyroActions);

<<<<<<< HEAD
        if (updateLock)
            return;

        currentProfile.TDPOverrideValues = new double[3]
        {
                (int)TDPSlider.Value,
                (int)TDPSlider.Value,
                (int)TDPSlider.Value
        };
        RequestUpdate();
    }

    private void AutoTDPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.AutoTDPEnabled = AutoTDPToggle.IsOn;
        AutoTDPRequestedFPSSlider.Value = currentProfile.AutoTDPRequestedFPS;

        RequestUpdate();
    }

    private void AutoTDPRequestedFPSSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.AutoTDPRequestedFPS = (int)AutoTDPRequestedFPSSlider.Value;
        RequestUpdate();
=======
        SubmitProfile(UpdateSource.Creation);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void SliderUMCAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

<<<<<<< HEAD
        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
=======
        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            return;

        if (currentAction is AxisActions)
            ((AxisActions)currentAction).AxisAntiDeadZone = (int)SliderUMCAntiDeadzone.Value;

<<<<<<< HEAD
        RequestUpdate();
=======
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void Slider_GyroWeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
<<<<<<< HEAD
        if (currentProfile is null)
=======
        if (selectedProfile is null)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            return;

        if (updateLock)
            return;

<<<<<<< HEAD
        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
=======
        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            return;

        if (currentAction is AxisActions)
            ((AxisActions)currentAction).gyroWeight = (float)Slider_GyroWeight.Value;

<<<<<<< HEAD
        RequestUpdate();
=======
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

<<<<<<< HEAD
        currentProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
        RequestUpdate();
=======
        selectedProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

<<<<<<< HEAD
        currentProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
        RequestUpdate();
=======
        selectedProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void TriggerCreated(Hotkey hotkey)
    {
        switch (hotkey.inputsHotkey.Listener)
        {
            case "shortcutProfilesPage@@":
                {
                    var hotkeyBorder = hotkey.GetControl();
                    if (hotkeyBorder is null || hotkeyBorder.Parent is not null)
                        return;

                    // pull hotkey
                    GyroHotkey = hotkey;

                    UMC_Activator.Children.Add(hotkeyBorder);
                }
                break;
        }
    }

    private void TriggerUpdated(string listener, InputsChord inputs, InputsManager.ListenerType type)
    {
        if (selectedProfile is null)
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        // no Monitor on threaded calls ?
        switch (listener)
        {
            case "shortcutProfilesPage@":
            case "shortcutProfilesPage@@":
                ((GyroActions)currentAction).MotionTrigger = inputs.State.Clone() as ButtonState;
<<<<<<< HEAD
                RequestUpdate();
=======
                UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                break;
        }
    }

    private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1 || selectedProfile is null)
            return;

        if (updateLock)
<<<<<<< HEAD
            return;

        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        ((GyroActions)currentAction).MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;
        RequestUpdate();
    }

    private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.GPUOverrideEnabled = GPUToggle.IsOn;
        RequestUpdate();
    }

    private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.GPUOverrideValue = (int)GPUSlider.Value;
        RequestUpdate();
=======
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        ((GyroActions)currentAction).MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void FramerateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (FramerateToggle.IsOn)
            {
                FramerateSlider_ValueChanged(null, null);
            }
            else
            {
                foreach (Control control in FramerateModeGrid.Children)
                {
<<<<<<< HEAD
                    if (control.GetType() != typeof(Label))
=======
                    if (control is not Label)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                        continue;

                    control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
                }
            }
        });

<<<<<<< HEAD
        if (currentProfile is null)
=======
        if (selectedProfile is null)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            return;

        if (updateLock)
            return;

<<<<<<< HEAD
        currentProfile.FramerateEnabled = FramerateToggle.IsOn;
        RequestUpdate();
=======
        selectedProfile.FramerateEnabled = FramerateToggle.IsOn;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var value = (int)FramerateSlider.Value;

            foreach (Control control in FramerateModeGrid.Children)
            {
<<<<<<< HEAD
                if (control.GetType() != typeof(Label))
=======
                if (control is not Label)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                    continue;

                control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
            }

<<<<<<< HEAD
            Label Label = (Label)FramerateModeGrid.Children[value];
            Label.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
=======
            Label label = (Label)FramerateModeGrid.Children[value];
            label.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        });

        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

<<<<<<< HEAD
        currentProfile.FramerateValue = (int)FramerateSlider.Value;
        RequestUpdate();
=======
        selectedProfile.FramerateValue = (int)FramerateSlider.Value;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void RSRToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

<<<<<<< HEAD
        if (updateLock)
            return;

        currentProfile.EPPOverrideEnabled = EPPToggle.IsOn;
        RequestUpdate();
=======
        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.RSREnabled = RSRToggle.IsOn;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void RSRSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

<<<<<<< HEAD
        if (updateLock)
            return;

        currentProfile.EPPOverrideValue = (uint)EPPSlider.Value;
        RequestUpdate();
    }

    private void RSRToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.RSREnabled = RSRToggle.IsOn;
        RequestUpdate();
    }

    private void RSRSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

=======
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        if (!RSRSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

<<<<<<< HEAD
        currentProfile.RSRSharpness = (int)RSRSlider.Value;
        RequestUpdate();
    }

    private void CPUCoreToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.CPUCoreEnabled = CPUCoreToggle.IsOn;
        RequestUpdate();
    }

    private void CPUCoreSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (!CPUCoreSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.CPUCoreCount = (int)CPUCoreSlider.Value;
        RequestUpdate();
=======
        selectedProfile.RSRSharpness = (int)RSRSlider.Value;
        UpdateProfile();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }

    private void Toggle_ControllerLayout_Toggled(object sender, RoutedEventArgs e)
    {
<<<<<<< HEAD
        if (currentProfile is null)
=======
        if (selectedProfile is null)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            return;

        // wait until lock is released
        if (updateLock)
            return;

<<<<<<< HEAD
        currentProfile.LayoutEnabled = Toggle_ControllerLayout.IsOn;
        RequestUpdate();
=======
        selectedProfile.LayoutEnabled = Toggle_ControllerLayout.IsOn;
        UpdateProfile();
    }

    private void Button_PowerSettings_Create_Click(object sender, RoutedEventArgs e)
    {
        int idx = PowerProfileManager.profiles.Values.Where(p => !p.IsDefault()).Count() + 1;

        string Name = string.Format(Properties.Resources.PowerProfileManualName, idx);
        PowerProfile powerProfile = new PowerProfile(Name, Properties.Resources.PowerProfileManualDescription);

        PowerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
    }
}