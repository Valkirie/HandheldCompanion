using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Platforms;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickProfilesPage.xaml
/// </summary>
public partial class QuickProfilesPage : Page
{
    private const int UpdateInterval = 500;
    private readonly Timer UpdateTimer;
    private ProcessEx currentProcess;
    private Profile currentProfile;

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

        SystemManager.DisplaySettingsChanged += SystemManager_DisplaySettingsChanged;
        SystemManager.RSRStateChanged += SystemManager_RSRStateChanged;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;
        HotkeysManager.HotkeyCreated += TriggerCreated;

        InputsManager.TriggerUpdated += TriggerUpdated;

        MainWindow.performanceManager.ProcessorStatusChanged += PerformanceManager_StatusChanged;
        MainWindow.performanceManager.EPPChanged += PerformanceManager_EPPChanged;

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

            comboBoxItem.Content = simpleStackPanel;
            cB_Input.Items.Add(comboBoxItem);
        }

        // device settings
        GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
        GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

        // motherboard settings
        CPUCoreSlider.Maximum = MotherboardInfo.NumberOfCores;

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
                    StackProfileAutoTDP.IsEnabled = true && Processor is not null ? Processor.CanChangeTDP : false;
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

    private void PerformanceManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            StackProfileTDP.IsEnabled = CanChangeTDP;
            StackProfileAutoTDP.IsEnabled = CanChangeTDP && PlatformManager.RTSS.IsInstalled;

            StackProfileGPU.IsEnabled = CanChangeGPU;
        });
    }

    private void PerformanceManager_EPPChanged(uint EPP)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() => { EPPSlider.Value = EPP; });
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
            }
        });
    }

    private void ProfileApplied(Profile profile, ProfileUpdateSource source)
    {
        if (true)
        {
            switch (source)
            {
                // self update, unlock and exit
                case ProfileUpdateSource.QuickProfilesPage:
                case ProfileUpdateSource.Serializer:
                    return;
            }

            // if an update is pending, execute it and stop timer
            if (UpdateTimer.Enabled)
            {
                UpdateTimer.Stop();
                SubmitProfile();
            }

            // update profile
            currentProfile = profile;

            // UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                using (new ScopedLock(updateLock))
                {
                    // update profile name
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

                        if (currentAction is AxisActions)
                        {
                            cB_Output.SelectedIndex = (int)((AxisActions)currentAction).Axis;
                            SliderUMCAntiDeadzone.Value = ((AxisActions)currentAction).AxisAntiDeadZone;
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
                // enable UI
                GridProfile.IsEnabled = true;

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
                    // disable UI
                    GridProfile.IsEnabled = false;

                    ProfileIcon.Source = null;

                    ProfileToggle.IsEnabled = false;
                    ProcessName.Text = Properties.Resources.QuickProfilesPage_Waiting;
                    ProcessPath.Text = string.Empty;
                }
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
            ProfileManager.UpdateOrCreateProfile(realProfile, ProfileUpdateSource.Creation);
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

    private void cB_Input_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Input.SelectedIndex == -1)
            return;

        var input = (MotionInput)cB_Input.SelectedIndex;
        Text_InputHint.Text = Profile.InputDescription[input];

        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        ((GyroActions)currentAction).MotionInput = (MotionInput)cB_Input.SelectedIndex;
        RequestUpdate();
    }

    private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
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

        // TDP and AutoTDP are mutually exclusive
        var toggled = TDPToggle.IsOn;
        if (toggled)
            AutoTDPToggle.IsOn = false;

        currentProfile.TDPOverrideEnabled = toggled;
        RequestUpdate();
    }

    private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

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

        // TDP and AutoTDP are mutually exclusive
        var toggled = AutoTDPToggle.IsOn;
        if (toggled)
            TDPToggle.IsOn = false;

        currentProfile.AutoTDPEnabled = toggled;
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
    }

    private void SliderUMCAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        if (currentAction is AxisActions)
            ((AxisActions)currentAction).AxisAntiDeadZone = (int)SliderUMCAntiDeadzone.Value;

        RequestUpdate();
    }

    private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
        RequestUpdate();
    }

    private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
        RequestUpdate();
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
        if (currentProfile is null)
            return;

        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        // no Monitor on threaded calls ?
        switch (listener)
        {
            case "shortcutProfilesPage@":
            case "shortcutProfilesPage@@":
                ((GyroActions)currentAction).MotionTrigger = inputs.State.Clone() as ButtonState;
                RequestUpdate();
                break;
        }
    }

    private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1 || currentProfile is null)
            return;

        if (updateLock)
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
                    if (control.GetType() != typeof(Label))
                        continue;

                    control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
                }
            }
        });

        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.FramerateEnabled = FramerateToggle.IsOn;
        RequestUpdate();
    }

    private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var value = (int)FramerateSlider.Value;

            foreach (Control control in FramerateModeGrid.Children)
            {
                if (control.GetType() != typeof(Label))
                    continue;

                control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
            }

            Label Label = (Label)FramerateModeGrid.Children[value];
            Label.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
        });

        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.FramerateValue = (int)FramerateSlider.Value;
        RequestUpdate();
    }

    private void EPPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (updateLock)
            return;

        currentProfile.EPPOverrideEnabled = EPPToggle.IsOn;
        RequestUpdate();
    }

    private void EPPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

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

        if (!RSRSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

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
    }

    private void Toggle_ControllerLayout_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.LayoutEnabled = Toggle_ControllerLayout.IsOn;
        RequestUpdate();
    }
}