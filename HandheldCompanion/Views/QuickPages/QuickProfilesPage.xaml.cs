using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;
using Separator = System.Windows.Controls.Separator;
using Timer = System.Timers.Timer;

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

    private CrossThreadLock profileLock = new();
    private CrossThreadLock foregroundLock = new();
    private CrossThreadLock graphicLock = new();

    private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_ACTIVATION_QP;
    private Hotkey GyroHotkey = new(gyroButtonFlags) { IsInternal = true };

    private Profile realProfile;

    public QuickProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickProfilesPage()
    {
        DataContext = new QuickProfilesPageViewModel();
        InitializeComponent();

        // manage events
        ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        ProfileManager.Applied += ProfileManager_Applied;
        ProfileManager.Deleted += ProfileManager_Deleted;
        PowerProfileManager.Updated += PowerProfileManager_Updated;
        PowerProfileManager.Deleted += PowerProfileManager_Deleted;
        MultimediaManager.Initialized += MultimediaManager_Initialized;
        MultimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        HotkeysManager.Updated += HotkeysManager_Updated;
        PlatformManager.RTSS.Updated += RTSS_Updated;
        GPUManager.Hooked += GPUManager_Hooked;
        GPUManager.Unhooked += GPUManager_Unhooked;

        foreach (var mode in Enum.GetValues<MotionOutput>())
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
            var icon = new FontIcon() { Glyph = mode.ToGlyph() };

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
            var icon = new FontIcon() { Glyph = mode.ToGlyph() };

            if (!string.IsNullOrEmpty(icon.Glyph))
                simpleStackPanel.Children.Add(icon);

            // create textblock
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };

            simpleStackPanel.Children.Add(text);

            comboBoxItem.Content = simpleStackPanel;
            cB_Input.Items.Add(comboBoxItem);
        }

        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();

        // force call
        RTSS_Updated(PlatformManager.RTSS.Status);

        // store hotkey to manager
        HotkeysManager.UpdateOrCreateHotkey(GyroHotkey);
    }

    private void MultimediaManager_Initialized()
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            DesktopScreen desktopScreen = MultimediaManager.PrimaryDesktop;
            desktopScreen.screenDividers.ForEach(d => IntegerScalingComboBox.Items.Add(d));
        });
    }

    private void GPUManager_Hooked(GPU GPU)
    {
        bool HasRSRSupport = false;
        if (GPU is AMDGPU amdGPU)
        {
            amdGPU.RSRStateChanged += OnRSRStateChanged;
            HasRSRSupport = amdGPU.HasRSRSupport();
        }

        GPU.IntegerScalingChanged += OnIntegerScalingChanged;
        GPU.GPUScalingChanged += OnGPUScalingChanged;

        bool HasScalingModeSupport = GPU.HasScalingModeSupport();
        bool HasIntegerScalingSupport = GPU.HasIntegerScalingSupport();
        bool HasGPUScalingSupport = GPU.HasGPUScalingSupport();
        bool IsGPUScalingEnabled = GPU.GetGPUScaling();

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            // GPU-specific settings
            StackProfileRSR.Visibility = GPU is AMDGPU ? Visibility.Visible : Visibility.Collapsed;
            IntegerScalingTypeGrid.Visibility = GPU is IntelGPU ? Visibility.Visible : Visibility.Collapsed;

            StackProfileRSR.IsEnabled = HasGPUScalingSupport && IsGPUScalingEnabled && HasRSRSupport;
            StackProfileIS.IsEnabled = HasGPUScalingSupport && IsGPUScalingEnabled && HasIntegerScalingSupport;
            StackProfileRIS.IsEnabled = HasGPUScalingSupport; // check if processor is AMD should be enough
            GPUScalingToggle.IsEnabled = HasGPUScalingSupport;
            GPUScalingComboBox.IsEnabled = HasGPUScalingSupport && HasScalingModeSupport;

            CurrentDeviceName.Text = GPU.adapterInformation.Details.Description;
        });
    }

    private void GPUManager_Unhooked(GPU GPU)
    {
        if (GPU is AMDGPU amdGPU)
            amdGPU.RSRStateChanged -= OnRSRStateChanged;

        GPU.IntegerScalingChanged -= OnIntegerScalingChanged;
        GPU.GPUScalingChanged -= OnGPUScalingChanged;

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            StackProfileRSR.IsEnabled = false;
            StackProfileIS.IsEnabled = false;
            GPUScalingToggle.IsEnabled = false;
            GPUScalingComboBox.IsEnabled = false;
        });
    }

    private void OnRSRStateChanged(bool Supported, bool Enabled, int Sharpness)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            StackProfileRSR.IsEnabled = Supported;
        });
    }

    private void OnGPUScalingChanged(bool Supported, bool Enabled, int Mode)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            GPUScalingToggle.IsEnabled = Supported;
            StackProfileRIS.IsEnabled = Supported; // check if processor is AMD should be enough
            StackProfileRSR.IsEnabled = Supported;
            StackProfileIS.IsEnabled = Supported;
        });
    }

    private void OnIntegerScalingChanged(bool Supported, bool Enabled)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            StackProfileIS.IsEnabled = Supported;
        });
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    var Processor = PerformanceManager.GetProcessor();
                    StackProfileFramerate.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    // StackProfileFramerate.IsEnabled = false;
                    // StackProfileAutoTDP.IsEnabled = false;
                    break;
            }
        });
    }

    private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        if (selectedProfile is null)
            return;

        List<ScreenFramelimit> frameLimits = desktopScreen.GetFramelimits();

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            cB_Framerate.Items.Clear();

            foreach (ScreenFramelimit frameLimit in frameLimits)
                cB_Framerate.Items.Add(frameLimit);

            cB_Framerate.SelectedItem = desktopScreen.GetClosest(selectedProfile.FramerateValue);
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
        Application.Current.Dispatcher.Invoke(() =>
        {
            int idx = -1;
            foreach (var item in ProfileStack.Children)
            {
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
        Application.Current.Dispatcher.Invoke(() =>
        {
            int idx = -1;
            foreach (var item in ProfileStack.Children)
            {
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
                if (button is not null)
                    button.Click += (sender, e) => PowerProfile_Clicked(powerProfile);

                RadioButton radioButton = powerProfile.GetRadioButton(this);
                if (radioButton is not null)
                    radioButton.Checked += (sender, e) => PowerProfile_Selected(powerProfile);

                // add new entry
                ProfileStack.Children.Add(button);
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
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            SelectedPowerProfileName.Text = powerProfile.Name;
        });

        // update profile
        selectedProfile.PowerProfile = powerProfile.Guid;
        UpdateProfile();
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        switch (source)
        {
            // self update, unlock and exit
            case UpdateSource.QuickProfilesPage:
                return;
        }

        if (profileLock.TryEnter())
        {
            try
            {
                // if an update is pending, execute it and stop timer
                if (UpdateTimer.Enabled)
                {
                    UpdateTimer.Stop();
                    SubmitProfile();
                }

                // update profile
                selectedProfile = profile;

                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // update profile name
                    CurrentProfileName.Text = selectedProfile.Name;
                    Toggle_ControllerLayout.IsOn = selectedProfile.LayoutEnabled;
                    Toggle_ControllerLayout.IsEnabled = !selectedProfile.Default;

                    // sub profiles
                    cb_SubProfiles.Items.Clear();
                    int selectedIndex = 0;

                    if (profile.Default)
                    {
                        cb_SubProfiles.Items.Add(profile);
                        cb_SubProfiles.IsEnabled = false;
                    }
                    else
                    {
                        Profile mainProfile = ProfileManager.GetProfileForSubProfile(selectedProfile);
                        Profile[] subProfiles = ProfileManager.GetSubProfilesFromPath(selectedProfile.Path, false);

                        cb_SubProfiles.Items.Add(mainProfile);
                        foreach (Profile subProfile in subProfiles)
                        {
                            cb_SubProfiles.Items.Add(subProfile);
                            if (subProfile.Guid == selectedProfile.Guid)
                                selectedIndex = cb_SubProfiles.Items.IndexOf(subProfile);
                        }
                        cb_SubProfiles.IsEnabled = true;
                    }

                    cb_SubProfiles.SelectedIndex = selectedIndex;

                    // power profile
                    PowerProfile powerProfile = PowerProfileManager.GetProfile(profile.PowerProfile);
                    powerProfile?.Check(this);
                    SelectedPowerProfileName.Text = powerProfile?.Name;

                    // gyro layout
                    if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
                    {
                        // no gyro layout available, mark as disabled
                        cB_Output.SelectedIndex = (int)MotionOutput.Disabled;
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

                        GyroHotkey.inputsChord.ButtonState = ((GyroActions)currentAction).MotionTrigger.Clone() as ButtonState;
                        HotkeysManager.UpdateOrCreateHotkey(GyroHotkey);
                    }

                    // Framerate limit
                    DesktopScreen? desktopScreen = MultimediaManager.PrimaryDesktop;
                    if (desktopScreen is not null)
                        cB_Framerate.SelectedItem = desktopScreen.GetClosest(selectedProfile.FramerateValue);

                    // GPU Scaling
                    GPUScalingToggle.IsOn = selectedProfile.GPUScaling;
                    GPUScalingComboBox.SelectedIndex = selectedProfile.ScalingMode;

                    // RSR
                    RSRToggle.IsOn = selectedProfile.RSREnabled;
                    RSRSlider.Value = selectedProfile.RSRSharpness;

                    // Integer Scaling
                    IntegerScalingToggle.IsOn = selectedProfile.IntegerScalingEnabled;
                    IntegerScalingTypeComboBox.SelectedIndex = selectedProfile.IntegerScalingType;

                    if (desktopScreen is not null)
                        IntegerScalingComboBox.SelectedItem = desktopScreen.screenDividers.FirstOrDefault(d => d.divider == selectedProfile.IntegerScalingDivider);

                    // RIS
                    RISToggle.IsOn = selectedProfile.RISEnabled;
                    RISSlider.Value = selectedProfile.RISSharpness;
                });
            }
            finally
            {
                profileLock.Exit();
            }
        }
    }

    private void ProfileManager_Deleted(Profile profile)
    {
        // this shouldn't happen, someone removed the currently applied profile
        if (selectedProfile == profile)
            ProcessManager_ForegroundChanged(currentProcess, null);
    }

    private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx)
    {
        if (foregroundLock.TryEnter())
        {
            try
            {
                // update current process
                currentProcess = processEx;

                // get path
                string path = currentProcess != null ? currentProcess.Path : string.Empty;
                ImageSource imageSource = currentProcess != null ? currentProcess.ProcessIcon : null;
                nint handle = currentProcess != null ? currentProcess.MainWindowHandle : IntPtr.Zero;

                // update real profile
                realProfile = ProfileManager.GetProfileFromPath(path, true);

                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProfileToggle.IsOn = !realProfile.Default && realProfile.Enabled;
                    ProfileIcon.Source = imageSource;

                    if (handle != IntPtr.Zero)
                    {
                        // string MainWindowTitle = ProcessUtils.GetWindowTitle(processEx.MainWindowHandle);

                        ProfileToggle.IsEnabled = true;
                        ProcessName.Text = currentProcess.Executable;
                        ProcessPath.Text = currentProcess.Path;
                        SubProfilesBorder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ProfileIcon.Source = null;

                        ProfileToggle.IsEnabled = false;
                        ProcessName.Text = Properties.Resources.QuickProfilesPage_Waiting;
                        ProcessPath.Text = string.Empty;
                        SubProfilesBorder.Visibility = Visibility.Collapsed;
                    }
                });
            }
            finally
            {
                foregroundLock.Exit();
            }
        }
    }

    private void UpdateProfile()
    {
        UpdateTimer.Stop();
        UpdateTimer.Start();
    }

    private void ProfileToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (foregroundLock.IsEntered())
            return;

        // update real profile
        realProfile = ProfileManager.GetProfileFromPath(realProfile.Path, true);
        if (realProfile is null)
            return;

        if (realProfile.Default)
        {
            CreateProfile();
        }
        else
        {
            realProfile.Enabled = ProfileToggle.IsOn;
            ProfileManager.UpdateOrCreateProfile(realProfile, UpdateSource.QuickProfilesCreation);
        }
    }

    private void CreateProfile()
    {
        if (currentProcess is null || currentProcess == ProcessManager.Empty)
            return;

        // create profile
        selectedProfile = new Profile(currentProcess.Path);
        selectedProfile.Layout = (ProfileManager.GetProfileWithDefaultLayout()?.Layout ?? LayoutTemplate.DefaultLayout.Layout).Clone() as Layout;
        selectedProfile.LayoutTitle = LayoutTemplate.DesktopLayout.Name;

        // if an update is pending, execute it and stop timer
        if (UpdateTimer.Enabled)
            UpdateTimer.Stop();

        SubmitProfile(UpdateSource.QuickProfilesCreation);
    }

    private void cB_Input_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Input.SelectedIndex == -1)
            return;

        MotionInput input = (MotionInput)cB_Input.SelectedIndex;
        Text_InputHint.Text = EnumUtils.GetDescriptionFromEnumValue(input, string.Empty, "Desc");

        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        ((GyroActions)currentAction).MotionInput = (MotionInput)cB_Input.SelectedIndex;
        UpdateProfile();
    }

    private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        // try get current actions, if exists
        selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions gyroActions);

        MotionOutput motionOuput = (MotionOutput)cB_Output.SelectedIndex;
        switch (motionOuput)
        {
            case MotionOutput.Disabled:
                selectedProfile.Layout.RemoveLayout(AxisLayoutFlags.Gyroscope);
                gyroActions = null;
                break;

            case MotionOutput.LeftStick:
            case MotionOutput.RightStick:
                {
                    if (gyroActions is null || gyroActions is not AxisActions)
                    {
                        gyroActions = new AxisActions()
                        {
                            AxisAntiDeadZone = GyroActions.DefaultAxisAntiDeadZone
                        };
                    }

                    ((AxisActions)gyroActions).Axis = motionOuput == MotionOutput.LeftStick ? AxisLayoutFlags.LeftStick : AxisLayoutFlags.RightStick;

                    ((AxisActions)gyroActions).MotionTrigger = GyroHotkey.inputsChord.ButtonState.Clone() as ButtonState;
                }
                break;

            case MotionOutput.MoveCursor:
            case MotionOutput.ScrollWheel:
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

                    ((MouseActions)gyroActions).MotionTrigger = GyroHotkey.inputsChord.ButtonState.Clone() as ButtonState;
                }
                break;
        }

        // proper layout update
        if (gyroActions is not null)
            selectedProfile.Layout.UpdateLayout(AxisLayoutFlags.Gyroscope, gyroActions);

        SubmitProfile(UpdateSource.Creation);
    }

    private void SliderUMCAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        if (currentAction is AxisActions)
            ((AxisActions)currentAction).AxisAntiDeadZone = (int)SliderUMCAntiDeadzone.Value;

        UpdateProfile();
    }

    private void Slider_GyroWeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        if (currentAction is AxisActions)
            ((AxisActions)currentAction).gyroWeight = (float)Slider_GyroWeight.Value;

        UpdateProfile();
    }

    private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
        UpdateProfile();
    }

    private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
        UpdateProfile();
    }

    private void HotkeysManager_Updated(Hotkey hotkey)
    {
        if (selectedProfile is null)
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        if (hotkey.ButtonFlags != gyroButtonFlags)
            return;

        // update gyro hotkey
        GyroHotkey = hotkey;

        ((GyroActions)currentAction).MotionTrigger = hotkey.inputsChord.ButtonState.Clone() as ButtonState;
        UpdateProfile();
    }

    private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        ((GyroActions)currentAction).MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;
        UpdateProfile();
    }

    private void RSRToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.RadeonSuperResolution, RSRToggle.IsOn);
        UpdateProfile();
    }

    private void RSRSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (!RSRSlider.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        selectedProfile.RSRSharpness = (int)RSRSlider.Value;
        UpdateProfile();
    }

    private void RISToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.RadeonImageSharpening, RISToggle.IsOn);
        UpdateProfile();
    }

    private void RISSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (!RISSlider.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        selectedProfile.RISSharpness = (int)RISSlider.Value;
        UpdateProfile();
    }

    private void Toggle_ControllerLayout_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.LayoutEnabled = Toggle_ControllerLayout.IsOn;
        UpdateProfile();
    }

    private void IntegerScalingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.IntegerScaling, IntegerScalingToggle.IsOn);
        UpdateProfile();
    }

    private void GPUScalingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GPUScalingComboBox.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        int selectedIndex = GPUScalingComboBox.SelectedIndex;
        // RSR does not work with ScalingMode.Center
        if (selectedProfile.RSREnabled && selectedIndex == 2)
        {
            selectedProfile.ScalingMode = 1;
            GPUScalingComboBox.SelectedIndex = 1;
        }
        else
        {
            selectedProfile.ScalingMode = GPUScalingComboBox.SelectedIndex;
        }
        UpdateProfile();
    }

    private void GPUScaling_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.GPUScaling, GPUScalingToggle.IsOn);
        UpdateProfile();
    }

    private void IntegerScalingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IntegerScalingComboBox.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        var divider = 1;
        if (IntegerScalingComboBox.SelectedItem is ScreenDivider screenDivider)
        {
            divider = screenDivider.divider;
        }

        selectedProfile.IntegerScalingDivider = divider;
        UpdateProfile();
    }

    private void IntegerScalingTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IntegerScalingTypeComboBox.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        selectedProfile.IntegerScalingType = (byte)IntegerScalingTypeComboBox.SelectedIndex;
        UpdateProfile();
    }

    private void Button_PowerSettings_Create_Click(object sender, RoutedEventArgs e)
    {
        int idx = PowerProfileManager.profiles.Values.Where(p => !p.IsDefault()).Count() + 1;

        string Name = string.Format(Properties.Resources.PowerProfileManualName, idx);
        PowerProfile powerProfile = new PowerProfile(Name, Properties.Resources.PowerProfileManualDescription)
        {
            TDPOverrideValues = IDevice.GetCurrent().nTDP
        };

        PowerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);
    }

    private void cb_SubProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        // return if combobox selected item is null
        if (cb_SubProfiles.SelectedIndex == -1)
            return;

        selectedProfile = (Profile)cb_SubProfiles.SelectedItem;
        UpdateProfile();
    }

    private void cB_Framerate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        // return if combobox selected item is null
        if (cB_Framerate.SelectedIndex == -1)
            return;

        if (cB_Framerate.SelectedItem is ScreenFramelimit screenFramelimit)
        {
            selectedProfile.FramerateValue = screenFramelimit.limit;
            UpdateProfile();
        }
    }

    private enum UpdateGraphicsSettingsSource
    {
        GPUScaling,
        RadeonSuperResolution,
        RadeonImageSharpening,
        IntegerScaling
    }

    private void UpdateGraphicsSettings(UpdateGraphicsSettingsSource source, bool isEnabled)
    {
        if (graphicLock.TryEnter())
        {
            try
            {
                switch (source)
                {
                    case UpdateGraphicsSettingsSource.GPUScaling:
                        {
                            selectedProfile.GPUScaling = isEnabled;
                            if (!isEnabled)
                            {
                                selectedProfile.RSREnabled = false;
                                selectedProfile.IntegerScalingEnabled = false;

                                RSRToggle.IsOn = false;
                                IntegerScalingToggle.IsOn = false;
                            }
                        }
                        break;
                    // RSR is incompatible with RIS and IS
                    case UpdateGraphicsSettingsSource.RadeonSuperResolution:
                        {
                            selectedProfile.RSREnabled = isEnabled;
                            if (isEnabled)
                            {
                                selectedProfile.RISEnabled = false;
                                selectedProfile.IntegerScalingEnabled = false;

                                RISToggle.IsOn = false;
                                IntegerScalingToggle.IsOn = false;

                                // RSR does not support ScalingMode.Center
                                if (selectedProfile.ScalingMode == 2)
                                {
                                    selectedProfile.ScalingMode = 1;
                                    GPUScalingComboBox.SelectedIndex = 1;
                                }
                            }
                        }
                        break;
                    // Image Sharpening is incompatible with RSR
                    case UpdateGraphicsSettingsSource.RadeonImageSharpening:
                        {
                            selectedProfile.RISEnabled = isEnabled;
                            if (isEnabled)
                            {
                                selectedProfile.RSREnabled = false;

                                RSRToggle.IsOn = false;
                            }
                        }
                        break;

                    // Integer Scaling is incompatible with RSR
                    case UpdateGraphicsSettingsSource.IntegerScaling:
                        {
                            selectedProfile.IntegerScalingEnabled = isEnabled;
                            if (isEnabled)
                            {
                                selectedProfile.RSREnabled = false;

                                RSRToggle.IsOn = false;
                            }
                        }
                        break;
                }
            }
            finally
            {
                graphicLock.Exit();
            }
        }
    }
}