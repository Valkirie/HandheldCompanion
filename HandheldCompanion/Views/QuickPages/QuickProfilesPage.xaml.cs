using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerCommon.Platforms;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using Inkore.UI.WPF.Modern.Controls;
using Layout = ControllerCommon.Layout;
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

    private bool profileLock;

    private Hotkey ProfilesPageHotkey = new(61);
    private Profile realProfile;

    public QuickProfilesPage()
    {
        InitializeComponent();

        ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

        ProfileManager.Applied += ProfileApplied;

        SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;
        HotkeysManager.HotkeyCreated += TriggerCreated;

        InputsManager.TriggerUpdated += TriggerUpdated;

        MainWindow.performanceManager.ProcessorStatusChanged += PerformanceManager_StatusChanged;
        MainWindow.performanceManager.EPPChanged += PerformanceManager_EPPChanged;

        foreach (var mode in (MotionInput[])Enum.GetValues(typeof(MotionInput)))
        {
            // create panel
            var panel = new SimpleStackPanel
                { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // create icon
            var icon = new FontIcon { Glyph = "" };

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
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };
            panel.Children.Add(text);

            cB_Input.Items.Add(panel);
        }

        foreach (var mode in (MotionOutput[])Enum.GetValues(typeof(MotionOutput)))
        {
            // create panel
            var panel = new SimpleStackPanel
                { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // create icon
            var icon = new FontIcon { Glyph = "" };

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
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };
            panel.Children.Add(text);

            cB_Output.Items.Add(panel);
        }

        // device settings
        GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
        GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();

        PlatformManager.RTSS.Updated += RTSS_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
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
                    StackProfileFramerate.IsEnabled = false;
                    StackProfileAutoTDP.IsEnabled = false;
                    break;
            }
        });
    }

    private void DesktopManager_DisplaySettingsChanged(ScreenResolution resolution)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var screenFrequency = SystemManager.GetDesktopScreen().GetFrequency();

            FramerateQuarter.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Quarter));
            FramerateThird.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Third));
            FramerateHalf.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Half));
            FramerateFull.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Full));
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
                case "ConfigurableTDPOverrideUp":
                    TDPSlider.Maximum = Convert.ToInt32(value);
                    break;
                case "ConfigurableTDPOverrideDown":
                    TDPSlider.Minimum = Convert.ToInt32(value);
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
            currentProfile = profile.Clone() as Profile;

            // UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // set lock
                profileLock = true;

                UMCToggle.IsOn = currentProfile.MotionEnabled;
                cB_Input.SelectedIndex = (int)currentProfile.MotionInput;
                cB_Output.SelectedIndex = (int)currentProfile.MotionOutput;
                cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)currentProfile.MotionMode;

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

                // Slider settings
                SliderUMCAntiDeadzone.Value = currentProfile.MotionAntiDeadzone;
                SliderSensitivityX.Value = currentProfile.MotionSensivityX;
                SliderSensitivityY.Value = currentProfile.MotionSensivityY;

                // EPP
                EPPToggle.IsOn = currentProfile.EPPOverrideEnabled;
                EPPSlider.Value = currentProfile.EPPOverrideValue;

                // todo: improve me ?
                ProfilesPageHotkey.inputsChord.State = currentProfile.MotionTrigger.Clone() as ButtonState;
                ProfilesPageHotkey.DrawInput();

                // release lock
                profileLock = false;
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
            profileLock = true;

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

            // release lock
            profileLock = false;
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

        if (!profileLock)
        {
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

        if (!profileLock)
        {
            currentProfile.MotionEnabled = UMCToggle.IsOn;
            RequestUpdate();
        }
    }

    private void cB_Input_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Input.SelectedIndex == -1)
            return;

        var input = (MotionInput)cB_Input.SelectedIndex;

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

        if (!profileLock)
        {
            currentProfile.MotionInput = (MotionInput)cB_Input.SelectedIndex;
            RequestUpdate();
        }
    }

    private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.MotionOutput = (MotionOutput)cB_Output.SelectedIndex;
            RequestUpdate();
        }
    }

    private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            // TDP and AutoTDP are mutually exclusive
            var toggled = TDPToggle.IsOn;
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

        if (!profileLock)
        {
            currentProfile.TDPOverrideValues = new double[3];
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

        if (!profileLock)
        {
            // TDP and AutoTDP are mutually exclusive
            var toggled = AutoTDPToggle.IsOn;
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

        if (!profileLock)
        {
            currentProfile.AutoTDPRequestedFPS = (int)AutoTDPRequestedFPSSlider.Value;
            RequestUpdate();
        }
    }

    private void SliderUMCAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.MotionAntiDeadzone = (float)SliderUMCAntiDeadzone.Value;
            RequestUpdate();
        }
    }

    private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
            RequestUpdate();
        }
    }

    private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
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
                var hotkeyBorder = hotkey.GetControl();
                if (hotkeyBorder is null || hotkeyBorder.Parent is not null)
                    return;

                // pull hotkey
                ProfilesPageHotkey = hotkey;

                UMC_Activator.Children.Add(hotkeyBorder);
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

        if (!profileLock)
        {
            currentProfile.MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;
            RequestUpdate();
        }
    }

    private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.GPUOverrideEnabled = GPUToggle.IsOn;
            RequestUpdate();
        }
    }

    private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.GPUOverrideValue = (int)GPUSlider.Value;
            RequestUpdate();
        }
    }

    private void FramerateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.FramerateEnabled = FramerateToggle.IsOn;

            RequestUpdate();
        }
    }

    private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var value = (int)FramerateSlider.Value;

            foreach (TextBlock tb in FramerateModeGrid.Children)
                tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

            var TextBlock = (TextBlock)FramerateModeGrid.Children[value];
            TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
        });

        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.FramerateValue = (int)FramerateSlider.Value;
            RequestUpdate();
        }
    }

    private void EPPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.EPPOverrideEnabled = EPPToggle.IsOn;

            RequestUpdate();
        }
    }

    private void EPPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (currentProfile is null)
            return;

        if (!profileLock)
        {
            currentProfile.EPPOverrideValue = (uint)EPPSlider.Value;
            RequestUpdate();
        }
    }
}