using HandheldCompanion.Actions;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Pages.Profiles;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static HandheldCompanion.GraphicsProcessingUnit.GPU;
using static HandheldCompanion.Utils.XInputPlusUtils;
using Page = System.Windows.Controls.Page;
using PowerLineStatus = System.Windows.Forms.PowerLineStatus;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Profiles.xaml
/// </summary>
public partial class ProfilesPage : Page
{
    // when set on start cannot be null anymore
    public static Profile selectedProfile;
    private static Profile selectedMainProfile;

    private readonly SettingsMode0 page0 = new("SettingsMode0");
    private readonly SettingsMode1 page1 = new("SettingsMode1");

    private CrossThreadLock profileLock = new();
    private CrossThreadLock graphicLock = new();

    private const int UpdateInterval = 500;
    private static Timer UpdateTimer;

    public ProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public ProfilesPage()
    {
        DataContext = new ProfilesPageViewModel(this);
        InitializeComponent();

        // manage events
        ManagerFactory.profileManager.Deleted += ProfileDeleted;
        ManagerFactory.profileManager.Updated += ProfileUpdated;
        ManagerFactory.profileManager.Applied += ProfileApplied;
        ManagerFactory.profileManager.Initialized += ProfileManagerLoaded;
        ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;
        ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        ManagerFactory.gpuManager.Hooked += GPUManager_Hooked;
        ManagerFactory.gpuManager.Unhooked += GPUManager_Unhooked;
        ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;

        // raise events
        switch (ManagerFactory.platformManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.platformManager.Initialized += PlatformManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPlatforms();
                break;
        }

        UpdateTimer = new Timer(UpdateInterval) { AutoReset = false };
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();

        // auto-sort
        cB_Profiles.Items.SortDescriptions.Add(new SortDescription(string.Empty, ListSortDirection.Descending));
    }

    private void QueryPlatforms()
    {
        // manage events
        PlatformManager.RTSS.Updated += RTSS_Updated;

        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    private void PlatformManager_Initialized()
    {
        QueryPlatforms();
    }

    private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        UIHelper.TryInvoke(() =>
        {
            SelectedPowerProfileName.Text = profile.Name;
        });
    }

    private void MultimediaManager_Initialized()
    {
        if (profileLock.TryEnter())
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
            catch { }
            finally
            {
                profileLock.Exit();
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
        });

