using HandheldCompanion.Actions;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static HandheldCompanion.GraphicsProcessingUnit.GPU;
using Page = System.Windows.Controls.Page;
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
    private CrossThreadLock multimediaLock = new();
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
        DataContext = new QuickProfilesPageViewModel(this);
        InitializeComponent();

        // manage events
        ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        ManagerFactory.profileManager.Applied += ProfileManager_Applied;
        ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;
        ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;
        ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;
        ManagerFactory.gpuManager.Hooked += GPUManager_Hooked;
        ManagerFactory.gpuManager.Unhooked += GPUManager_Unhooked;
        PlatformManager.RTSS.Updated += RTSS_Updated;

        // raise events
        switch (ManagerFactory.processManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.processManager.Initialized += ProcessManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryForeground();
                break;
        }

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

        UpdateTimer = new Timer(UpdateInterval)
        {
            AutoReset = false
        };
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();

        // force call
        RTSS_Updated(PlatformManager.RTSS.Status);

        // store hotkey to manager
        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(GyroHotkey);
    }

    private void QueryForeground()
    {
        ProcessManager_ForegroundChanged(ProcessManager.GetForegroundProcess(), null);
    }

    private void ProcessManager_Initialized()
    {
        QueryForeground();
    }

    public void Close()
    {
        // manage events
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
        ManagerFactory.processManager.Initialized -= ProcessManager_Initialized;
        ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
        ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;
        ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
        ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
        ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
        ManagerFactory.gpuManager.Hooked -= GPUManager_Hooked;
        ManagerFactory.gpuManager.Unhooked -= GPUManager_Unhooked;
        PlatformManager.RTSS.Updated -= RTSS_Updated;

        ((QuickProfilesPageViewModel)DataContext).Dispose();

        UpdateTimer.Stop();
    }

    private void MultimediaManager_Initialized()
    {
        if (multimediaLock.TryEnter())
        {
            try
            {
                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                    if (desktopScreen is not null)
                        desktopScreen.screenDividers.ForEach(d => IntegerScalingComboBox.Items.Add(d));
                });
            }
            finally
            {
                multimediaLock.Exit();
            }
        }
    }

    private bool HasRSRSupport = false;
    private bool HasAFMFSupport = false;
    private bool HasScalingModeSupport = false;
    private bool HasIntegerScalingSupport = false;
    private bool HasGPUScalingSupport = false;
    private bool IsGPUScalingEnabled = false;

    private void GPUManager_Hooked(GPU GPU)
    {
        HasRSRSupport = false;
        HasAFMFSupport = false;

        if (GPU is AMDGPU amdGPU)
        {
            amdGPU.RSRStateChanged += OnRSRStateChanged;
            HasRSRSupport = amdGPU.HasRSRSupport();

            amdGPU.AFMFStateChanged += OnAFMFStateChanged;
            HasAFMFSupport = amdGPU.HasAFMFSupport();
        }

        GPU.IntegerScalingChanged += OnIntegerScalingChanged;
        GPU.GPUScalingChanged += OnGPUScalingChanged;
        GPU.StatusChanged += OnStatusChanged;

        HasScalingModeSupport = GPU.HasScalingModeSupport();
        HasIntegerScalingSupport = GPU.HasIntegerScalingSupport();
        HasGPUScalingSupport = GPU.HasGPUScalingSupport();
        IsGPUScalingEnabled = GPU.GetGPUScaling();

        // UI thread (async)
        UIHelper.TryInvoke(() =>
        {
            // GPU-specific settings
            StackProfileRSR.Visibility = GPUManager.GetCurrent() is AMDGPU ? Visibility.Visible : Visibility.Collapsed;
            StackProfileAFMF.Visibility = GPUManager.GetCurrent() is AMDGPU ? Visibility.Visible : Visibility.Collapsed;
            IntegerScalingTypeGrid.Visibility = GPU is IntelGPU ? Visibility.Visible : Visibility.Collapsed;
        });

        UpdateGraphicsSettingsUI();
    }

    private void OnStatusChanged(bool status)
    {
        // UI thread (async)
        UIHelper.TryInvoke(() =>
        {
            GraphicsSettingsExpander.IsEnabled = !status;
            GraphicsSettingsRing.Visibility = status ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void GPUManager_Unhooked(GPU GPU)
    {
        if (GPU is AMDGPU amdGPU)
        {
            amdGPU.RSRStateChanged -= OnRSRStateChanged;
            amdGPU.AFMFStateChanged -= OnAFMFStateChanged;
        }

        GPU.IntegerScalingChanged -= OnIntegerScalingChanged;
        GPU.GPUScalingChanged -= OnGPUScalingChanged;
        GPU.StatusChanged -= OnStatusChanged;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // GPU-specific settings
            StackProfileRSR.Visibility = Visibility.Collapsed;
            StackProfileAFMF.Visibility = Visibility.Collapsed;
            IntegerScalingTypeGrid.Visibility = Visibility.Collapsed;

            StackProfileRSR.IsEnabled = false;
            StackProfileAFMF.IsEnabled = false;
            StackProfileGPUScaling.IsEnabled = false;
            StackProfileIS.IsEnabled = false;
            StackProfileRIS.IsEnabled = false;
            GPUScalingComboBox.IsEnabled = false;
        });
    }

    private void UpdateGraphicsSettingsUI()
    {
        // UI thread (async)
        UIHelper.TryInvoke(() =>
        {
            StackProfileRSR.IsEnabled = HasRSRSupport;
            StackProfileAFMF.IsEnabled = HasAFMFSupport;
            StackProfileGPUScaling.IsEnabled = HasGPUScalingSupport;
            StackProfileIS.IsEnabled = HasIntegerScalingSupport;
            StackProfileRIS.IsEnabled = HasGPUScalingSupport; // check if processor is AMD should be enough
            GPUScalingComboBox.IsEnabled = HasScalingModeSupport;
        });
    }

    private void OnRSRStateChanged(bool Supported, bool Enabled, int Sharpness)
    {
        if (Supported != HasRSRSupport)
        {
            HasRSRSupport = Supported;
            UpdateGraphicsSettingsUI();
        }
    }

    private void OnAFMFStateChanged(bool Supported, bool Enabled)
    {
        if (Supported != HasAFMFSupport)
        {
            HasAFMFSupport = Supported;
            UpdateGraphicsSettingsUI();
        }
    }

    private void OnGPUScalingChanged(bool Supported, bool Enabled, int Mode)
    {
        if (Supported != HasGPUScalingSupport)
        {
            HasGPUScalingSupport = Supported;
            UpdateGraphicsSettingsUI();
        }
    }

    private void OnIntegerScalingChanged(bool Supported, bool Enabled)
    {
        if (Supported != HasIntegerScalingSupport)
        {
            HasIntegerScalingSupport = Supported;
            UpdateGraphicsSettingsUI();
        }
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
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
        List<ScreenFramelimit> frameLimits = desktopScreen.GetFramelimits();

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            cB_Framerate.Items.Clear();

            foreach (ScreenFramelimit frameLimit in frameLimits)
                cB_Framerate.Items.Add(frameLimit);

            if (selectedProfile is not null)
                cB_Framerate.SelectedItem = desktopScreen.GetClosest(selectedProfile.FramerateValue);
        });
    }

    public void SubmitProfile(UpdateSource source = UpdateSource.QuickProfilesPage)
    {
        if (selectedProfile is null)
            return;

        ManagerFactory.profileManager.UpdateOrCreateProfile(selectedProfile, source);
    }

    private void PowerProfileOnBatteryMore_Click(object sender, RoutedEventArgs e)
    {
        OverlayQuickTools.GetCurrent().performancePage.SelectionChanged(selectedProfile.PowerProfiles[(int)PowerLineStatus.Offline]);
        OverlayQuickTools.GetCurrent().NavigateToPage("QuickPerformancePage");
    }

    private void PowerProfilePluggedMore_Click(object sender, RoutedEventArgs e)
    {
        OverlayQuickTools.GetCurrent().performancePage.SelectionChanged(selectedProfile.PowerProfiles[(int)PowerLineStatus.Online]);
        OverlayQuickTools.GetCurrent().NavigateToPage("QuickPerformancePage");
    }

    public void PowerProfile_Selected(PowerProfile powerProfile, bool AC)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            switch (AC)
            {
                case false:
                    selectedProfile.PowerProfiles[(int)PowerLineStatus.Offline] = powerProfile.Guid;
                    SelectedPowerProfileName.Text = powerProfile.Name;
                    break;
                case true:
                    selectedProfile.PowerProfiles[(int)PowerLineStatus.Online] = powerProfile.Guid;
                    SelectedPowerProfilePluggedName.Text = powerProfile.Name;
                    break;
            }
        });
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
                UIHelper.TryInvoke(() =>
                {
                    // update profile name
                    CurrentProfileName.Text = selectedProfile.Name;

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
                        Profile mainProfile = ManagerFactory.profileManager.GetProfileForSubProfile(selectedProfile);
                        Profile[] subProfiles = ManagerFactory.profileManager.GetSubProfilesFromPath(selectedProfile.Path, false);

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
                    PowerProfile powerProfileDC = ManagerFactory.powerProfileManager.GetProfile(profile.PowerProfiles[(int)PowerLineStatus.Offline]);
                    PowerProfile powerProfileAC = ManagerFactory.powerProfileManager.GetProfile(profile.PowerProfiles[(int)PowerLineStatus.Online]);

                    SelectedPowerProfileName.Text = powerProfileDC?.Name;
                    SelectedPowerProfilePluggedName.Text = powerProfileAC?.Name;

                    ((QuickProfilesPageViewModel)DataContext).PowerProfileChanged(powerProfileAC, powerProfileDC);

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
                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(GyroHotkey);
                    }

                    // Framerate limit
                    DesktopScreen? desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                    if (desktopScreen is not null)
                        cB_Framerate.SelectedItem = desktopScreen.GetClosest(selectedProfile.FramerateValue);

                    // GPU Scaling
                    GPUScalingToggle.IsOn = selectedProfile.GPUScaling;
                    GPUScalingComboBox.SelectedIndex = selectedProfile.ScalingMode;

                    // RSR
                    RSRToggle.IsOn = selectedProfile.RSREnabled;
                    RSRSlider.Value = selectedProfile.RSRSharpness;

                    // AFMF
                    AFMFToggle.IsOn = selectedProfile.AFMFEnabled;

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
                ImageSource imageSource = currentProcess?.ProcessIcon;

                // update real profile
                realProfile = ManagerFactory.profileManager.GetProfileFromPath(path, true);

                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    ProfileToggle.IsOn = !realProfile.Default && realProfile.Enabled;
                    ProfileIcon.Source = imageSource;

                    if (processEx is null)
                    {
                        ProfileIcon.Source = null;

                        ProfileToggle.IsEnabled = false;
                        ProcessName.Text = Properties.Resources.QuickProfilesPage_Waiting;
                        ProcessPath.Text = string.Empty;
                        SubProfilesBorder.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ProfileToggle.IsEnabled = true;
                        ProcessName.Text = currentProcess.Executable;
                        ProcessPath.Text = currentProcess.Path;
                        SubProfilesBorder.Visibility = Visibility.Visible;
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
        realProfile = ManagerFactory.profileManager.GetProfileFromPath(realProfile.Path, true);
        if (realProfile is null)
            return;

        if (realProfile.Default)
        {
            new Thread(CreateProfile).Start();
        }
        else
        {
            realProfile.Enabled = ProfileToggle.IsOn;
            ManagerFactory.profileManager.UpdateOrCreateProfile(realProfile, UpdateSource.QuickProfilesCreation);
        }
    }

    private void CreateProfile()
    {
        if (currentProcess is null)
            return;

        // create profile
        selectedProfile = new Profile(currentProcess.Path);

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

        // prevent update loop
        if (profileLock.IsEntered())
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

    private void AFMFToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.AFMF, AFMFToggle.IsOn);
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
        if (profileLock.IsEntered() || graphicLock.IsEntered() || multimediaLock.IsEntered())
            return;

        var divider = 1;
        if (IntegerScalingComboBox.SelectedItem is ScreenDivider screenDivider)
            divider = screenDivider.divider;

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
        int idx = ManagerFactory.powerProfileManager.profiles.Values.Where(p => !p.IsDefault()).Count() + 1;

        string Name = string.Format(Properties.Resources.PowerProfileManualName, idx);
        PowerProfile powerProfile = new PowerProfile(Name, Properties.Resources.PowerProfileManualDescription)
        {
            TDPOverrideValues = IDevice.GetCurrent().nTDP
        };

        ManagerFactory.powerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);

        // localize me
        new Dialog(OverlayQuickTools.GetCurrent())
        {
            Title = "Power preset",
            Content = $"{powerProfile.Name} preset was created",
            PrimaryButtonText = Properties.Resources.ProfilesPage_OK
        }.Show();
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

                    // AFMF
                    case UpdateGraphicsSettingsSource.AFMF:
                        {
                            selectedProfile.AFMFEnabled = isEnabled;
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