        UpdateGraphicsSettingsUI();
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

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // GPU-specific settings
            StackProfileRSR.Visibility = Visibility.Collapsed;
            StackProfileAFMF.Visibility = Visibility.Collapsed;

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
                case PlatformStatus.Started:
                    StackProfileFramerate.IsEnabled = true;
                    break;
                default:
                    StackProfileFramerate.IsEnabled = false;
                    break;
            }
        });
    }

    private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        List<ScreenFramelimit> frameLimits = desktopScreen.GetFramelimits();

        if (profileLock.TryEnter())
        {
            try
            {
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
            catch { }
            finally
            {
                profileLock.Exit();
            }
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
        // manage events
        ManagerFactory.profileManager.Deleted -= ProfileDeleted;
        ManagerFactory.profileManager.Updated -= ProfileUpdated;
        ManagerFactory.profileManager.Applied -= ProfileApplied;
        ManagerFactory.profileManager.Initialized -= ProfileManagerLoaded;
        ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
        ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
        ManagerFactory.gpuManager.Hooked -= GPUManager_Hooked;
        ManagerFactory.gpuManager.Unhooked -= GPUManager_Unhooked;
        PlatformManager.RTSS.Updated -= RTSS_Updated;
        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;

        UpdateTimer.Elapsed -= (sender, e) => SubmitProfile();

        ((ProfilesPageViewModel)DataContext).Dispose();
    }

    private async void b_CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        // if an update is pending, execute it and stop timer
        if (UpdateTimer.Enabled)
            UpdateTimer.Stop();

        try
        {
            string path = string.Empty;
            string arguments = string.Empty;
            string name = string.Empty;

            FileUtils.CommonFileDialog(out path, out arguments, out name);

            // create profile
            Profile profile = new Profile(path);
            profile.Arguments = arguments;
            if (!string.IsNullOrEmpty(arguments))
                profile.Name = name;

            // check on path rather than profile
            bool exists = false;
            Profile parentProfile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
            if (parentProfile is not null && !parentProfile.Default)
            {
                Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                {
                    Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite1, parentProfile.Name),
                    Content = string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite2, parentProfile.Name),
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_Yes,
                    SecondaryButtonText = Properties.Resources.ProfilesPage_AreYouSureOverwriteSecondary,
                }.ShowAsync();

                await dialogTask; // sync call

                switch (dialogTask.Result)
                {
                    case ContentDialogResult.Primary:
                        exists = false;
                        break;
                    case ContentDialogResult.Secondary:
                        {
                            Profile mainProfile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
                            profile.IsSubProfile = true;
                            profile.ParentGuid = mainProfile.Guid;

                            exists = false;
                        }
                        break;
                    default:
                        exists = true;
                        break;
                }
            }

            if (!exists)
                ManagerFactory.profileManager.UpdateOrCreateProfile(profile, UpdateSource.Creation);
        }
        catch (Exception ex)
        {
            LogManager.LogError(ex.Message);
        }
    }

    private void b_AdditionalSettings_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        // TODO: MOVE ME TO LAYOUT !
        switch (((GyroActions)currentAction).MotionInput)
        {
            default:
                page0.SetProfile();
                MainWindow.NavView_Navigate(page0);
                break;
            case MotionInput.JoystickSteering:
                page1.SetProfile();
                MainWindow.NavView_Navigate(page1);
                break;
        }
    }

    private void PowerProfileOnBatteryMore_Click(object sender, RoutedEventArgs e)
    {
        // If the click originated in the ComboBox (or any ComboBoxItem), ignore it
        DependencyObject? src = e.Source as DependencyObject;
        if (src is not SettingsCard)
            return;

        PowerProfile powerProfile = ManagerFactory.powerProfileManager.GetProfile(selectedProfile.PowerProfiles[(int)PowerLineStatus.Offline]);
        if (powerProfile is null)
            return;

        MainWindow.performancePage.SelectionChanged(powerProfile);
        MainWindow.GetCurrent().NavigateToPage("PerformancePage");
    }

    private void PowerProfilePluggedMore_Click(object sender, RoutedEventArgs e)
    {
        // If the click originated in the ComboBox (or any ComboBoxItem), ignore it
        DependencyObject? src = e.Source as DependencyObject;
        if (src is not SettingsCard)
            return;

        PowerProfile powerProfile = ManagerFactory.powerProfileManager.GetProfile(selectedProfile.PowerProfiles[(int)PowerLineStatus.Online]);
        if (powerProfile is null)
            return;

        MainWindow.performancePage.SelectionChanged(powerProfile);
        MainWindow.GetCurrent().NavigateToPage("PerformancePage");
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
                    break;
                case true:
                    selectedProfile.PowerProfiles[(int)PowerLineStatus.Online] = powerProfile.Guid;
                    break;
            }
        });
        UpdateProfile();
    }

    private void cB_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Profiles.SelectedItem is null)
            return;

        selectedMainProfile = (Profile)cB_Profiles.SelectedItem;

        UpdateSubProfiles();
    }

    private void UpdateMotionControlsVisibility()
    {
        bool MotionMapped = false;
        if (selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions action))
            if (action is not null && action.actionType != ActionType.Disabled)
                MotionMapped = true;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            MotionControlAdditional.IsEnabled = MotionMapped;
        });
    }

    private void UpdateUI()
    {
        if (selectedProfile is null)
            return;

        if (profileLock.TryEnter())
        {
            ((ProfilesPageViewModel)DataContext).ProfileChanged(selectedProfile);

            try
            {
                UIHelper.TryInvoke(() =>
                {
                    // disable delete button if is default profile or any sub profile is running
                    //TODO consider sub profiles pertaining to this main profile is running
                    b_DeleteProfile.IsEnabled = (selectedProfile.ErrorCode & (ProfileErrorCode.Default | ProfileErrorCode.Running)) == 0;
                    // prevent user from renaming default profile
                    b_ProfileRename.IsEnabled = !selectedMainProfile.Default;
                    // prevent user from disabling default profile
                    Toggle_EnableProfile.IsEnabled = !selectedProfile.Default;
                    // prevent user from using Wrapper on default profile
                    cB_Wrapper.IsEnabled = !selectedProfile.Default;
                    UseFullscreenOptimizations.IsEnabled = !selectedProfile.Default;
                    UseHighDPIAwareness.IsEnabled = !selectedProfile.Default;
                    LibrarySettings.IsEnabled = !selectedProfile.Default;
                    Toggle_SuspendOnQuicktools.IsEnabled = !selectedProfile.Default;

                    // sub profiles
                    b_SubProfileCreate.IsEnabled = !selectedMainProfile.Default;

                    // enable delete and rename if not default sub profile
                    b_SubProfileDelete.IsEnabled = selectedProfile.IsSubProfile ? true : false;
                    b_SubProfileRename.IsEnabled = selectedProfile.IsSubProfile ? true : false;

                    // Profile info
                    tB_ProfileName.Text = selectedMainProfile.Name;
                    tB_ProfilePath.Text = selectedProfile.Path;
                    tB_ProfileArguments.Text = selectedProfile.Arguments;
                    tB_ProfileLaunchString.Text = selectedProfile.LaunchString;
                    Toggle_EnableProfile.IsOn = selectedProfile.Enabled;

                    // Global settings
                    cB_Whitelist.IsChecked = selectedProfile.Whitelisted;
                    b_ProfileLike.IsChecked = selectedProfile.IsLiked;
                    cB_Suspend.IsChecked = selectedProfile.SuspendOnSleep;
                    cB_Wrapper.SelectedIndex = (int)selectedProfile.XInputPlus;

                    // Library
                    Toggle_ShowInLibrary.IsOn = selectedProfile.ShowInLibrary;

                    // Window(s)
                    Toggle_SuspendOnQuicktools.IsOn = selectedProfile.SuspendOnQT;

                    // Emulated controller assigned to the profile
                    cB_EmulatedController.IsEnabled = !selectedProfile.Default; // if default profile, disable combobox
                    cB_EmulatedController.SelectedIndex = new Func<int>(() =>
                    {
                        if (selectedProfile.Default) // Default profile always shows default, but keeps track of default controller internally
                            return 0;
                        else if (selectedProfile.HID == HIDmode.Xbox360Controller)
                            return 1;
                        else if (selectedProfile.HID == HIDmode.DualShock4Controller)
                            return 2;
                        else
                            return 0; // Current or not assigned
                    })();

                    // Motion control settings
                    tb_ProfileGyroValue.Value = selectedProfile.GyrometerMultiplier;
                    tb_ProfileAcceleroValue.Value = selectedProfile.AccelerometerMultiplier;

                    cB_GyroSteering.SelectedIndex = (byte)selectedProfile.SteeringAxis;
                    cB_InvertHorizontal.IsChecked = selectedProfile.MotionInvertHorizontal;
                    cB_InvertVertical.IsChecked = selectedProfile.MotionInvertVertical;

                    UpdateMotionControlsVisibility();

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

                    if (desktopScreen is not null && selectedProfile.IntegerScalingEnabled)
                        IntegerScalingComboBox.SelectedItem = desktopScreen.screenDividers.FirstOrDefault(d => d.divider == selectedProfile.IntegerScalingDivider);

                    // RIS
                    RISToggle.IsOn = selectedProfile.RISEnabled;
                    RISSlider.Value = selectedProfile.RISSharpness;

                    // Compatibility settings
                    UseFullscreenOptimizations.IsOn = selectedProfile.FullScreenOptimization;
                    UseHighDPIAwareness.IsOn = selectedProfile.HighDPIAware;

                    // power profile
                    PowerProfile powerProfileDC = ManagerFactory.powerProfileManager.GetProfile(selectedProfile.PowerProfiles[(int)PowerLineStatus.Offline]);
                    PowerProfile powerProfileAC = ManagerFactory.powerProfileManager.GetProfile(selectedProfile.PowerProfiles[(int)PowerLineStatus.Online]);

                    switch (System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus)
                    {
                        case System.Windows.Forms.PowerLineStatus.Unknown:
                        case System.Windows.Forms.PowerLineStatus.Offline:
                            SelectedPowerProfileName.Text = powerProfileDC?.Name;
                            break;
                        case System.Windows.Forms.PowerLineStatus.Online:
                            SelectedPowerProfileName.Text = powerProfileAC?.Name;
                            break;
                    }

                    ((ProfilesPageViewModel)DataContext).PowerProfileChanged(powerProfileAC, powerProfileDC);

                    // display warnings
                    WarningInfoBar.Message = EnumUtils.GetDescriptionFromEnumValue(selectedProfile.ErrorCode);

                    (Visibility warningVisibility, bool controlsEnabled, bool redirectionEnabled) = selectedProfile.ErrorCode switch
                    {
                        ProfileErrorCode.MissingPermission => (Visibility.Visible, true, false),
                        ProfileErrorCode.Running or
                        ProfileErrorCode.MissingExecutable or
                        ProfileErrorCode.MissingPath or
                        ProfileErrorCode.Default => (Visibility.Visible, false, false),
                        _ => (Visibility.Collapsed, true, true)
                    };

                    WarningInfoBar.Visibility = warningVisibility;
                    GlobalSettings.IsEnabled = controlsEnabled;
                    cB_Wrapper_Injection.IsEnabled = controlsEnabled;
                    b_Play.IsEnabled = controlsEnabled;
                    cB_Wrapper_Redirection.IsEnabled = redirectionEnabled;

                    // update dropdown lists
                    cB_Profiles.Items.Refresh();
                    cb_SubProfilePicker.Items.Refresh();
                });
            }
            catch { }
            finally
            {
                profileLock.Exit();
            }
        }
    }

    private void UpdateSubProfiles(Profile updatedProfile = null)
    {
        if (selectedMainProfile is null)
            return;

        if (profileLock.TryEnter())
        {
            try
            {
                cb_SubProfilePicker.Items.Clear();

                IEnumerable<Profile> profiles = ManagerFactory.profileManager.GetSubProfilesFromProfile(selectedMainProfile, true);
                foreach (Profile profile in profiles)
                    cb_SubProfilePicker.Items.Add(profile);

                // Only need to refresh if we loaded real sub‑profiles
                if (!selectedMainProfile.Default)
                    cb_SubProfilePicker.Items.Refresh();

                // Decide which index to select
                int selectedIndex;
                if (updatedProfile != null && cb_SubProfilePicker.Items.Contains(updatedProfile))
                    selectedIndex = cb_SubProfilePicker.Items.IndexOf(updatedProfile);
                else
                    selectedIndex = profiles.Select((p, i) => new { p, i }).FirstOrDefault(x => x.p.IsFavoriteSubProfile)?.i ?? 0;

                cb_SubProfilePicker.SelectedIndex = selectedIndex;
            }
            catch { }
            finally
            {
                profileLock.Exit();
            }
        }

        // update UI elements
        UpdateUI();
    }

    private async void b_DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureDelete1, selectedMainProfile.Name),
            Content = Properties.Resources.ProfilesPage_AreYouSureDelete2,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Delete
        }.ShowAsync();

        await dialogTask; // sync call

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                ManagerFactory.profileManager.DeleteProfile(selectedMainProfile);
                cB_Profiles.SelectedIndex = 0;
                break;
        }
    }

    private void cB_Whitelist_Checked(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.Whitelisted = (bool)cB_Whitelist.IsChecked;
        UpdateProfile();
    }

    private void cB_Suspend_Checked(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.SuspendOnSleep = (bool)cB_Suspend.IsChecked;
        UpdateProfile();
    }

    private void b_ProfileLike_Click(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.IsLiked = (bool)b_ProfileLike.IsChecked;
        UpdateProfile();
    }

    private void cB_Wrapper_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (cB_Wrapper.SelectedIndex == -1)
            return;

        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.XInputPlus = (XInputPlusMethod)cB_Wrapper.SelectedIndex;

        switch (selectedProfile.XInputPlus)
        {
            case XInputPlusMethod.Injection:
                cB_Whitelist.IsChecked = true;
                break;
            case XInputPlusMethod.Redirection:
                cB_Whitelist.IsChecked = false;
                break;
        }

        UpdateProfile();
    }

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        ((Expander)sender).BringIntoView();
    }

    private void Toggle_EnableProfile_Toggled(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.Enabled = Toggle_EnableProfile.IsOn;
        UpdateProfile();
    }

    private LayoutTemplate selectedTemplate;
    public void ControllerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate is not null)
        {
            selectedTemplate.Updated -= Template_Updated;
            selectedTemplate = null;
        }

        // prepare layout editor
        selectedTemplate = new(selectedProfile.Layout)
        {
            Name = selectedProfile.LayoutTitle,
            Description = Properties.Resources.ProfilesPage_Layout_Desc,
            Author = Environment.UserName,
            Executable = selectedProfile.Executable,
            Product = selectedProfile.Name,
        };
        selectedTemplate.Updated += Template_Updated;

        MainWindow.layoutPage.UpdateLayoutTemplate(selectedTemplate);
        MainWindow.NavView_Navigate(MainWindow.layoutPage);
    }

    private void Template_Updated(LayoutTemplate layoutTemplate)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            selectedProfile.LayoutTitle = layoutTemplate.Name;
        });

        selectedProfile.Layout.ButtonLayout = layoutTemplate.Layout.ButtonLayout;
        selectedProfile.Layout.AxisLayout = layoutTemplate.Layout.AxisLayout;
        selectedProfile.Layout.GyroLayout = layoutTemplate.Layout.GyroLayout;
        UpdateMotionControlsVisibility();

        UpdateProfile();
    }

    #region UI

    private void ProfileApplied(Profile profile, UpdateSource source)
    {
        if (profile.Default)
            return;

        ProfileUpdated(profile, source, true);
    }

    public void ProfileUpdated(Profile profile, UpdateSource source, bool isCurrent)
    {
        isCurrent = selectedProfile?.Guid == profile?.Guid;
        isCurrent |= source.HasFlag(UpdateSource.Creation);

        switch (source)
        {
            case UpdateSource.QuickProfilesPage:
                if (!isCurrent) return;
                break;
        }

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            var idx = -1;
            if (!profile.IsSubProfile)
            {
                foreach (Profile pr in cB_Profiles.Items)
                {
                    bool isCurrent = pr.Guid == profile.Guid;
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

                if (isCurrent)
                    cB_Profiles.SelectedItem = profile;
            }
            else if (isCurrent)
            {
                cB_Profiles.SelectedItem = selectedMainProfile;
            }

            UpdateSubProfiles(profile);
        });
    }

    public void ProfileDeleted(Profile profile)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            int prevIdx = cB_Profiles.SelectedIndex;

            if (!profile.IsSubProfile)
            {
                // Profiles
                var idx = -1;
                foreach (Profile pr in cB_Profiles.Items)
                {
                    var isCurrent = pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase);
                    if (isCurrent)
                    {
                        idx = cB_Profiles.Items.IndexOf(pr);
                        break;
                    }
                }

                if (idx == -1)
                    return;

                cB_Profiles.Items.RemoveAt(idx);

                if (prevIdx == idx)
                    cB_Profiles.SelectedIndex = 0;
            }
            else
            {
                var idx = -1;
                foreach (Profile pr in cb_SubProfilePicker.Items)
                {
                    // sub profiles
                    var isCurrent = profile.Guid == pr.Guid;
                    if (isCurrent)
                    {
                        idx = cb_SubProfilePicker.Items.IndexOf(pr);
                        break;
                    }
                }

                if (idx == -1)
                    return;

                cb_SubProfilePicker.Items.RemoveAt(idx);
                cb_SubProfilePicker.SelectedIndex = 0;
            }
        });
    }

    private void ProfileManagerLoaded()
    {
        // UI thread
        UIHelper.TryInvoke(() => { cB_Profiles.SelectedItem = ManagerFactory.profileManager.GetDefault(); });
    }

    #endregion

    private void tb_ProfileGyroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileGyroValue.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.GyrometerMultiplier = (float)tb_ProfileGyroValue.Value;
        UpdateProfile();
    }

    private void tb_ProfileAcceleroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileAcceleroValue.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.AccelerometerMultiplier = (float)tb_ProfileAcceleroValue.Value;
        UpdateProfile();
    }

    private void cB_GyroSteering_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_GyroSteering.SelectedIndex == -1)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.SteeringAxis = (SteeringAxis)cB_GyroSteering.SelectedIndex;
        UpdateProfile();
    }

    private void cB_InvertHorizontal_Checked(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.MotionInvertHorizontal = (bool)cB_InvertHorizontal.IsChecked;
        UpdateProfile();
    }

    private void cB_InvertVertical_Checked(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.MotionInvertVertical = (bool)cB_InvertVertical.IsChecked;
        UpdateProfile();
    }

    public static void UpdateProfile()
    {
        if (UpdateTimer is not null)
        {
            UpdateTimer.Stop();
            UpdateTimer.Start();
        }
    }

    public void SubmitProfile(UpdateSource source = UpdateSource.ProfilesPage)
    {
        if (selectedProfile is null)
            return;

        switch (source)
        {
            case UpdateSource.ProfilesPageUpdateOnly: // when renaming main profile, update main profile only but don't apply it
                ManagerFactory.profileManager.UpdateOrCreateProfile(selectedMainProfile, source);
                break;
            default:
                ManagerFactory.profileManager.UpdateOrCreateProfile(selectedProfile, source);
                break;
        }
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

    private void IntegerScalingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IntegerScalingComboBox.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        var divider = 1;
        if (IntegerScalingComboBox.SelectedItem is ScreenDivider screenDivider)
            divider = screenDivider.divider;

        selectedProfile.IntegerScalingDivider = divider;
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

    private void cB_EmulatedController_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        ComboBoxItem selectedEmulatedController = (ComboBoxItem)cB_EmulatedController.SelectedItem;

        if (selectedEmulatedController is null || selectedEmulatedController.Tag is null)
            return;

        HIDmode HIDmode = (HIDmode)int.Parse(selectedEmulatedController.Tag.ToString());
        selectedProfile.HID = HIDmode;

        UpdateProfile();
    }

    private void cb_SubProfilePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cb_SubProfilePicker.SelectedIndex == -1)
            return;

        // update selected profile
        selectedProfile = (Profile)cb_SubProfilePicker.SelectedItem;
        UpdateUI();

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        // selected sub profile
        if (selectedProfile != cb_SubProfilePicker.SelectedItem)
            UpdateProfile();
    }

    private void b_SubProfileCreate_Click(object sender, RoutedEventArgs e)
    {
        // create a new sub profile matching the original profile's settings
        Profile newSubProfile = (Profile)selectedMainProfile.Clone();

        newSubProfile.Name = Properties.Resources.ProfilesPage_NewSubProfile;
        newSubProfile.Guid = Guid.NewGuid();
        newSubProfile.IsSubProfile = true;
        newSubProfile.ParentGuid = selectedMainProfile.Guid;

        ManagerFactory.profileManager.UpdateOrCreateProfile(newSubProfile);
    }

    private async void b_SubProfileDelete_Click(object sender, RoutedEventArgs e)
    {
        // return if original profile or nothing is selected
        if (cb_SubProfilePicker.SelectedIndex <= 0)
            return;

        // get selected subprofile from dropdown
        Profile subProfile = (Profile)cb_SubProfilePicker.SelectedItem;

        // user confirmation
        Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureDelete1, subProfile.Name),
            Content = Properties.Resources.ProfilesPage_AreYouSureDelete2,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Delete
        }.ShowAsync();

        await dialogTask; // sync call

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                ManagerFactory.profileManager.DeleteProfile(subProfile);
                break;
        }
    }

    private void b_SubProfileRename_Click(object sender, RoutedEventArgs e)
    {
        tb_SubProfileName.Text = selectedProfile.Name;
        SubProfileRenameDialog.ShowAsync();
    }

    private void SubProfileRenameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (selectedProfile is null)
            return;

        // update subprofile name
        selectedProfile.Name = tb_SubProfileName.Text;

        // serialize subprofile
        SubmitProfile();
    }

    private void SubProfileRenameDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // restore subprofile rename dialog
        tb_SubProfileName.Text = selectedProfile.Name;
    }

    private void ProfileRenameDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // restore profile name textbox
        tB_ProfileName.Text = selectedMainProfile.Name;
    }

    private void ProfileRenameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // change main profile name
        selectedMainProfile.Name = tB_ProfileName.Text;
        SubmitProfile(UpdateSource.ProfilesPageUpdateOnly);
    }

    private void b_ProfileRename_Click(object sender, RoutedEventArgs e)
    {
        tB_ProfileName.Text = selectedMainProfile.Name;
        ProfileRenameDialog.ShowAsync();
    }

    private void cB_Framerate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Framerate.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        if (cB_Framerate.SelectedItem is ScreenFramelimit screenFramelimit && screenFramelimit.limit != selectedProfile.FramerateValue)
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
                                selectedProfile.GPUScaling = true;
                                selectedProfile.RISEnabled = false;
                                selectedProfile.IntegerScalingEnabled = false;

                                GPUScalingToggle.IsOn = true;
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
            catch { }
            finally
            {
                graphicLock.Exit();
            }
        }
    }

    private void UseFullscreenOptimizations_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.FullScreenOptimization = UseFullscreenOptimizations.IsOn;
        UpdateProfile();
    }

    private void UseHighDPIAwareness_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.HighDPIAware = UseHighDPIAwareness.IsOn;
        UpdateProfile();
    }

    private void tB_ProfileArguments_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.Arguments = tB_ProfileArguments.Text;
        UpdateProfile();
    }

    // Toggle to show or hide profile in library or quick start
    private void ShowInLibrary_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.ShowInLibrary = Toggle_ShowInLibrary.IsOn;
        UpdateProfile();
    }

    private void Toggle_SuspendOnQuicktools_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.SuspendOnQT = Toggle_SuspendOnQuicktools.IsOn;
        UpdateProfile();
    }

    private void tB_ProfileLaunchString_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.LaunchString = tB_ProfileLaunchString.Text;
        UpdateProfile();
    }
}