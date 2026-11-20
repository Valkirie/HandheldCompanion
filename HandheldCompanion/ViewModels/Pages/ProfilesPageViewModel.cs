using HandheldCompanion.Actions;
using HandheldCompanion.Devices;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Libraries;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views.Pages;
using HandheldCompanion.Views.QuickPages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Libraries.LibraryEntry;
using static HandheldCompanion.Managers.LibraryManager;
using static HandheldCompanion.Misc.ProcessEx;
using static HandheldCompanion.Utils.XInputPlusUtils;
using PowerLineStatus = System.Windows.Forms.PowerLineStatus;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.ViewModels
{
    /// <summary>
    /// Unified ViewModel for both ProfilesPage and QuickProfilesPage.
    /// 
    /// CRITICAL ARCHITECTURE NOTES:
    /// 1. isLoadingProfile flag prevents circular updates when syncing Profile -> UI
    /// 2. WPF bindings will set SelectedProfile=null when SubProfiles.Clear() is called
    ///    - Always set SelectedProfile BEFORE clearing/modifying SubProfiles
    /// 3. SelectedProfile setter calls OnProfileChanged() which calls UpdateCurrentProcessViewModel()
    ///    - Only use the property setter when you want these side effects
    ///    - Use _selectedProfile backing field when updating from ProfileUpdated events
    /// 4. Order matters in HandleProfileApplied: SelectedMainProfile -> SubProfiles -> SelectedProfile
    /// </summary>
    public class ProfilesPageViewModel : BaseViewModel
    {
        private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_ACTIVATION_QP;
        private const int UpdateInterval = 500;

        // Main profiles collection for cB_Profiles ComboBox
        public ObservableCollection<ProfileViewModel> MainProfiles { get; } = [];

        // Sub-profiles collection for cb_SubProfilePicker ComboBox
        public ObservableCollection<ProfileViewModel> SubProfiles { get; } = [];

        private ObservableCollection<ProfilesPickerViewModel> ProfilePicker = [];
        public ListCollectionView ProfilePickerCollectionViewAC { get; set; } = null!;
        public ListCollectionView ProfilePickerCollectionViewDC { get; set; } = null!;
        public ListCollectionView MainProfilesView { get; private set; } = null!;
        public ListCollectionView SubProfilesView { get; private set; } = null!;

        public ObservableCollection<LibraryEntryViewModel> LibraryPickers { get; } = [];
        public ObservableCollection<WindowListItemViewModel> AllWindows { get; } = [];
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        // ComboBox collections
        public ObservableCollection<ScreenDividerViewModel> IntegerScalingDividers { get; } = [];

        // Motion output and input modes for ComboBox data binding
        public ObservableCollection<MotionOutputViewModel> MotionOutputModes { get; } = [];
        public ObservableCollection<MotionInputViewModel> MotionInputModes { get; } = [];

        public bool HasAnyWindows => AllWindows.Any();

        // True if library search results are available in LibraryPickers (for enabling ComboBox and showing preview)
        public bool HasLibraryEntry => LibraryPickers.Any();

        // True if library manager has network connectivity (for enabling library features)
        public bool IsLibraryConnected => ManagerFactory.libraryManager.IsConnected;

        // True if library manager is busy downloading/searching (for showing ProgressRing in dialog)
        public bool IsLibraryBusy => ManagerFactory.libraryManager.Status.HasFlag(ManagerStatus.Busy);

        // True if the Apply button in the library dialog should be enabled (results available and not currently loading)
        public bool CanApplyLibrary => HasLibraryEntry && !IsLibraryBusy;

        public bool HasProfileExecutables => SelectedProfile?.Executables.Any() ?? false;

        // True if a profile is selected (not null) - used to enable/disable the entire page
        public bool HasSelectedProfile => SelectedProfile != null;

        // True if the selected profile can be renamed/deleted (not Default)
        public bool IsProfileManagementEnabled => SelectedMainProfile != null && !SelectedMainProfile.Default;

        // True if the library browse button should be enabled (connected and not Default profile)
        public bool IsLibrarySettingsEnabled => IsLibraryConnected && IsProfileManagementEnabled;

        // True if the ProfileEnabled toggle can be modified (not Default profile)
        public bool IsProfileEnabledToggleEnabled => SelectedProfile != null && !SelectedProfile.Default;

        public bool IsControllerPassthroughEnabled => SelectedProfile != null && !SelectedProfile.Default;

        private readonly bool IsQuickTools;
        private ProfilesPage profilesPage = null!;
        private QuickProfilesPage quickProfilesPage = null!;
        private Timer UpdateTimer = null!;
        private ProcessEx? currentProcess;
        private ProcessEx? selectedProcess;
        private Hotkey GyroHotkey = null!;
        private LayoutTemplate? selectedTemplate;

        #region Profile
        private Profile _selectedProfile = null!;
        private Profile? _libraryTargetProfile;

        /// <summary>
        /// CRITICAL: Setting this property triggers OnProfileChanged() which:
        /// - Calls UpdateCurrentProcessViewModel()
        /// - Updates AllWindows collection
        /// - Calls UpdateUI()
        /// 
        /// Only set this when you want these cascading updates.
        /// Use _selectedProfile backing field when updating from external events (ProfileUpdated).
        /// </summary>
        public Profile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                using (new LoadingScope(this))
                    SetSelectedProfile(value, true);
            }
        }

        private Profile _selectedMainProfile = null!;
        public Profile SelectedMainProfile
        {
            get => _selectedMainProfile;
            set
            {
                SetSelectedMainProfile(value, true);
            }
        }

        public ProfileViewModel? SelectedMainProfileViewModel
        {
            get => MainProfiles.FirstOrDefault(vm => vm.Profile.Guid == _selectedMainProfile?.Guid);
            set
            {
                if (value is not null && value.Profile != _selectedMainProfile)
                    SelectedMainProfile = value.Profile;
            }
        }

        public ObservableCollection<string> ProfileExecutables { get; } = new();

        private int _ProfileExecutablesIdx;
        public int ProfileExecutablesIdx
        {
            get => _ProfileExecutablesIdx;
            set
            {
                if (value != _ProfileExecutablesIdx)
                {
                    _ProfileExecutablesIdx = value;
                    OnPropertyChanged(nameof(ProfileExecutablesIdx));
                }
            }
        }

        // Graphics Properties
        private bool _IsRTSSReady;
        public bool IsRTSSReady
        {
            get => _IsRTSSReady;
            set { if (value != _IsRTSSReady) { _IsRTSSReady = value; OnPropertyChanged(nameof(IsRTSSReady)); } }
        }

        private bool _IsAMDGPU;
        public bool IsAMDGPU
        {
            get => _IsAMDGPU;
            set { if (value != _IsAMDGPU) { _IsAMDGPU = value; OnPropertyChanged(nameof(IsAMDGPU)); RefreshGraphicsOverrideRows(); } }
        }

        private bool _HasRSRSupport;
        public bool HasRSRSupport
        {
            get => _HasRSRSupport;
            set { if (value != _HasRSRSupport) { _HasRSRSupport = value; OnPropertyChanged(nameof(HasRSRSupport)); RefreshGraphicsOverrideRows(); } }
        }

        private bool _HasAFMFSupport;
        public bool HasAFMFSupport
        {
            get => _HasAFMFSupport;
            set { if (value != _HasAFMFSupport) { _HasAFMFSupport = value; OnPropertyChanged(nameof(HasAFMFSupport)); RefreshGraphicsOverrideRows(); } }
        }

        private bool _HasAFMF21Support;
        public bool HasAFMF21Support
        {
            get => _HasAFMF21Support;
            set { if (value != _HasAFMF21Support) { _HasAFMF21Support = value; OnPropertyChanged(nameof(HasAFMF21Support)); RefreshGraphicsOverrideRows(); } }
        }

        private bool _HasScalingModeSupport;
        public bool HasScalingModeSupport
        {
            get => _HasScalingModeSupport;
            set { if (value != _HasScalingModeSupport) { _HasScalingModeSupport = value; OnPropertyChanged(nameof(HasScalingModeSupport)); } }
        }

        private bool _HasIntegerScalingSupport;
        public bool HasIntegerScalingSupport
        {
            get => _HasIntegerScalingSupport;
            set { if (value != _HasIntegerScalingSupport) { _HasIntegerScalingSupport = value; OnPropertyChanged(nameof(HasIntegerScalingSupport)); RefreshGraphicsOverrideRows(); } }
        }

        private bool _HasGPUScalingSupport;
        public bool HasGPUScalingSupport
        {
            get => _HasGPUScalingSupport;
            set { if (value != _HasGPUScalingSupport) { _HasGPUScalingSupport = value; OnPropertyChanged(nameof(HasGPUScalingSupport)); RefreshGraphicsOverrideRows(); } }
        }

        public bool GPUManagementEnabled => ManagerFactory.settingsManager.GetBoolean("GPUManagementEnabled");
        public bool PerformanceManagerEnabled => ManagerFactory.settingsManager.GetBoolean("PerformanceManagerEnabled");

        // power slots hold performance and graphics settings
        public bool ShowPowerSlots => PerformanceManagerEnabled || GPUManagementEnabled;
        public bool HasPrebuiltShaderDownload => SelectedProfile is not null && !string.IsNullOrEmpty(SelectedProfile.Executable) && GPUManager.GetCurrent() is GPU gpu && gpu.HasPrebuiltShaderDownload(out bool perAppSupported) && perAppSupported;

        public bool PrebuiltShaderDownloadEnabled
        {
            get
            {
                if (SelectedProfile is null || string.IsNullOrEmpty(SelectedProfile.Executable) || GPUManager.GetCurrent() is not GPU gpu)
                    return false;

                return gpu.GetPrebuiltShaderDownload(SelectedProfile.Executable, out bool enabled) ? enabled : SelectedProfile.PrebuiltShaderDownloadEnabled;
            }
            set
            {
                if (SelectedProfile is null || string.IsNullOrEmpty(SelectedProfile.Executable) || GPUManager.GetCurrent() is not GPU gpu)
                    return;

                if (gpu.SetPrebuiltShaderDownload(SelectedProfile.Executable, value))
                {
                    if (SelectedProfile.PrebuiltShaderDownloadEnabled != value)
                    {
                        SelectedProfile.PrebuiltShaderDownloadEnabled = value;
                        UpdateProfile();
                    }

                    OnPropertyChanged(nameof(PrebuiltShaderDownloadEnabled));
                }
            }
        }

        private double _GyroMultiplier = 1.0f;
        public double GyroMultiplier
        {
            get => _GyroMultiplier;
            set
            {
                if (value != _GyroMultiplier)
                {
                    _GyroMultiplier = value;
                    OnPropertyChanged(nameof(GyroMultiplier));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null)
                    {
                        SelectedProfile.GyrometerMultiplier = (float)value;
                        UpdateProfile();
                    }
                }
            }
        }

        private double _AcceleroMultiplier = 1.0f;
        public double AcceleroMultiplier
        {
            get => _AcceleroMultiplier;
            set
            {
                if (value != _AcceleroMultiplier)
                {
                    _AcceleroMultiplier = value;
                    OnPropertyChanged(nameof(AcceleroMultiplier));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null)
                    {
                        SelectedProfile.AccelerometerMultiplier = (float)value;
                        UpdateProfile();
                    }
                }
            }
        }

        // Quick Page Binding Properties
        private bool _ProfileEnabled;
        public bool ProfileEnabled
        {
            get => _ProfileEnabled;
            set
            {
                if (value != _ProfileEnabled)
                {
                    _ProfileEnabled = value;
                    OnPropertyChanged(nameof(ProfileEnabled));

                    if (isLoadingProfile || SelectedProfile is null)
                        return;

                    if (IsQuickTools)
                    {
                        if (value)
                        {
                            // Toggle ON: only act when Default is currently applied (no active per-game profile)
                            if (SelectedProfile.Default && currentProcess is not null)
                            {
                                // Check if a disabled profile already exists for this process
                                Profile existingProfile = ManagerFactory.profileManager.GetProfileFromPath(currentProcess.Path, true, true);
                                if (!existingProfile.Default)
                                {
                                    // Enable the existing disabled profile and force-apply it immediately
                                    existingProfile.Enabled = true;
                                    ManagerFactory.profileManager.UpdateOrCreateProfile(existingProfile, UpdateSource.QuickProfilesEnable);
                                }
                                else
                                {
                                    // No profile exists - create one for the foreground process
                                    Profile newProfile = new Profile(currentProcess.Path);
                                    ManagerFactory.profileManager.UpdateOrCreateProfile(newProfile, UpdateSource.QuickProfilesCreation);
                                }
                            }
                        }
                        else
                        {
                            // Toggle OFF: disable the active per-game profile so Default is used
                            if (!SelectedProfile.Default && SelectedProfile.Enabled)
                            {
                                SelectedProfile.Enabled = false;
                                UpdateProfile();
                            }
                        }
                    }
                    else
                    {
                        // Regular profiles page: just set the enabled state
                        if (SelectedProfile.Enabled != value)
                        {
                            SelectedProfile.Enabled = value;
                            UpdateProfile();
                        }
                    }
                }
            }
        }

        private bool _IsProcessCardEnabled;
        public bool IsProcessCardEnabled
        {
            get => _IsProcessCardEnabled;
            set { if (value != _IsProcessCardEnabled) { _IsProcessCardEnabled = value; OnPropertyChanged(nameof(IsProcessCardEnabled)); } }
        }

        private string _ProcessName = Properties.Resources.QuickProfilesPage_Waiting;
        public string ProcessName
        {
            get => _ProcessName;
            set { if (value != _ProcessName) { _ProcessName = value; OnPropertyChanged(nameof(ProcessName)); } }
        }

        private string _ProcessPath = string.Empty;
        public string ProcessPath
        {
            get => _ProcessPath;
            set { if (value != _ProcessPath) { _ProcessPath = value; OnPropertyChanged(nameof(ProcessPath)); } }
        }

        private bool _IsProcessPathVisible;
        public bool IsProcessPathVisible
        {
            get => _IsProcessPathVisible;
            set { if (value != _IsProcessPathVisible) { _IsProcessPathVisible = value; OnPropertyChanged(nameof(IsProcessPathVisible)); } }
        }

        private bool _IsSubProfilesVisible;
        public bool IsSubProfilesVisible
        {
            get => _IsSubProfilesVisible;
            set { if (value != _IsSubProfilesVisible) { _IsSubProfilesVisible = value; OnPropertyChanged(nameof(IsSubProfilesVisible)); } }
        }

        private System.Windows.Media.ImageSource? _ProcessIcon;
        public System.Windows.Media.ImageSource? ProcessIcon
        {
            get => _ProcessIcon;
            set { if (value != _ProcessIcon) { _ProcessIcon = value; OnPropertyChanged(nameof(ProcessIcon)); } }
        }

        private int _SelectedSubProfileIndex = -1;
        /// <summary>
        /// Index of the selected sub-profile in the SubProfiles collection.
        /// 
        /// CRITICAL: When user changes this (e.g., via ComboBox), we APPLY the profile (not just select it).
        /// This triggers the full profile application flow including:
        /// - Setting as favorite
        /// - Discarding previous profile
        /// - Raising Applied event
        /// </summary>
        public int SelectedSubProfileIndex
        {
            get => _SelectedSubProfileIndex;
            set
            {
                if (value != _SelectedSubProfileIndex)
                {
                    _SelectedSubProfileIndex = value;
                    OnPropertyChanged(nameof(SelectedSubProfileIndex));

                    if (isLoadingProfile)
                        return;

                    if (value >= 0 && value < SubProfiles.Count)
                    {
                        Profile newSelectedProfile = SubProfiles[value].Profile;
                        if (newSelectedProfile != _selectedProfile)
                            SelectedProfile = newSelectedProfile;
                    }
                }
            }
        }

        /// <summary>
        /// The currently selected sub-profile ViewModel, used for ComboBox SelectedItem binding.
        /// Works correctly with sorted views - no index mapping required.
        /// Setting this (by user ComboBox interaction) triggers profile application.
        /// </summary>
        public ProfileViewModel? SelectedSubProfileViewModel
        {
            get => SubProfiles.FirstOrDefault(vm => vm.Profile.Guid == _selectedProfile?.Guid);
            set
            {
                if (!isLoadingProfile && value != null && value.Profile != _selectedProfile)
                    SelectedProfile = value.Profile;
            }
        }

        private int _OutputMode;
        public int OutputMode
        {
            get => _OutputMode;
            set
            {
                if (value != _OutputMode)
                {
                    _OutputMode = value;
                    OnPropertyChanged(nameof(OutputMode));

                    // Only write back to profile if we're not loading from it
                    if (isLoadingProfile || SelectedProfile == null)
                        return;

                    SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? gyroActions);

                    // Preserve existing MotionInput and MotionMode if switching between output types
                    MotionInput preservedMotionInput = MotionInput.LocalSpace;
                    MotionMode preservedMotionMode = Utils.MotionMode.Off;
                    float preservedGyroWeight = GyroActions.DefaultGyroWeight;
                    if (gyroActions is GyroActions existingGyroAction)
                    {
                        preservedMotionInput = existingGyroAction.MotionInput;
                        preservedMotionMode = existingGyroAction.MotionMode;
                        preservedGyroWeight = existingGyroAction.gyroWeight;
                    }

                    MotionOutput motionOutput = (MotionOutput)value;
                    switch (motionOutput)
                    {
                        case MotionOutput.Disabled:
                            SelectedProfile.Layout.RemoveLayout(AxisLayoutFlags.Gyroscope);
                            gyroActions = null;
                            break;
                        case MotionOutput.LeftStick:
                            if (gyroActions is not AxisActions)
                            {
                                gyroActions = new AxisActions()
                                {
                                    AxisAntiDeadZone = GyroActions.DefaultAxisAntiDeadZone,
                                    Axis = AxisLayoutFlags.LeftStick,
                                    MotionTrigger = (ButtonState)GyroHotkey.inputsChord.ButtonState.Clone(),
                                    MotionInput = preservedMotionInput,
                                    MotionMode = preservedMotionMode,
                                    gyroWeight = preservedGyroWeight
                                };
                            }
                            else if (gyroActions is AxisActions aa)
                            {
                                aa.Axis = AxisLayoutFlags.LeftStick;
                            }
                            break;
                        case MotionOutput.RightStick:
                            if (gyroActions is not AxisActions)
                            {
                                gyroActions = new AxisActions()
                                {
                                    AxisAntiDeadZone = GyroActions.DefaultAxisAntiDeadZone,
                                    Axis = AxisLayoutFlags.RightStick,
                                    MotionTrigger = (ButtonState)GyroHotkey.inputsChord.ButtonState.Clone(),
                                    MotionInput = preservedMotionInput,
                                    MotionMode = preservedMotionMode,
                                    gyroWeight = preservedGyroWeight
                                };
                            }
                            else if (gyroActions is AxisActions aa)
                            {
                                aa.Axis = AxisLayoutFlags.RightStick;
                            }
                            break;
                        case MotionOutput.MoveCursor:
                        case MotionOutput.ScrollWheel:
                            if (gyroActions is not MouseActions)
                            {
                                gyroActions = new MouseActions()
                                {
                                    MouseType = GyroActions.DefaultMouseActionsType,
                                    Sensivity = GyroActions.DefaultSensivity,
                                    Deadzone = GyroActions.DefaultDeadzone,
                                    MotionTrigger = (ButtonState)GyroHotkey.inputsChord.ButtonState.Clone(),
                                    MotionInput = preservedMotionInput,
                                    MotionMode = preservedMotionMode,
                                    gyroWeight = preservedGyroWeight
                                };
                            }
                            break;
                        case MotionOutput.LeftPad:
                        case MotionOutput.RightPad:
                            AxisLayoutFlags axis = motionOutput == MotionOutput.LeftPad
                                ? AxisLayoutFlags.LeftPad
                                : AxisLayoutFlags.RightPad;

                            if (gyroActions is TouchpadActions touchpadActions)
                            {
                                touchpadActions.SetTarget(axis);
                            }
                            else
                            {
                                gyroActions = new TouchpadActions(axis)
                                {
                                    MotionTrigger = (ButtonState)GyroHotkey.inputsChord.ButtonState.Clone(),
                                    MotionInput = preservedMotionInput,
                                    MotionMode = preservedMotionMode,
                                    gyroWeight = preservedGyroWeight
                                };
                            }
                            break;
                    }

                    if (gyroActions is not null)
                        SelectedProfile.Layout.UpdateLayout(AxisLayoutFlags.Gyroscope, gyroActions);

                    SubmitProfile();
                }
            }
        }

        private int _InputMode;
        public int InputMode
        {
            get => _InputMode;
            set
            {
                if (value != _InputMode)
                {
                    _InputMode = value;
                    OnPropertyChanged(nameof(InputMode));

                    // Only write back to profile if we're not loading from it
                    if (isLoadingProfile || SelectedProfile is null)
                        return;

                    if (!SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? currentAction))
                        return;

                    if (currentAction is GyroActions gyroActions)
                        gyroActions.MotionInput = (MotionInput)value;

                    UpdateProfile();
                }
            }
        }

        private int _MotionMode;
        public int MotionMode
        {
            get => _MotionMode;
            set
            {
                if (value != _MotionMode)
                {
                    _MotionMode = value;
                    OnPropertyChanged(nameof(MotionMode));

                    // Only write back to profile if we're not loading from it
                    if (isLoadingProfile || SelectedProfile is null)
                        return;

                    if (!SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? currentAction))
                        return;

                    if (currentAction is GyroActions gyroActions)
                        gyroActions.MotionMode = (MotionMode)value;

                    UpdateProfile();
                }
            }
        }

        private double _AntiDeadzoneValue;
        public double AntiDeadzoneValue
        {
            get => _AntiDeadzoneValue;
            set
            {
                if (value != _AntiDeadzoneValue)
                {
                    _AntiDeadzoneValue = value;
                    OnPropertyChanged(nameof(AntiDeadzoneValue));

                    // Only write back to profile if we're not loading from it
                    if (isLoadingProfile || SelectedProfile is null)
                        return;

                    if (!SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? currentAction))
                        return;

                    if (currentAction is AxisActions axisActions)
                        axisActions.AxisAntiDeadZone = (int)value;

                    UpdateProfile();
                }
            }
        }

        private double _GyroWeightValue = 1.0f;
        public double GyroWeightValue
        {
            get => _GyroWeightValue;
            set
            {
                if (value != _GyroWeightValue)
                {
                    _GyroWeightValue = value;
                    OnPropertyChanged(nameof(GyroWeightValue));

                    // Only write back to profile if we're not loading from it
                    if (isLoadingProfile || SelectedProfile is null)
                        return;

                    if (!SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? currentAction))
                        return;

                    if (currentAction is AxisActions axisActions)
                        axisActions.gyroWeight = (float)value;

                    UpdateProfile();
                }
            }
        }

        private double _SensitivityXValue = 1.0f;
        public double SensitivityXValue
        {
            get => _SensitivityXValue;
            set
            {
                if (value != _SensitivityXValue)
                {
                    _SensitivityXValue = value;
                    OnPropertyChanged(nameof(SensitivityXValue));

                    // Only write back to profile if we're not loading from it
                    if (isLoadingProfile || SelectedProfile is null)
                        return;

                    SelectedProfile.MotionSensivityX = (float)value;
                    UpdateProfile();
                }
            }
        }

        private double _SensitivityYValue = 1.0f;
        public double SensitivityYValue
        {
            get => _SensitivityYValue;
            set
            {
                if (value != _SensitivityYValue)
                {
                    _SensitivityYValue = value;
                    OnPropertyChanged(nameof(SensitivityYValue));

                    // Only write back to profile if we're not loading from it
                    if (isLoadingProfile || SelectedProfile is null)
                        return;

                    SelectedProfile.MotionSensivityY = (float)value;
                    UpdateProfile();
                }
            }
        }

        // ProfilesPage-specific properties
        private bool _HasWarning;
        public bool HasWarning
        {
            get => _HasWarning;
            set { if (value != _HasWarning) { _HasWarning = value; OnPropertyChanged(nameof(HasWarning)); } }
        }

        private string _WarningMessage = string.Empty;
        public string WarningMessage
        {
            get => _WarningMessage;
            set { if (value != _WarningMessage) { _WarningMessage = value; OnPropertyChanged(nameof(WarningMessage)); } }
        }

        private bool _IsWrapperInjectionEnabled = true;
        public bool IsWrapperInjectionEnabled
        {
            get => _IsWrapperInjectionEnabled;
            set { if (value != _IsWrapperInjectionEnabled) { _IsWrapperInjectionEnabled = value; OnPropertyChanged(nameof(IsWrapperInjectionEnabled)); } }
        }

        private bool _IsWrapperRedirectionEnabled = true;
        public bool IsWrapperRedirectionEnabled
        {
            get => _IsWrapperRedirectionEnabled;
            set { if (value != _IsWrapperRedirectionEnabled) { _IsWrapperRedirectionEnabled = value; OnPropertyChanged(nameof(IsWrapperRedirectionEnabled)); } }
        }

        // Separate flag for ProfileDetailsExpander (Path/Arguments/Executables section)
        // This is disabled for Default profile since those are structural properties
        private bool _IsProfileDetailsExpanderEnabled = true;
        public bool IsProfileDetailsExpanderEnabled
        {
            get => _IsProfileDetailsExpanderEnabled;
            set { if (value != _IsProfileDetailsExpanderEnabled) { _IsProfileDetailsExpanderEnabled = value; OnPropertyChanged(nameof(IsProfileDetailsExpanderEnabled)); } }
        }

        private string _SelectedPowerProfileName = string.Empty;
        public string SelectedPowerProfileName
        {
            get => _SelectedPowerProfileName;
            set { if (value != _SelectedPowerProfileName) { _SelectedPowerProfileName = value; OnPropertyChanged(nameof(SelectedPowerProfileName)); } }
        }

        private string _CurrentProfileName = string.Empty;
        public string CurrentProfileName
        {
            get => _CurrentProfileName;
            set { if (value != _CurrentProfileName) { _CurrentProfileName = value; OnPropertyChanged(nameof(CurrentProfileName)); } }
        }

        private string _CurrentProfileDescription = string.Empty;
        public string CurrentProfileDescription
        {
            get => _CurrentProfileDescription;
            set { if (value != _CurrentProfileDescription) { _CurrentProfileDescription = value; OnPropertyChanged(nameof(CurrentProfileDescription)); } }
        }

        private string _ProfileArguments = string.Empty;
        public string ProfileArguments
        {
            get => _ProfileArguments;
            set
            {
                if (value != _ProfileArguments)
                {
                    _ProfileArguments = value;
                    OnPropertyChanged(nameof(ProfileArguments));

                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.Arguments != value)
                    {
                        SelectedProfile.Arguments = value;
                        UpdateProfile();
                    }
                }
            }
        }

        private string _ProfileLaunchString = string.Empty;
        public string ProfileLaunchString
        {
            get => _ProfileLaunchString;
            set
            {
                if (value != _ProfileLaunchString)
                {
                    _ProfileLaunchString = value;
                    OnPropertyChanged(nameof(ProfileLaunchString));

                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.LaunchString != value)
                    {
                        SelectedProfile.LaunchString = value;
                        UpdateProfile();
                    }
                }
            }
        }

        private string _createProfileName = string.Empty;
        public string CreateProfileName
        {
            get => _createProfileName;
            set
            {
                if (value != _createProfileName)
                {
                    _createProfileName = value;
                    OnPropertyChanged(nameof(CreateProfileName));
                }
            }
        }

        private bool _copyDefaultProfileSettings = false;
        public bool CopyDefaultProfileSettings
        {
            get => _copyDefaultProfileSettings;
            set
            {
                if (value != _copyDefaultProfileSettings)
                {
                    _copyDefaultProfileSettings = value;
                    OnPropertyChanged(nameof(CopyDefaultProfileSettings));
                }
            }
        }

        public int SteeringAxisIndex
        {
            get => SelectedProfile != null ? (int)SelectedProfile.SteeringAxis : 0;
            set
            {
                if (SelectedProfile != null && (int)SelectedProfile.SteeringAxis != value)
                {
                    SelectedProfile.SteeringAxis = (SteeringAxis)value;
                    OnPropertyChanged(nameof(SteeringAxisIndex));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public int HIDModeIndex
        {
            get
            {
                if (SelectedProfile == null) return 0;
                return SelectedProfile.HID switch
                {
                    HIDmode.Xbox360Controller => 1,
                    HIDmode.DualShock4Controller => 2,
                    HIDmode.DualSenseController => 3,
                    HIDmode.SteamDeckController => 4,
                    HIDmode.SwitchProController => 5,
                    HIDmode.SteamController => 6,
                    HIDmode.NoController => 7,
                    _ => 0 // NotSelected
                };
            }
            set
            {
                HIDmode mode = value switch
                {
                    1 => HIDmode.Xbox360Controller,
                    2 => HIDmode.DualShock4Controller,
                    3 => HIDmode.DualSenseController,
                    4 => HIDmode.SteamDeckController,
                    5 => HIDmode.SwitchProController,
                    6 => HIDmode.SteamController,
                    7 => HIDmode.NoController,
                    _ => HIDmode.NotSelected
                };
                if (SelectedProfile != null && SelectedProfile.HID != mode)
                {
                    SelectedProfile.HID = mode;
                    OnPropertyChanged(nameof(HIDModeIndex));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool CanEditHIDMode
        {
            get => SelectedProfile != null && !SelectedProfile.Default;
        }

        public int XInputPlusIndex
        {
            get => SelectedProfile != null ? (int)SelectedProfile.XInputPlus : 0;
            set
            {
                if (SelectedProfile != null && (int)SelectedProfile.XInputPlus != value)
                {
                    SelectedProfile.XInputPlus = (XInputPlusMethod)value;
                    OnPropertyChanged(nameof(XInputPlusIndex));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        // Additional Profile Wrapper Properties
        public string ProfilePath
        {
            get => SelectedProfile?.Path ?? string.Empty;
            set
            {
                if (SelectedProfile != null && SelectedProfile.Path != value)
                {
                    SelectedProfile.Path = value;
                    OnPropertyChanged(nameof(ProfilePath));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool ProfileWhitelisted
        {
            get => SelectedProfile?.Whitelisted ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.Whitelisted != value)
                {
                    SelectedProfile.Whitelisted = value;
                    OnPropertyChanged(nameof(ProfileWhitelisted));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool ShowInLibrary
        {
            get => SelectedProfile?.ShowInLibrary ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.ShowInLibrary != value)
                {
                    SelectedProfile.ShowInLibrary = value;
                    OnPropertyChanged(nameof(ShowInLibrary));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool MotionInvertHorizontal
        {
            get => SelectedProfile?.MotionInvertHorizontal ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.MotionInvertHorizontal != value)
                {
                    SelectedProfile.MotionInvertHorizontal = value;
                    OnPropertyChanged(nameof(MotionInvertHorizontal));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool MotionInvertVertical
        {
            get => SelectedProfile?.MotionInvertVertical ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.MotionInvertVertical != value)
                {
                    SelectedProfile.MotionInvertVertical = value;
                    OnPropertyChanged(nameof(MotionInvertVertical));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool SuspendOnQT
        {
            get => SelectedProfile?.SuspendOnQT ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.SuspendOnQT != value)
                {
                    SelectedProfile.SuspendOnQT = value;
                    OnPropertyChanged(nameof(SuspendOnQT));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool SuspendOnSleep
        {
            get => SelectedProfile?.SuspendOnSleep ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.SuspendOnSleep != value)
                {
                    SelectedProfile.SuspendOnSleep = value;
                    OnPropertyChanged(nameof(SuspendOnSleep));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool FullScreenOptimization
        {
            get => SelectedProfile?.FullScreenOptimization ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.FullScreenOptimization != value)
                {
                    SelectedProfile.FullScreenOptimization = value;
                    OnPropertyChanged(nameof(FullScreenOptimization));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool HighDPIAware
        {
            get => SelectedProfile?.HighDPIAware ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.HighDPIAware != value)
                {
                    SelectedProfile.HighDPIAware = value;
                    OnPropertyChanged(nameof(HighDPIAware));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool IsLiked
        {
            get => SelectedProfile?.IsLiked ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.IsLiked != value)
                {
                    SelectedProfile.IsLiked = value;
                    OnPropertyChanged(nameof(IsLiked));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }
        #endregion

        #region Library
        private string _LibrarySearchField = string.Empty;
        public string LibrarySearchField
        {
            get => _LibrarySearchField;
            set
            {
                if (_LibrarySearchField != value)
                {
                    _LibrarySearchField = value;
                    OnPropertyChanged(nameof(LibrarySearchField));
                }
            }
        }

        private LibraryEntry? _SelectedLibraryEntry = null;
        public LibraryEntry? SelectedLibraryEntry
        {
            get => _SelectedLibraryEntry;
            set
            {
                if (_SelectedLibraryEntry != value)
                {
                    _SelectedLibraryEntry = value;
                    UpdateSelectedLibraryIndex(value);

                    OnPropertyChanged(nameof(SelectedLibraryEntry));
                    SelectedLibraryChanged();
                }
            }
        }

        private int _SelectedLibraryIndex;
        public int SelectedLibraryIndex
        {
            get => _SelectedLibraryIndex;
            set
            {
                if (_SelectedLibraryIndex != value)
                {
                    _SelectedLibraryIndex = value;
                    UpdateSelectedLibraryEntry(value);

                    OnPropertyChanged(nameof(SelectedLibraryEntry));
                    OnPropertyChanged(nameof(SelectedLibraryIndex));
                    SelectedLibraryChanged();
                }
            }
        }

        private void UpdateSelectedLibraryIndex(LibraryEntry? entry)
        {
            _SelectedLibraryIndex = GetLibraryIndex(entry);
            OnPropertyChanged(nameof(SelectedLibraryIndex));
        }

        private int GetLibraryIndex(LibraryEntry? entry)
        {
            if (entry is null)
                return -1;

            LibraryEntryViewModel? matchingPicker = LibraryPickers.FirstOrDefault(p => p.Id == entry.Id);
            return matchingPicker is not null ? LibraryPickers.IndexOf(matchingPicker) : -1;
        }

        private void UpdateSelectedLibraryEntry(int index)
        {
            _SelectedLibraryEntry = index >= 0 && index < LibraryPickers.Count
                ? LibraryPickers[index].LibEntry
                : null;
        }

        private int _LibraryCoversIndex;
        public int LibraryCoversIndex
        {
            get => _LibraryCoversIndex;
            set
            {
                if (value != -1)
                {
                    // Update the bound index before starting the asynchronous download.
                    // Otherwise the UI observes the new page count while the index is
                    // still -1 and may attempt to index the visual collection with it.
                    SetLibraryCoversIndex(value);
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.cover | LibraryType.thumbnails);
                }
                else
                    RefreshCover(value);
            }
        }

        private void SetLibraryCoversIndex(int value)
        {
            value = value >= 0 && value < LibraryCovers.Count ? value : -1;
            if (_LibraryCoversIndex != value)
            {
                _LibraryCoversIndex = value;
                OnPropertyChanged(nameof(LibraryCoversIndex));
            }
        }

        public ObservableCollection<LibraryVisualViewModel> LibraryCovers
        {
            get
            {
                if (_SelectedLibraryIndex != -1 && _SelectedLibraryIndex < LibraryPickers.Count)
                    return LibraryPickers[_SelectedLibraryIndex].LibraryCovers;
                return new();
            }
        }

        public int LibraryCoversPageCount => LibraryCovers.Count;

        private int _LibraryArtworksIndex;
        public int LibraryArtworksIndex
        {
            get => _LibraryArtworksIndex;
            set
            {
                if (value != -1)
                {
                    SetLibraryArtworksIndex(value);
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.artwork | LibraryType.thumbnails);
                }
                else
                    RefreshArtwork(value);
            }
        }

        private void SetLibraryArtworksIndex(int value)
        {
            value = value >= 0 && value < LibraryArtworks.Count ? value : -1;
            if (_LibraryArtworksIndex != value)
            {
                _LibraryArtworksIndex = value;
                OnPropertyChanged(nameof(LibraryArtworksIndex));
            }
        }

        public ObservableCollection<LibraryVisualViewModel> LibraryArtworks
        {
            get
            {
                if (_SelectedLibraryIndex != -1 && _SelectedLibraryIndex < LibraryPickers.Count)
                    return LibraryPickers[_SelectedLibraryIndex].LibraryArtworks;
                return new();
            }
        }

        public int LibraryArtworksPageCount => LibraryArtworks.Count;

        private int _LibraryLogosIndex;
        public int LibraryLogosIndex
        {
            get => _LibraryLogosIndex;
            set
            {
                if (value != -1)
                {
                    SetLibraryLogosIndex(value);
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.logo | LibraryType.thumbnails);
                }
                else
                    RefreshLogo(value);
            }
        }

        private void SetLibraryLogosIndex(int value)
        {
            value = value >= 0 && value < LibraryLogos.Count ? value : -1;
            if (_LibraryLogosIndex != value)
            {
                _LibraryLogosIndex = value;
                OnPropertyChanged(nameof(LibraryLogosIndex));
            }
        }

        public ObservableCollection<LibraryVisualViewModel> LibraryLogos
        {
            get
            {
                if (_SelectedLibraryIndex != -1 && _SelectedLibraryIndex < LibraryPickers.Count)
                    return LibraryPickers[_SelectedLibraryIndex].LibraryLogos;
                return new();
            }
        }

        public int LibraryLogosPageCount => LibraryLogos.Count;

        public bool QuerySteamGrid { get; set; } = true;
        public bool QueryIGDB { get; set; } = true;

        public bool IsLibraryOrManualEnabled => IsLibraryConnected || SelectedLibraryEntry is not null;

        public BitmapImage? Cover
        {
            get
            {
                if (SelectedProfile?.LibraryEntry == null)
                    return LibraryResources.MissingCover;

                long id = SelectedProfile.LibraryEntry.Id;
                long imageId = SelectedProfile.LibraryEntry.GetCoverId();
                string imageExtension = SelectedProfile.LibraryEntry.GetCoverExtension(false);

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryType.cover, imageId, imageExtension);
            }
        }

        public BitmapImage? Artwork
        {
            get
            {
                if (SelectedProfile?.LibraryEntry == null)
                    return LibraryResources.MissingArtwork;

                long id = SelectedProfile.LibraryEntry.Id;
                long imageId = SelectedProfile.LibraryEntry.GetArtworkId();
                string imageExtension = SelectedProfile.LibraryEntry.GetArtworkExtension(false);

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryType.artwork, imageId, imageExtension);
            }
        }

        public BitmapImage? Logo
        {
            get
            {
                if (SelectedProfile?.LibraryEntry == null)
                    return null;

                long id = SelectedProfile.LibraryEntry.Id;
                long imageId = SelectedProfile.LibraryEntry.GetLogoId();
                string imageExtension = SelectedProfile.LibraryEntry.GetLogoExtension(false);

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryType.logo, imageId, imageExtension);
            }
        }
        #endregion

        #region PowerProfile
        private PowerProfile _selectedPresetDC = null!;
        public PowerProfile SelectedPresetDC => _selectedPresetDC;

        private ProfilesPickerViewModel? _selectedPickerDC;
        public ProfilesPickerViewModel? SelectedPickerDC
        {
            get => _selectedPickerDC;
            set
            {
                if (_selectedPickerDC != value)
                {
                    _selectedPickerDC = value;
                    OnPropertyChanged(nameof(SelectedPickerDC));
                    SelectPresetDC(value);
                }
            }
        }

        private PowerProfile _selectedPresetAC = null!;
        public PowerProfile SelectedPresetAC => _selectedPresetAC;

        private ProfilesPickerViewModel? _selectedPickerAC;
        public ProfilesPickerViewModel? SelectedPickerAC
        {
            get => _selectedPickerAC;
            set
            {
                if (_selectedPickerAC != value)
                {
                    _selectedPickerAC = value;
                    OnPropertyChanged(nameof(SelectedPickerAC));
                    SelectPresetAC(value);
                }
            }
        }

        private void SelectPresetDC(ProfilesPickerViewModel? picker)
        {
            if (picker?.LinkedPresetId == null)
                return;

            _selectedPresetDC = ManagerFactory.powerProfileManager.GetProfile(picker.LinkedPresetId.Value);
            OnPropertyChanged(nameof(SelectedPresetDC));

            if (!isLoadingProfile)
                PowerProfile_Selected(_selectedPresetDC, false);
        }

        private void SelectPresetAC(ProfilesPickerViewModel? picker)
        {
            if (picker?.LinkedPresetId == null)
                return;

            _selectedPresetAC = ManagerFactory.powerProfileManager.GetProfile(picker.LinkedPresetId.Value);
            OnPropertyChanged(nameof(SelectedPresetAC));

            if (!isLoadingProfile)
                PowerProfile_Selected(_selectedPresetAC, true);
        }
        #endregion

        #region Framerate limit overrides
        // slots use PowerLineStatus keys: 0 = on battery (DC), 1 = plugged in (AC)
        private const int FramerateSlotDC = 0;
        private const int FramerateSlotAC = 1;

        // combo key of the Custom entry, the preset entries use their limit
        private const string FramerateCustomKey = "custom";

        // same choices as the power profile page: presets with Disabled (0) first, then Custom.
        // Each slot lists the presets of the refresh rate that applies while the game runs on that power state.
        private readonly IReadOnlyList<ScreenFramelimitViewModel>[] _framerateLimits =
        [
            ScreenFramelimitViewModel.BuildList(null),
            ScreenFramelimitViewModel.BuildList(null),
        ];
        private readonly int[] _framerateLimitsHz = new int[2];
        private readonly int[] _framerateCustomMaximum = [60, 60];

        private int GetFramerateCustomMaximum(int slot) => Math.Max(ScreenFramelimitViewModel.CustomMinimum, _framerateCustomMaximum[slot]);

        // Custom was picked for a slot, its value may still match a preset.
        // This is view state only (not stored in the profile): it belongs to the profile it was picked on
        // and to the value written with it, so a reload of that same profile keeps Custom open.
        private readonly bool[] _framerateCustomSelected = new bool[2];
        private readonly int[] _framerateCustomValue = new int[2];
        private Guid _framerateCustomProfile = Guid.Empty;

        private void MarkFramerateCustom(int slot, int value)
        {
            _framerateCustomSelected[slot] = true;
            _framerateCustomValue[slot] = value;
            _framerateCustomProfile = SelectedProfile?.Guid ?? Guid.Empty;
        }

        /// <summary>
        /// Profile reloads (our own save coming back through ProfileManager.Updated/Applied) must not drop Custom:
        /// a custom value equal to a preset would otherwise snap to that preset and close the slider row.
        /// Custom is only dropped on another profile, when the slot left Override, or when the value was changed elsewhere.
        /// </summary>
        private void RevalidateFramerateCustom()
        {
            bool sameProfile = SelectedProfile is not null && SelectedProfile.Guid == _framerateCustomProfile;
            for (int slot = 0; slot < _framerateCustomSelected.Length; slot++)
            {
                if (_framerateCustomSelected[slot] &&
                    (!sameProfile || !IsFramerateOverride(slot) || GetFramerateValue(slot) != _framerateCustomValue[slot]))
                    _framerateCustomSelected[slot] = false;
            }
        }

        // one framerate limit row per power slot
        public FramerateOverrideRow? FramerateRowDC { get; private set; }
        public FramerateOverrideRow? FramerateRowAC { get; private set; }

        private IEnumerable<FramerateOverrideRow> AllFramerateOverrideRows => new[] { FramerateRowDC, FramerateRowAC }.OfType<FramerateOverrideRow>();

        private void InitializeFramerateOverrideRows()
        {
            FramerateRowDC = new(this, FramerateSlotDC);
            FramerateRowAC = new(this, FramerateSlotAC);
        }

        private static string FormatKey(int value) => value.ToString(CultureInfo.InvariantCulture);

        private PerformanceOverrides? GetPerformanceOverrides(int slot, bool create)
        {
            if (SelectedProfile is null)
                return null;

            SelectedProfile.PerformanceOverrides ??= new();
            if (!SelectedProfile.PerformanceOverrides.TryGetValue(slot, out PerformanceOverrides? overrides) || overrides is null)
            {
                if (!create)
                    return null;

                overrides = new();
                SelectedProfile.PerformanceOverrides[slot] = overrides;
            }

            return overrides;
        }

        private FramerateLimitMode GetFramerateMode(int slot)
        {
            return (GetPerformanceOverrides(slot, false)?.FramerateMode ?? FramerateLimitMode.Inherit).Normalize();
        }

        private int GetFramerateValue(int slot)
        {
            return Math.Max(0, GetPerformanceOverrides(slot, false)?.FramerateValue ?? 0);
        }

        private PowerProfile? GetSlotPowerProfile(int slot)
        {
            if (SelectedProfile is null || !SelectedProfile.PowerProfiles.TryGetValue(slot, out Guid guid))
                return null;

            return ManagerFactory.powerProfileManager.GetProfile(guid);
        }

        private bool IsFramerateOverride(int slot) => GetFramerateMode(slot) == FramerateLimitMode.Override;

        // what this layer gets without its own limit: the default profile, then the power profile;
        // the default profile itself only has the power profile below it
        private int GetInheritedFramerateLimit(int slot)
        {
            PowerProfile? powerProfile = GetSlotPowerProfile(slot);
            if (SelectedProfile?.Default == true)
                return PowerProfileManager.GetFramerateBaseline(powerProfile);

            (_, int value, _) = ManagerFactory.powerProfileManager.ResolveFramerateLimit(null, slot, powerProfile);
            return value;
        }

        private string GetFramerateInheritLabel(int slot)
        {
            string format = SelectedProfile?.Default == true ? Properties.Resources.OverrideMode_UsePowerProfile : Properties.Resources.OverrideMode_UseDefaultProfile;
            return string.Format(format, GetFramerateValueLabel(GetInheritedFramerateLimit(slot)));
        }

        private static string GetFramerateValueLabel(int value)
        {
            return value > 0 ? $"{value} FPS" : Properties.Resources.ProfilesPage_FramerateNoLimit;
        }

        private IEnumerable<(string Key, string Label, bool IsEnabled)> GetFramerateValueChoices(int slot)
        {
            return _framerateLimits[slot].Select(vm => (vm.IsCustom ? FramerateCustomKey : FormatKey(vm.FrameLimit.limit), vm.ToString(), true));
        }

        private string GetFramerateChoiceKey(int slot)
        {
            if (!IsFramerateOverride(slot))
                return OverrideChoice.InheritKey;

            return IsCustomFrameLimit(slot) ? FramerateCustomKey : FormatKey(GetFramerateValue(slot));
        }

        private void SelectFramerateChoice(int slot, string? key)
        {
            // a list swap clears the selection on its way, that is not a pick
            if (string.IsNullOrEmpty(key) || key == GetFramerateChoiceKey(slot))
                return;

            PerformanceOverrides? overrides = GetPerformanceOverrides(slot, true);
            if (overrides is null)
                return;

            if (key == OverrideChoice.InheritKey)
            {
                overrides.FramerateMode = FramerateLimitMode.Inherit;
                _framerateCustomSelected[slot] = false;
            }
            else
            {
                // a new override starts from the limit that applies right now (0 = no limit)
                if (!IsFramerateOverride(slot))
                {
                    overrides.FramerateValue = GetInheritedFramerateLimit(slot);
                    overrides.FramerateMode = FramerateLimitMode.Override;
                }

                if (key == FramerateCustomKey)
                {
                    // Custom starts from the limit in effect (the resolved/inherited one, or the preset picked before).
                    // A custom limit is never 0: coming from no limit it starts at the refresh rate, the top of the
                    // slider and the closest cap to "no limit", so picking Custom alone doesn't throttle the game.
                    if (overrides.FramerateValue <= 0)
                    {
                        int refreshRate = _framerateLimitsHz[slot] > 0 ? _framerateLimitsHz[slot] : GetFramerateCustomMaximum(slot);
                        overrides.FramerateValue = Math.Min(refreshRate, GetFramerateCustomMaximum(slot));
                    }

                    MarkFramerateCustom(slot, overrides.FramerateValue);
                }
                else if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit))
                {
                    _framerateCustomSelected[slot] = false;
                    overrides.FramerateValue = Math.Max(0, limit);
                }
            }

            RefreshFramerateOverrides();

            if (!isLoadingProfile)
                UpdateProfile();
        }

        private bool IsCustomFrameLimit(int slot)
        {
            if (_framerateCustomSelected[slot])
                return true;

            int value = GetFramerateValue(slot);
            return !_framerateLimits[slot].Any(vm => !vm.IsCustom && vm.FrameLimit.limit == value);
        }

        private double GetCustomFramerate(int slot)
        {
            int value = GetFramerateValue(slot);
            if (value <= 0)
                return GetFramerateCustomMaximum(slot);

            return Math.Clamp(value, ScreenFramelimitViewModel.CustomMinimum, GetFramerateCustomMaximum(slot));
        }

        private void SetCustomFramerate(int slot, double value)
        {
            if (double.IsNaN(value) || !IsCustomFrameLimit(slot))
                return;

            int framerate = Math.Clamp((int)Math.Round(value), ScreenFramelimitViewModel.CustomMinimum, GetFramerateCustomMaximum(slot));
            MarkFramerateCustom(slot, framerate);
            WriteFramerateValue(slot, framerate);
        }

        private void WriteFramerateValue(int slot, int framerate)
        {
            PerformanceOverrides? overrides = GetPerformanceOverrides(slot, true);
            if (overrides is null)
                return;

            bool changed = overrides.FramerateValue != framerate;
            overrides.FramerateValue = framerate;
            RefreshFramerateOverrides();

            if (changed && !isLoadingProfile)
                UpdateProfile();
        }

        /// <summary>
        /// Monitor whose refresh rate drives the presets: the one the game window was last active on,
        /// the primary monitor when the game never ran, that monitor isn't connected, or for the default profile.
        /// </summary>
        private DisplayMonitorOption? GetFramerateMonitor()
        {
            return GetStoredFramerateMonitor() ?? GetPrimaryMonitor();
        }

        private DisplayMonitorOption? GetPrimaryMonitor()
        {
            return _displayMonitors.FirstOrDefault(m => m.IsPrimary) ?? _displayMonitors.FirstOrDefault();
        }

        // connected monitor the game window was last active on, sub profiles fall back to their parent
        private DisplayMonitorOption? GetStoredFramerateMonitor()
        {
            if (SelectedProfile is null || SelectedProfile.Default)
                return null;

            Guid monitorId = DisplayModeHelper.GetLastMonitorId(SelectedProfile);
            return monitorId == Guid.Empty ? null : _displayMonitors.FirstOrDefault(m => m.Id == monitorId);
        }

        // a process of the selected game (or of its parent for a sub profile) is running
        private bool IsSelectedGameRunning()
        {
            if (SelectedProfile is null || SelectedProfile.Default)
                return false;

            return DisplayModeHelper.IsGameRunning(SelectedProfile);
        }

        private IReadOnlyList<DisplayMonitorItem> _displayMonitorItems = [];
        public IReadOnlyList<DisplayMonitorItem> DisplayMonitorItems => _displayMonitorItems;

        /// <summary>
        /// Rebuilds the monitor entries: Main on the current primary monitor, and one FPS presets source badge.
        /// </summary>
        private void RefreshDisplayMonitorBadges()
        {
            DisplayMonitorOption? stored = GetStoredFramerateMonitor();
            Guid sourceId = stored?.Id ?? GetPrimaryMonitor()?.Id ?? Guid.Empty;
            FramerateSourceBadge sourceBadge = stored is null
                ? FramerateSourceBadge.Default
                : IsSelectedGameRunning() ? FramerateSourceBadge.RunningHere : FramerateSourceBadge.LastUsed;

            _displayMonitorItems = _displayMonitors
                .Select(m => new DisplayMonitorItem(m.Id, m.Label, m.IsPrimary, m.Id == sourceId ? sourceBadge : FramerateSourceBadge.None) { IsInternal = m.IsInternal })
                .ToList();
            OnPropertyChanged(nameof(DisplayMonitorItems));

            // one override row per monitor and slot, with the same badges
            UpdateDisplayOverrideRows(_displayMonitorItems);
        }

        // a game starting or exiting switches its badge between Running here and Last used
        private void DisplayBadges_ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            UIHelper.TryBeginInvoke(RefreshDisplayMonitorBadges);
        }

        private void DisplayBadges_ProcessStopped(ProcessEx processEx)
        {
            UIHelper.TryBeginInvoke(RefreshDisplayMonitorBadges);
        }

        /// <summary>
        /// Refresh rate that applies on a slot while the game runs: the resolved refresh rate override
        /// (game profile, then default profile), else the current refresh rate, snapped like the apply path does.
        /// Also returns the highest refresh rate of the resolution that applies.
        /// </summary>
        private (int RefreshRate, int MaximumRate) GetFramerateRefreshRate(int slot)
        {
            DisplayMonitorOption? monitor = GetFramerateMonitor();
            if (monitor is null)
            {
                // monitors not listed yet, behave like before
                DesktopScreen? desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                return (desktopScreen?.GetCurrentFrequency() ?? 0, desktopScreen?.GetMaximumFrequency() ?? 60);
            }

            ResolvedDisplayMode resolved = SelectedProfile is null
                ? default
                : ManagerFactory.powerProfileManager.ResolveDisplayMode(SelectedProfile, slot, monitor.Id);

            // integer scaling owns the primary display resolution
            bool resolutionLocked = IsResolutionLocked(slot, monitor);
            (int width, int height) = resolved.HasResolution && !resolutionLocked
                ? (resolved.Width, resolved.Height)
                : (monitor.CurrentWidth, monitor.CurrentHeight);

            IReadOnlyList<DisplayRefreshOption> refreshRates = FindResolution(monitor, width, height)?.RefreshRates ?? [];

            int refreshRate = SnapRefreshRate(refreshRates, resolved.HasRefreshRate ? resolved.RefreshRate : monitor.CurrentFrequency);
            int maximumRate = refreshRates.Count > 0 ? refreshRates.Max(r => r.Frequency) : refreshRate;
            return (refreshRate, maximumRate);
        }

        /// <summary>
        /// Rebuilds the preset list of both slots from their effective refresh rate.
        /// The stored limit is never changed here: a value missing from the new presets shows as Custom with its own value.
        /// </summary>
        private void RegenerateFramerateLimits()
        {
            for (int slot = 0; slot < 2; slot++)
            {
                (int refreshRate, int maximumRate) = GetFramerateRefreshRate(slot);

                if (refreshRate != _framerateLimitsHz[slot] || _framerateLimits[slot].Count <= 1)
                {
                    _framerateLimitsHz[slot] = refreshRate;
                    _framerateLimits[slot] = ScreenFramelimitViewModel.BuildList(refreshRate);
                }

                // keep a stored custom limit above the new maximum reachable and visible
                int storedValue = GetFramerateValue(slot);
                _framerateCustomMaximum[slot] = Math.Max(maximumRate > 0 ? maximumRate : 60, storedValue);
            }

            RefreshFramerateOverrides();

            // the presets source monitor drives the monitor badges
            RefreshDisplayMonitorBadges();
        }

        private void MultimediaManager_PrimaryScreenChanged(DesktopScreen? screen)
        {
            // raised while the display list is locked, list the monitors later on the UI thread
            UIHelper.TryBeginInvoke(UpdateDisplayMonitors);
        }

        private void ProfileManager_LastMonitorChanged(Profile profile)
        {
            if (SelectedProfile is null)
                return;

            if (SelectedProfile.Guid == profile.Guid || SelectedProfile.ParentGuid == profile.Guid)
                UIHelper.TryBeginInvoke(RegenerateFramerateLimits);
        }

        /// <summary>
        /// Re-reads the monitors and rebuilds the presets, called when the page is shown.
        /// </summary>
        public void RefreshDisplayState()
        {
            UpdateDisplayMonitors();
        }

        // the inherited limit comes from the default profile or the power profile, both may change while the page is open
        private void RefreshFramerateHints()
        {
            UIHelper.TryBeginInvoke(RefreshFramerateOverrides);
        }

        private void RefreshFramerateOverrides()
        {
            foreach (FramerateOverrideRow row in AllFramerateOverrideRows)
                row.Refresh();
        }

        /// <summary>
        /// Framerate limit of one power slot: one choice list (Inherit, the presets, Custom) and a slider row for Custom.
        /// </summary>
        public sealed class FramerateOverrideRow : BaseViewModel
        {
            private readonly ProfilesPageViewModel _owner;
            private readonly OverrideChoiceList _choices = new();

            public FramerateOverrideRow(ProfilesPageViewModel owner, int slot)
            {
                _owner = owner;
                Slot = slot;
            }

            public int Slot { get; }

            public IReadOnlyList<OverrideChoice> Choices => _choices.Items;

            // key of the selected entry, stable across lists: the preset entries are rebuilt when the refresh rate changes
            public string SelectedKey
            {
                get => _owner.GetFramerateChoiceKey(Slot);
                set => _owner.SelectFramerateChoice(Slot, value);
            }

            // bound through SelectedItem: a replaced list hands the combo a new entry, so it always selects again
            public OverrideChoice? SelectedChoice
            {
                get => _choices.Find(SelectedKey);
                set => SelectedKey = value?.Key ?? string.Empty;
            }

            public bool ShowCustom => _owner.IsFramerateOverride(Slot) && _owner.IsCustomFrameLimit(Slot);
            public int CustomMinimum => ScreenFramelimitViewModel.CustomMinimum;
            public int CustomMaximum => _owner.GetFramerateCustomMaximum(Slot);

            public double CustomValue
            {
                get => _owner.GetCustomFramerate(Slot);
                set => _owner.SetCustomFramerate(Slot, value);
            }

            internal void Refresh()
            {
                // option list before its selection
                if (_choices.Update(_owner.GetFramerateInheritLabel(Slot), _owner.GetFramerateValueChoices(Slot),
                    _owner.GetFramerateChoiceKey(Slot), () => GetFramerateValueLabel(_owner.GetFramerateValue(Slot))))
                    _choices.Publish(OnPropertyChanged, nameof(Choices), nameof(SelectedChoice));
                else
                    OnPropertyChanged(nameof(SelectedChoice));
                OnPropertyChanged(nameof(SelectedKey));
                OnPropertyChanged(nameof(ShowCustom));
                OnPropertyChanged(nameof(CustomMaximum));
                OnPropertyChanged(nameof(CustomValue));
            }
        }
        #endregion

        #region Display mode overrides
        // per monitor, per power slot; resolution and refresh rate are separate Inherit/Override fields

        // connected monitors with a known id, primary first
        private IReadOnlyList<DisplayMonitorOption> _displayMonitors = [];
        public IReadOnlyList<DisplayMonitorOption> DisplayMonitors => _displayMonitors;
        public bool HasMultipleDisplays => _displayMonitors.Count > 1;

        public bool HasDisplayMonitors => _displayMonitors.Count > 0;

        // one override row per connected monitor and power slot, rebuilt with the monitor badges
        public ObservableCollection<DisplayOverrideRow> DisplayOverrideRowsDC { get; } = [];
        public ObservableCollection<DisplayOverrideRow> DisplayOverrideRowsAC { get; } = [];

        // rows are reused per monitor so an expanded monitor row stays expanded across refreshes
        private readonly Dictionary<Guid, DisplayOverrideRow>[] _displayOverrideRows = [new(), new()];

        private void UpdateDisplayOverrideRows(IReadOnlyList<DisplayMonitorItem> items)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                ObservableCollection<DisplayOverrideRow> rows = slot == FramerateSlotDC ? DisplayOverrideRowsDC : DisplayOverrideRowsAC;
                Dictionary<Guid, DisplayOverrideRow> cache = _displayOverrideRows[slot];

                List<DisplayOverrideRow> wanted = [];
                foreach (DisplayMonitorItem item in items)
                {
                    DisplayMonitorOption? monitor = _displayMonitors.FirstOrDefault(m => m.Id == item.Id);
                    if (monitor is null)
                        continue;

                    if (cache.TryGetValue(item.Id, out DisplayOverrideRow? row))
                        row.Update(monitor, item);
                    else
                        cache[item.Id] = row = new DisplayOverrideRow(this, slot, monitor, item);

                    wanted.Add(row);
                }

                // only touch the collection when the connected monitors changed
                if (!rows.SequenceEqual(wanted))
                {
                    rows.Clear();
                    foreach (DisplayOverrideRow row in wanted)
                        rows.Add(row);
                }

                foreach (DisplayOverrideRow row in rows)
                    row.Refresh();
            }
        }

        private DisplayModeOverride? GetDisplayModeOverride(int slot, DisplayMonitorOption monitor, bool create)
        {
            PerformanceOverrides? overrides = GetPerformanceOverrides(slot, create);
            if (overrides is null)
                return null;

            overrides.DisplayModes ??= new();
            if (!overrides.DisplayModes.TryGetValue(monitor.Id, out DisplayModeOverride? displayMode) || displayMode is null)
            {
                if (!create)
                    return null;

                displayMode = new();
                overrides.DisplayModes[monitor.Id] = displayMode;
            }

            return displayMode;
        }

        // an entry with both fields inherited means nothing, keep the profile clean
        private void PruneDisplayModeOverride(int slot, DisplayMonitorOption monitor)
        {
            PerformanceOverrides? overrides = GetPerformanceOverrides(slot, false);
            if (overrides?.DisplayModes is null)
                return;

            if (overrides.DisplayModes.TryGetValue(monitor.Id, out DisplayModeOverride? displayMode)
                && (displayMode is null || (displayMode.ResolutionMode == DisplayOverrideMode.Inherit && displayMode.RefreshMode == DisplayOverrideMode.Inherit)))
                overrides.DisplayModes.Remove(monitor.Id);
        }

        private ResolvedDisplayMode ResolveDisplayMode(int slot, DisplayMonitorOption monitor)
        {
            if (SelectedProfile is null)
                return default;

            return ManagerFactory.powerProfileManager.ResolveDisplayMode(SelectedProfile, slot, monitor.Id);
        }

        // integer scaling owns the primary display resolution on the slots it resolves to on
        private bool IsResolutionLocked(int slot, DisplayMonitorOption monitor)
        {
            return monitor.IsPrimary && ResolveGraphics(slot).IntegerScaling.IsEnabled;
        }

        // resolution the refresh rates are listed for: this layer, the inherited one, then the system display mode
        private (int Width, int Height) GetEffectiveResolution(int slot, DisplayMonitorOption monitor)
        {
            if (IsResolutionLocked(slot, monitor))
                return (monitor.CurrentWidth, monitor.CurrentHeight);

            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, false);
            if (displayMode?.HasResolution == true)
                return (displayMode.Width, displayMode.Height);

            return GetInheritedResolution(slot, monitor);
        }

        // what this layer gets without its own display mode: the default profile for a game profile,
        // nothing for the default profile, which falls back to the system display mode
        private ResolvedDisplayMode ResolveInheritedDisplayMode(int slot, DisplayMonitorOption monitor)
        {
            if (SelectedProfile is null || SelectedProfile.Default)
                return default;

            return ManagerFactory.powerProfileManager.ResolveDisplayMode(null, slot, monitor.Id);
        }

        // what this layer gets without its own resolution: the inherited one, then the system display mode
        private (int Width, int Height) GetInheritedResolution(int slot, DisplayMonitorOption monitor)
        {
            ResolvedDisplayMode resolved = ResolveInheritedDisplayMode(slot, monitor);
            if (resolved.HasResolution)
                return (resolved.Width, resolved.Height);

            (int width, int height, _) = GetSystemDisplayMode(monitor);
            return (width, height);
        }

        private static DisplayResolutionOption? FindResolution(DisplayMonitorOption monitor, int width, int height)
        {
            return DisplayModeHelper.FindResolution(monitor.Resolutions, width, height, r => r.Width, r => r.Height);
        }

        private IReadOnlyList<DisplayRefreshOption> GetRefreshRates(int slot, DisplayMonitorOption monitor)
        {
            (int width, int height) = GetEffectiveResolution(slot, monitor);
            return FindResolution(monitor, width, height)?.RefreshRates ?? [];
        }

        // same rule as the apply path: closest available, higher on a tie
        private static int SnapRefreshRate(IReadOnlyList<DisplayRefreshOption> refreshRates, int frequency)
        {
            return DisplayModeHelper.SnapRefreshRate(refreshRates.Select(r => r.Frequency), frequency);
        }

        // chevron down when collapsed, up when expanded
        private static string GetChevronGlyph(bool expanded)
        {
            return expanded ? "\uE70E" : "\uE70D";
        }

        // what this layer gets without its own refresh rate, rounded like the apply path to a rate the effective resolution offers
        private int GetInheritedRefreshRate(int slot, DisplayMonitorOption monitor)
        {
            ResolvedDisplayMode resolved = ResolveInheritedDisplayMode(slot, monitor);
            int frequency = resolved.HasRefreshRate ? resolved.RefreshRate : GetSystemDisplayMode(monitor).Frequency;
            return SnapRefreshRate(GetRefreshRates(slot, monitor), frequency);
        }

        // the default profile inherits the system display mode, Quick Tools uses the short label
        private string DisplayInheritFormat => SelectedProfile?.Default == true
            ? (IsQuickTools ? Properties.Resources.OverrideMode_UseSystemSettingsShort : Properties.Resources.OverrideMode_UseSystemSettings)
            : Properties.Resources.OverrideMode_UseDefaultProfile;

        private string GetResolutionInheritLabel(int slot, DisplayMonitorOption monitor)
        {
            (int width, int height) = GetInheritedResolution(slot, monitor);
            return string.Format(DisplayInheritFormat, $"{width} x {height}");
        }

        private string GetRefreshInheritLabel(int slot, DisplayMonitorOption monitor)
        {
            return string.Format(DisplayInheritFormat, $"{GetInheritedRefreshRate(slot, monitor)} Hz");
        }

        private string GetStoredResolutionLabel(int slot, DisplayMonitorOption monitor)
        {
            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, false);
            return displayMode is null ? string.Empty : $"{displayMode.Width} x {displayMode.Height}";
        }

        private string GetStoredRefreshLabel(int slot, DisplayMonitorOption monitor)
        {
            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, false);
            return displayMode is null ? string.Empty : $"{displayMode.RefreshRate} Hz";
        }

        private static IEnumerable<(string Key, string Label, bool IsEnabled)> GetResolutionValueChoices(DisplayMonitorOption monitor)
        {
            return monitor.Resolutions.Select(r => (r.Key, r.Label, true));
        }

        private IEnumerable<(string Key, string Label, bool IsEnabled)> GetRefreshValueChoices(int slot, DisplayMonitorOption monitor)
        {
            return GetRefreshRates(slot, monitor).Select(r => (FormatKey(r.Frequency), r.Label, true));
        }

        private string GetResolutionChoiceKey(int slot, DisplayMonitorOption monitor)
        {
            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, false);
            if (displayMode is null || displayMode.ResolutionMode != DisplayOverrideMode.Override)
                return OverrideChoice.InheritKey;

            // a stored resolution the monitor doesn't list keeps its own key, the list shows it as not available
            if (FindResolution(monitor, displayMode.Width, displayMode.Height) is DisplayResolutionOption resolution)
                return resolution.Key;

            return displayMode.Width > 0 && displayMode.Height > 0 ? $"{displayMode.Width}x{displayMode.Height}" : string.Empty;
        }

        private void SelectResolutionChoice(int slot, DisplayMonitorOption monitor, string? key)
        {
            // a list swap clears the selection on its way, that is not a pick
            if (string.IsNullOrEmpty(key) || IsResolutionLocked(slot, monitor) || key == GetResolutionChoiceKey(slot, monitor))
                return;

            DisplayResolutionOption? resolution = null;
            if (key != OverrideChoice.InheritKey)
            {
                resolution = monitor.Resolutions.FirstOrDefault(r => r.Key == key);
                if (resolution is null)
                    return;
            }

            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, true);
            if (displayMode is null)
                return;

            if (resolution is null)
            {
                displayMode.ResolutionMode = DisplayOverrideMode.Inherit;
            }
            else
            {
                displayMode.ResolutionMode = DisplayOverrideMode.Override;
                displayMode.Width = resolution.Width;
                displayMode.Height = resolution.Height;

                // keep the stored refresh rate valid for the new resolution
                if (displayMode.RefreshMode == DisplayOverrideMode.Override && displayMode.RefreshRate > 0)
                    displayMode.RefreshRate = SnapRefreshRate(resolution.RefreshRates, displayMode.RefreshRate);
            }

            PruneDisplayModeOverride(slot, monitor);
            RefreshDisplayModeOverrides();

            if (!isLoadingProfile)
                UpdateProfile();
        }

        private string GetRefreshChoiceKey(int slot, DisplayMonitorOption monitor)
        {
            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, false);
            if (displayMode is null || displayMode.RefreshMode != DisplayOverrideMode.Override)
                return OverrideChoice.InheritKey;

            return displayMode.RefreshRate > 0 ? FormatKey(displayMode.RefreshRate) : string.Empty;
        }

        private void SelectRefreshChoice(int slot, DisplayMonitorOption monitor, string? key)
        {
            // a list swap clears the selection on its way, that is not a pick
            if (string.IsNullOrEmpty(key) || key == GetRefreshChoiceKey(slot, monitor))
                return;

            int frequency = 0;
            if (key != OverrideChoice.InheritKey && (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out frequency) || frequency <= 0))
                return;

            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, true);
            if (displayMode is null)
                return;

            if (frequency > 0)
            {
                displayMode.RefreshMode = DisplayOverrideMode.Override;
                displayMode.RefreshRate = frequency;
            }
            else
            {
                displayMode.RefreshMode = DisplayOverrideMode.Inherit;
            }

            PruneDisplayModeOverride(slot, monitor);
            RefreshDisplayModeOverrides();

            if (!isLoadingProfile)
                UpdateProfile();
        }

        private static (int Width, int Height, int Frequency) GetSystemDisplayMode(DisplayMonitorOption monitor)
        {
            return ManagerFactory.multimediaManager.GetSystemDisplayMode(monitor.Id, monitor.CurrentWidth, monitor.CurrentHeight, monitor.CurrentFrequency);
        }

        private string? GetResolutionLockedHint(int slot, DisplayMonitorOption monitor)
        {
            return IsResolutionLocked(slot, monitor) ? Properties.Resources.ProfilesPage_DisplayResolutionLocked : null;
        }

        /// <summary>
        /// Mode a monitor ends up with on one power slot, shown on its collapsed row.
        /// Overridden values come from this profile, the others are inherited.
        /// </summary>
        public sealed record DisplayModeSummary(string Resolution, bool IsResolutionOverride, string RefreshRate, bool IsRefreshOverride);

        // inherited values: default game profile, otherwise the system display mode
        private DisplayModeSummary GetDisplayModeSummary(int slot, DisplayMonitorOption monitor)
        {
            DisplayModeOverride? displayMode = GetDisplayModeOverride(slot, monitor, false);
            ResolvedDisplayMode resolved = ResolveDisplayMode(slot, monitor);
            (int systemWidth, int systemHeight, int systemFrequency) = GetSystemDisplayMode(monitor);

            bool locked = IsResolutionLocked(slot, monitor);
            bool resolutionOverride = !locked && displayMode?.ResolutionMode == DisplayOverrideMode.Override && displayMode.HasResolution;

            int width, height;
            if (resolutionOverride)
                (width, height) = (displayMode!.Width, displayMode.Height);
            else if (locked)
                (width, height) = (monitor.CurrentWidth, monitor.CurrentHeight);
            else if (resolved.HasResolution)
                (width, height) = (resolved.Width, resolved.Height);
            else
                (width, height) = (systemWidth, systemHeight);

            bool refreshOverride = displayMode?.RefreshMode == DisplayOverrideMode.Override && displayMode.RefreshRate > 0;

            int frequency;
            if (refreshOverride)
                frequency = displayMode!.RefreshRate;
            else
            {
                // the apply path rounds to a rate the resolution offers
                int inherited = resolved.HasRefreshRate ? resolved.RefreshRate : systemFrequency;
                frequency = SnapRefreshRate(FindResolution(monitor, width, height)?.RefreshRates ?? [], inherited);
            }

            return new($"{width} x {height}", resolutionOverride, $"{frequency} Hz", refreshOverride);
        }

        private void UpdateDisplayMonitors()
        {
            List<DisplayMonitorOption> monitors = [];

            try
            {
                int position = 0;
                foreach (DesktopScreen screen in ManagerFactory.multimediaManager.GetConnectedMonitors())
                {
                    position++;
                    KnownMonitor? known = ManagerFactory.multimediaManager.GetKnownMonitor(screen.KnownMonitorId);
                    // internal and primary are badges on the monitor rows, not part of the name
                    string label = MonitorIdentity.GetDisplayLabel(screen, known, position);

                    List<DisplayResolutionOption> resolutions = screen.screenResolutions
                        .ToList()
                        .Select(r => new DisplayResolutionOption(r.Width, r.Height,
                            r.Frequencies.Keys.ToList().Select(f => new DisplayRefreshOption(f)).ToList()))
                        .ToList();

                    monitors.Add(new(screen.KnownMonitorId, label, screen.IsPrimary,
                        screen.devMode.dmPelsWidth, screen.devMode.dmPelsHeight, screen.devMode.dmDisplayFrequency, resolutions)
                    {
                        IsInternal = screen.IsInternal,
                    });
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to list monitors: {0}", ex.Message);
            }

            _displayMonitors = monitors;
            OnPropertyChanged(nameof(DisplayMonitors));
            OnPropertyChanged(nameof(HasMultipleDisplays));
            OnPropertyChanged(nameof(HasDisplayMonitors));
            RefreshDisplayModeOverrides();
        }

        private void MultimediaManager_DisplaySettingsChanged(DesktopScreen screen, ScreenResolution resolution)
        {
            // raised while the display list is locked, list the monitors later on the UI thread
            UIHelper.TryBeginInvoke(UpdateDisplayMonitors);
        }

        private void RefreshDisplayModeOverrides()
        {
            // the refresh rate override drives the framerate presets,
            // regenerating them ends with the monitor badges, which refresh the per monitor rows
            RegenerateFramerateLimits();
        }

        /// <summary>
        /// Resolution and refresh rate overrides of one connected monitor on one power slot.
        /// </summary>
        public sealed class DisplayOverrideRow : BaseViewModel
        {
            private readonly ProfilesPageViewModel _owner;
            private readonly OverrideChoiceList _resolutionChoices = new();
            private readonly OverrideChoiceList _refreshChoices = new();
            private bool _isExpanded;

            public DisplayOverrideRow(ProfilesPageViewModel owner, int slot, DisplayMonitorOption monitor, DisplayMonitorItem item)
            {
                _owner = owner;
                Slot = slot;
                Monitor = monitor;
                Item = item;
                ToggleExpandedCommand = new DelegateCommand(() => IsExpanded = !IsExpanded);
            }

            public int Slot { get; }
            public DisplayMonitorOption Monitor { get; private set; }

            // monitor name and badges
            public DisplayMonitorItem Item { get; private set; }
            public string Name => Item.Label;

            public string? BadgeText => Item.BadgeText;

            public ICommand ToggleExpandedCommand { get; }

            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    if (SetProperty(ref _isExpanded, value))
                    {
                        OnPropertyChanged(nameof(ShowSettings));
                        OnPropertyChanged(nameof(ChevronGlyph));
                    }
                }
            }

            // a single monitor shows its rows directly, several get one clickable monitor row each
            public bool HasMultipleDisplays => _owner.HasMultipleDisplays;
            public bool IsSingleDisplay => !_owner.HasMultipleDisplays;
            public bool ShowSettings => IsSingleDisplay || IsExpanded;

            public string ChevronGlyph => GetChevronGlyph(IsExpanded);

            // collapsed row summary: overridden parts emphasized, inherited parts in italic
            public DisplayModeSummary Summary => _owner.GetDisplayModeSummary(Slot, Monitor);

            // integer scaling owns the primary display resolution, the choice stays disabled meanwhile
            public bool IsResolutionEnabled => !_owner.IsResolutionLocked(Slot, Monitor);
            public string? ResolutionHint => _owner.GetResolutionLockedHint(Slot, Monitor);

            public IReadOnlyList<OverrideChoice> ResolutionChoices => _resolutionChoices.Items;

            public string SelectedResolutionKey
            {
                get => _owner.GetResolutionChoiceKey(Slot, Monitor);
                set => _owner.SelectResolutionChoice(Slot, Monitor, value);
            }

            public OverrideChoice? SelectedResolutionChoice
            {
                get => _resolutionChoices.Find(SelectedResolutionKey);
                set => SelectedResolutionKey = value?.Key ?? string.Empty;
            }

            public IReadOnlyList<OverrideChoice> RefreshChoices => _refreshChoices.Items;

            public string SelectedRefreshKey
            {
                get => _owner.GetRefreshChoiceKey(Slot, Monitor);
                set => _owner.SelectRefreshChoice(Slot, Monitor, value);
            }

            public OverrideChoice? SelectedRefreshChoice
            {
                get => _refreshChoices.Find(SelectedRefreshKey);
                set => SelectedRefreshKey = value?.Key ?? string.Empty;
            }

            internal void Update(DisplayMonitorOption monitor, DisplayMonitorItem item)
            {
                Monitor = monitor;
                Item = item;
            }

            internal void Refresh()
            {
                OnPropertyChanged(nameof(Item));
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(BadgeText));
                OnPropertyChanged(nameof(HasMultipleDisplays));
                OnPropertyChanged(nameof(IsSingleDisplay));
                OnPropertyChanged(nameof(ShowSettings));
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(IsResolutionEnabled));
                OnPropertyChanged(nameof(ResolutionHint));

                // option lists before their selections
                if (_resolutionChoices.Update(_owner.GetResolutionInheritLabel(Slot, Monitor), GetResolutionValueChoices(Monitor),
                    _owner.GetResolutionChoiceKey(Slot, Monitor), () => _owner.GetStoredResolutionLabel(Slot, Monitor)))
                    _resolutionChoices.Publish(OnPropertyChanged, nameof(ResolutionChoices), nameof(SelectedResolutionChoice));
                else
                    OnPropertyChanged(nameof(SelectedResolutionChoice));
                OnPropertyChanged(nameof(SelectedResolutionKey));

                if (_refreshChoices.Update(_owner.GetRefreshInheritLabel(Slot, Monitor), _owner.GetRefreshValueChoices(Slot, Monitor),
                    _owner.GetRefreshChoiceKey(Slot, Monitor), () => _owner.GetStoredRefreshLabel(Slot, Monitor)))
                    _refreshChoices.Publish(OnPropertyChanged, nameof(RefreshChoices), nameof(SelectedRefreshChoice));
                else
                    OnPropertyChanged(nameof(SelectedRefreshChoice));
                OnPropertyChanged(nameof(SelectedRefreshKey));
            }
        }
        #endregion

        #region Graphics overrides
        // per power slot, each graphics group is a separate Inherit/Override field
        public enum GraphicsGroup
        {
            Scaling,
            RSR,
            IntegerScaling,
            RIS,
            AFMF,
        }

        // combo keys of the on and off entries, scaling modes and integer scaling dividers use their value
        private const string GraphicsOnKey = "on";
        private const string GraphicsOffKey = "off";

        // the default profile is the graphics baseline, its groups always apply and have no mode to pick
        private bool IsGraphicsBaseline => SelectedProfile?.Default == true;

        // one row per graphics group and power slot
        public IReadOnlyList<GraphicsOverrideRow> GraphicsOverrideRowsDC { get; private set; } = [];
        public IReadOnlyList<GraphicsOverrideRow> GraphicsOverrideRowsAC { get; private set; } = [];

        private void InitializeGraphicsOverrideRows()
        {
            GraphicsOverrideRowsDC = CreateGraphicsOverrideRows(FramerateSlotDC);
            GraphicsOverrideRowsAC = CreateGraphicsOverrideRows(FramerateSlotAC);
        }

        private IReadOnlyList<GraphicsOverrideRow> CreateGraphicsOverrideRows(int slot)
        {
            return
            [
                new(this, slot, GraphicsGroup.Scaling, Properties.Resources.ProfilesPage_GPUScaling),
                new(this, slot, GraphicsGroup.RSR, Properties.Resources.ProfilesPage_RSR),
                new(this, slot, GraphicsGroup.IntegerScaling, Properties.Resources.ProfilesPage_IS),
                new(this, slot, GraphicsGroup.RIS, Properties.Resources.ProfilesPage_ImageSharpening),
                new(this, slot, GraphicsGroup.AFMF, Properties.Resources.ProfilesPage_AFMF),
            ];
        }

        private IEnumerable<GraphicsOverrideRow> AllGraphicsOverrideRows => GraphicsOverrideRowsDC.Concat(GraphicsOverrideRowsAC);

        private void RefreshGraphicsOverrideRows()
        {
            foreach (GraphicsOverrideRow row in AllGraphicsOverrideRows)
                row.Refresh();
        }

        private GraphicsGroupOverride? GetGraphicsGroup(int slot, GraphicsGroup group, bool create)
        {
            GraphicsOverrides? graphics = GetPerformanceOverrides(slot, create)?.Graphics;
            return group switch
            {
                GraphicsGroup.Scaling => graphics?.Scaling,
                GraphicsGroup.RSR => graphics?.RSR,
                GraphicsGroup.IntegerScaling => graphics?.IntegerScaling,
                GraphicsGroup.RIS => graphics?.RIS,
                GraphicsGroup.AFMF => graphics?.AFMF,
                _ => null,
            };
        }

        private ResolvedGraphics ResolveGraphics(int slot)
        {
            if (SelectedProfile is null)
                return default;

            return ManagerFactory.powerProfileManager.ResolveGraphics(SelectedProfile, slot);
        }

        // values the default profile gives a game profile; the default profile is the baseline and inherits nothing
        private GraphicsGroupOverride? GetInheritedGraphicsValues(int slot, GraphicsGroup group)
        {
            if (SelectedProfile is null || IsGraphicsBaseline)
                return null;

            return GetResolvedGraphicsValues(ManagerFactory.powerProfileManager.ResolveGraphics(null, slot), group);
        }

        private string GetGraphicsChoiceKey(int slot, GraphicsGroup group)
        {
            GraphicsGroupOverride? values = GetGraphicsGroup(slot, group, false);
            if (!IsGraphicsBaseline && values?.IsOverride != true)
                return OverrideChoice.InheritKey;

            if (values is null)
                return string.Empty;

            if (!values.Enabled)
                return GraphicsOffKey;

            return values switch
            {
                ScalingOverride scaling => FormatKey(scaling.ScalingMode),
                IntegerScalingOverride integerScaling => FormatKey(integerScaling.Divider),
                _ => GraphicsOnKey,
            };
        }

        private void SelectGraphicsChoice(int slot, GraphicsGroup group, string? key)
        {
            // a list swap clears the selection on its way, that is not a pick
            if (string.IsNullOrEmpty(key) || key == GetGraphicsChoiceKey(slot, group))
                return;

            if (key == OverrideChoice.InheritKey)
            {
                if (!IsGraphicsBaseline)
                    SetGraphicsValue<GraphicsGroupOverride>(slot, group, g => g.Mode = GraphicsOverrideMode.Inherit);
                return;
            }

            GraphicsGroupOverride? inherited = GetInheritedGraphicsValues(slot, group);

            SetGraphicsValue<GraphicsGroupOverride>(slot, group, g =>
            {
                // a new override starts from what the slot inherits, so the values the combo doesn't pick carry over
                if (!g.IsOverride && inherited is not null)
                    g.CopyValuesFrom(inherited);

                g.Mode = GraphicsOverrideMode.Override;
                g.Enabled = key != GraphicsOffKey;

                if (g.Enabled && int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    switch (g)
                    {
                        case ScalingOverride scaling:
                            scaling.ScalingMode = value;
                            break;
                        case IntegerScalingOverride integerScaling:
                            integerScaling.Divider = value;
                            break;
                    }
                }
            });
        }

        private static string GetScalingModeLabel(int scalingMode)
        {
            return scalingMode switch
            {
                0 => Properties.Resources.ProfilesPage_ScalingMode_PreserveAspectRatio,
                1 => Properties.Resources.ProfilesPage_ScalingMode_FullPanel,
                2 => Properties.Resources.ProfilesPage_ScalingMode_Center,
                _ => FormatKey(scalingMode),
            };
        }

        // short divider form for the Inherit and not available entries, the list entries keep the resulting resolution
        private static string GetDividerLabel(int divider) => $"1/{divider}";

        // dividers of the primary display, integer scaling only drives that one
        private List<ScreenDividerViewModel> GetIntegerScalingDividers()
        {
            lock (_collectionLock6)
                return IntegerScalingDividers.ToList();
        }

        private string GetGraphicsValueLabel(GraphicsGroupOverride values)
        {
            if (!values.Enabled)
                return Properties.Resources.ProfilesPage_GraphicsOff;

            return values switch
            {
                ScalingOverride scaling => GetScalingModeLabel(scaling.ScalingMode),
                IntegerScalingOverride integerScaling => GetDividerLabel(integerScaling.Divider),
                RSROverride rsr => string.Format(Properties.Resources.ProfilesPage_GraphicsOnPercent, rsr.Sharpness),
                RISOverride ris => string.Format(Properties.Resources.ProfilesPage_GraphicsOnPercent, ris.Sharpness),
                _ => Properties.Resources.ProfilesPage_GraphicsOn,
            };
        }

        private string GetStoredGraphicsLabel(int slot, GraphicsGroup group)
        {
            GraphicsGroupOverride? values = GetGraphicsGroup(slot, group, false);
            return values is null ? string.Empty : GetGraphicsValueLabel(values);
        }

        // no Inherit entry on the default profile, the graphics baseline
        private string? GetGraphicsInheritLabel(int slot, GraphicsGroup group)
        {
            if (IsGraphicsBaseline)
                return null;

            GraphicsGroupOverride? inherited = GetInheritedGraphicsValues(slot, group);
            return string.Format(Properties.Resources.OverrideMode_UseDefaultProfile,
                inherited is null ? Properties.Resources.ProfilesPage_GraphicsOff : GetGraphicsValueLabel(inherited));
        }

        private IEnumerable<(string Key, string Label, bool IsEnabled)> GetGraphicsValueChoices(int slot, GraphicsGroup group)
        {
            (string, string, bool) off = (GraphicsOffKey, Properties.Resources.ProfilesPage_GraphicsOff, true);

            switch (group)
            {
                case GraphicsGroup.Scaling:
                    // center scaling doesn't work with RSR
                    bool centerEnabled = !ResolveGraphics(slot).RSR.IsEnabled;
                    return
                    [
                        off,
                        (FormatKey(0), GetScalingModeLabel(0), true),
                        (FormatKey(1), GetScalingModeLabel(1), true),
                        (FormatKey(2), GetScalingModeLabel(2), centerEnabled),
                    ];

                case GraphicsGroup.IntegerScaling:
                    return new[] { off }.Concat(GetIntegerScalingDividers().Select(d => (FormatKey(d.Divider), d.ToString(), true)));

                default:
                    return
                    [
                        (GraphicsOnKey, Properties.Resources.ProfilesPage_GraphicsOn, true),
                        off,
                    ];
            }
        }

        // effective values of one group, null when no layer sets it
        private static GraphicsGroupOverride? GetResolvedGraphicsValues(ResolvedGraphics resolved, GraphicsGroup group)
        {
            return group switch
            {
                GraphicsGroup.Scaling => resolved.Scaling.IsSet ? resolved.Scaling.Values : null,
                GraphicsGroup.RSR => resolved.RSR.IsSet ? resolved.RSR.Values : null,
                GraphicsGroup.IntegerScaling => resolved.IntegerScaling.IsSet ? resolved.IntegerScaling.Values : null,
                GraphicsGroup.RIS => resolved.RIS.IsSet ? resolved.RIS.Values : null,
                GraphicsGroup.AFMF => resolved.AFMF.IsSet ? resolved.AFMF.Values : null,
                _ => null,
            };
        }

        private void SetGraphicsValue<T>(int slot, GraphicsGroup group, Action<T> apply) where T : GraphicsGroupOverride
        {
            if (isLoadingProfile || SelectedProfile is null)
                return;

            if (GetGraphicsGroup(slot, group, true) is not T values)
                return;

            apply(values);

            if (IsGraphicsBaseline)
                values.Mode = GraphicsOverrideMode.Override;

            // scaling gates RSR and integer scaling, integer scaling locks the primary display resolution
            RefreshGraphicsOverrideRows();
            if (group is GraphicsGroup.Scaling or GraphicsGroup.IntegerScaling)
                RefreshDisplayModeOverrides();

            UpdateProfile();
        }

        // same capability gating as before the move to power slots
        private bool IsGraphicsGroupVisible(GraphicsGroup group)
        {
            return group switch
            {
                GraphicsGroup.RSR or GraphicsGroup.AFMF => IsAMDGPU,
                _ => true,
            };
        }

        private bool IsGraphicsGroupSupported(int slot, GraphicsGroup group)
        {
            return group switch
            {
                GraphicsGroup.Scaling or GraphicsGroup.RIS => HasGPUScalingSupport,
                GraphicsGroup.RSR => HasRSRSupport && NeedsGPUScalingMet(slot),
                GraphicsGroup.IntegerScaling => HasIntegerScalingSupport && NeedsGPUScalingMet(slot),
                GraphicsGroup.AFMF => HasAFMFSupport,
                _ => false,
            };
        }

        // RSR and integer scaling need GPU scaling on the same power slot
        private bool NeedsGPUScalingMet(int slot)
        {
            return ResolveGraphics(slot).Scaling.IsEnabled;
        }

        // the value sits in the combo, the hint only explains why a group can't apply
        private string? GetGraphicsHint(int slot, GraphicsGroup group)
        {
            if (SelectedProfile is null)
                return null;

            if ((group is GraphicsGroup.RSR or GraphicsGroup.IntegerScaling) && !NeedsGPUScalingMet(slot))
                return Properties.Resources.ProfilesPage_GraphicsNeedsGPUScaling;

            return null;
        }

        /// <summary>
        /// One graphics group on one power slot: one choice list (Inherit, Off, On or the group's values), and its value rows while overridden and on.
        /// </summary>
        public sealed class GraphicsOverrideRow : BaseViewModel
        {
            private readonly ProfilesPageViewModel _owner;
            private readonly OverrideChoiceList _choices = new();

            public GraphicsOverrideRow(ProfilesPageViewModel owner, int slot, GraphicsGroup group, string header)
            {
                _owner = owner;
                Slot = slot;
                Group = group;
                Header = header;
            }

            public int Slot { get; }
            public GraphicsGroup Group { get; }
            public string Header { get; }

            public bool IsVisible => _owner.IsGraphicsGroupVisible(Group);
            public bool IsSupported => _owner.IsGraphicsGroupSupported(Slot, Group);
            public string? Hint => _owner.GetGraphicsHint(Slot, Group);

            public IReadOnlyList<OverrideChoice> Choices => _choices.Items;

            public string SelectedKey
            {
                get => _owner.GetGraphicsChoiceKey(Slot, Group);
                set => _owner.SelectGraphicsChoice(Slot, Group, value);
            }

            // bound through SelectedItem: a replaced list hands the combo a new entry, so it always selects again
            public OverrideChoice? SelectedChoice
            {
                get => _choices.Find(SelectedKey);
                set => SelectedKey = value?.Key ?? string.Empty;
            }

            private GraphicsGroupOverride? Values => _owner.GetGraphicsGroup(Slot, Group, false);

            // value rows edit this layer's values: shown while it overrides the group and turns it on
            public bool ShowValues => Values is { Enabled: true } values && (_owner.IsGraphicsBaseline || values.IsOverride);
            public bool ShowSharpness => ShowValues && (Group is GraphicsGroup.RSR or GraphicsGroup.RIS);
            public bool ShowIntegerScalingType => ShowValues && Group == GraphicsGroup.IntegerScaling;
            public bool ShowAFMF21 => ShowValues && Group == GraphicsGroup.AFMF && _owner.HasAFMF21Support;

            public string SharpnessHeader => Group == GraphicsGroup.RSR
                ? Properties.Resources.ProfilesPage_ResolutionScale
                : Properties.Resources.ProfilesPage_SharpeningScale;

            public double Sharpness
            {
                get => Values switch
                {
                    RSROverride rsr => rsr.Sharpness,
                    RISOverride ris => ris.Sharpness,
                    _ => 0,
                };
                set
                {
                    if (Group == GraphicsGroup.RSR)
                        _owner.SetGraphicsValue<RSROverride>(Slot, Group, g => g.Sharpness = (int)value);
                    else
                        _owner.SetGraphicsValue<RISOverride>(Slot, Group, g => g.Sharpness = (int)value);
                }
            }

            public int IntegerScalingType
            {
                get => (Values as IntegerScalingOverride)?.Type ?? 0;
                set => _owner.SetGraphicsValue<IntegerScalingOverride>(Slot, Group, g => g.Type = (byte)value);
            }

            public int AFMFAlgorithm
            {
                get => (Values as AFMFOverride)?.Algorithm ?? 0;
                set => _owner.SetGraphicsValue<AFMFOverride>(Slot, Group, g => g.Algorithm = value);
            }

            public int AFMFSearchMode
            {
                get => (Values as AFMFOverride)?.SearchMode ?? 0;
                set => _owner.SetGraphicsValue<AFMFOverride>(Slot, Group, g => g.SearchMode = value);
            }

            public int AFMFPerformanceMode
            {
                get => (Values as AFMFOverride)?.PerformanceMode ?? 0;
                set => _owner.SetGraphicsValue<AFMFOverride>(Slot, Group, g => g.PerformanceMode = value);
            }

            public int AFMFFastMotionResponse
            {
                get => (Values as AFMFOverride)?.FastMotionResponse ?? 0;
                set => _owner.SetGraphicsValue<AFMFOverride>(Slot, Group, g => g.FastMotionResponse = value);
            }

            internal void Refresh()
            {
                OnPropertyChanged(nameof(IsVisible));
                OnPropertyChanged(nameof(IsSupported));
                OnPropertyChanged(nameof(Hint));

                // option list before its selection
                if (_choices.Update(_owner.GetGraphicsInheritLabel(Slot, Group), _owner.GetGraphicsValueChoices(Slot, Group),
                    _owner.GetGraphicsChoiceKey(Slot, Group), () => _owner.GetStoredGraphicsLabel(Slot, Group)))
                    _choices.Publish(OnPropertyChanged, nameof(Choices), nameof(SelectedChoice));
                else
                    OnPropertyChanged(nameof(SelectedChoice));
                OnPropertyChanged(nameof(SelectedKey));

                OnPropertyChanged(nameof(ShowValues));
                OnPropertyChanged(nameof(ShowSharpness));
                OnPropertyChanged(nameof(ShowIntegerScalingType));
                OnPropertyChanged(nameof(ShowAFMF21));
                OnPropertyChanged(nameof(Sharpness));
                OnPropertyChanged(nameof(IntegerScalingType));
                OnPropertyChanged(nameof(AFMFAlgorithm));
                OnPropertyChanged(nameof(AFMFSearchMode));
                OnPropertyChanged(nameof(AFMFPerformanceMode));
                OnPropertyChanged(nameof(AFMFFastMotionResponse));
            }
        }
        #endregion

        #region Process Control
        private ProcessExViewModel? _CurrentProcessViewModel;
        /// <summary>
        /// Tracks the currently running process for the selected profile (ProfilesPage) or foreground app (QuickProfilesPage).
        /// 
        /// Setting this property triggers notifications for:
        /// - Can* properties (CanLaunchProcess, CanSuspendProcess, etc.)
        /// - Command properties (SuspendProcessCommand, ResumeProcessCommand, KillProcessCommand)
        /// 
        /// This ensures MenuItem bindings update correctly.
        /// </summary>
        public ProcessExViewModel? CurrentProcessViewModel
        {
            get => _CurrentProcessViewModel;
            private set
            {
                if (_CurrentProcessViewModel != value)
                {
                    _CurrentProcessViewModel = value;

                    // Subscribe to new ViewModel
                    _CurrentProcessViewModel?.PropertyChanged += CurrentProcessViewModel_PropertyChanged;

                    OnPropertyChanged(nameof(CurrentProcessViewModel));

                    // Notify that the Can* properties changed
                    OnPropertyChanged(nameof(CanLaunchProcess));
                    OnPropertyChanged(nameof(CanSuspendProcess));
                    OnPropertyChanged(nameof(CanResumeProcess));
                    OnPropertyChanged(nameof(CanKillProcess));
                    OnPropertyChanged(nameof(IsProfileProcessRunning));
                    OnPropertyChanged(nameof(IsProfileProcessSuspended));
                    OnPropertyChanged(nameof(CanToggleProfileProcess));
                    OnPropertyChanged(nameof(ProfileProcessActionText));
                    OnPropertyChanged(nameof(ProfileProcessActionGlyph));

                    OnPropertyChanged(nameof(KillProcessCommand));
                }
            }
        }
        #endregion

        private void CurrentProcessViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProcessExViewModel.IsRunning):
                    OnPropertyChanged(nameof(CanLaunchProcess));
                    OnPropertyChanged(nameof(CanKillProcess));
                    OnPropertyChanged(nameof(IsProfileProcessRunning));
                    OnPropertyChanged(nameof(CanToggleProfileProcess));
                    OnPropertyChanged(nameof(ProfileProcessActionText));
                    OnPropertyChanged(nameof(ProfileProcessActionGlyph));
                    break;
                case nameof(ProcessExViewModel.IsSuspended):
                    OnPropertyChanged(nameof(CanSuspendProcess));
                    OnPropertyChanged(nameof(CanResumeProcess));
                    OnPropertyChanged(nameof(IsProfileProcessSuspended));
                    OnPropertyChanged(nameof(ProfileProcessActionText));
                    OnPropertyChanged(nameof(ProfileProcessActionGlyph));
                    break;
            }
        }

        public bool IsProfileProcessRunning => CurrentProcessViewModel?.IsRunning == true;
        public bool IsProfileProcessSuspended => CurrentProcessViewModel?.IsSuspended == true;
        public bool CanToggleProfileProcess => CanLaunchProcess || CanKillProcess;
        public bool CanLaunchProcess => SelectedProfile != null && !string.IsNullOrEmpty(SelectedProfile.Path) && CurrentProcessViewModel?.IsRunning != true;
        public bool CanSuspendProcess => CurrentProcessViewModel?.CanSuspend == true;
        public bool CanResumeProcess => CurrentProcessViewModel?.CanResume == true;
        public bool CanKillProcess => CurrentProcessViewModel?.IsRunning == true;

        public string ProfileProcessActionText
        {
            get
            {
                if (!IsProfileProcessRunning)
                    return Properties.Resources.ProfilesPage_Play;

                if (IsProfileProcessSuspended)
                    return Properties.Resources.ProfilesPage_ResumeProcess;

                return Properties.Resources.ProfilesPage_SuspendProcess;
            }
        }

        public string ProfileProcessActionGlyph
        {
            get
            {
                // Glyph codes: Play=F5B0, Pause=E769, Resume=E768
                if (!IsProfileProcessRunning)
                    return "\uF5B0";  // Play glyph (launch)

                if (IsProfileProcessSuspended)
                    return "\uE768";  // Resume glyph (play)

                return "\uE769";      // Pause glyph (suspend)
            }
        }

        // Delegate process commands to CurrentProcessViewModel, except Launch which uses Profile
        public ICommand? SuspendProcessCommand => CurrentProcessViewModel?.SuspendProcessCommand;
        public ICommand? ResumeProcessCommand => CurrentProcessViewModel?.ResumeProcessCommand;
        public ICommand? KillProcessCommand => CurrentProcessViewModel?.KillProcessCommand;

        // Events for View to handle UI-specific operations
        public event EventHandler? RequestCreateProfile;
        public event EventHandler<Profile>? RequestDeleteProfile;
        public event EventHandler<Profile>? RequestRenameProfile;
        public event EventHandler? RequestCreateSubProfile;
        public event EventHandler<Profile>? RequestDeleteSubProfile;
        public event EventHandler<Profile>? RequestRenameSubProfile;
        public event EventHandler<LayoutTemplate>? RequestOpenControllerLayout;
        public event EventHandler<PowerProfile>? RequestOpenPowerProfile;
        public event EventHandler? RequestOpenProfilePage;
        public event EventHandler? RequestOpenProfileLayout;
        public event EventHandler? RequestCreatePowerProfile;
        public event EventHandler? RequestOpenAdditionalSettings;
        public event EventHandler? RequestShowLibraryDialog;

        // Commands
        public ICommand RefreshLibrary { get; private set; } = null!;
        public ICommand DisplayLibrary { get; private set; } = null!;
        public ICommand DownloadLibrary { get; private set; } = null!;
        public ICommand LaunchExecutable { get; private set; } = null!;
        public ICommand AddProfileExecutable { get; private set; } = null!;
        public ICommand RemoveProfileExecutable { get; private set; } = null!;
        public ICommand CreateProfileCommand { get; private set; } = null!;
        public ICommand DeleteProfileCommand { get; private set; } = null!;
        public ICommand RenameProfileCommand { get; private set; } = null!;
        public ICommand ToggleFavoriteCommand { get; private set; } = null!;
        public ICommand CreateSubProfileCommand { get; private set; } = null!;
        public ICommand DeleteSubProfileCommand { get; private set; } = null!;
        public ICommand RenameSubProfileCommand { get; private set; } = null!;
        public ICommand OpenControllerLayoutCommand { get; private set; } = null!;
        public ICommand OpenPowerProfileOnBatteryCommand { get; private set; } = null!;
        public ICommand OpenPowerProfilePluggedCommand { get; private set; } = null!;
        public ICommand OpenProfilePageCommand { get; private set; } = null!;
        public ICommand OpenProfileLayoutCommand { get; private set; } = null!;
        public ICommand CreatePowerProfileCommand { get; private set; } = null!;
        public ICommand ShowCreateProfileFlyoutCommand { get; private set; } = null!;
        public ICommand OpenAdditionalSettingsCommand { get; private set; } = null!;
        public ICommand BrowseCoverCommand { get; private set; } = null!;
        public ICommand BrowseArtworkCommand { get; private set; } = null!;
        public ICommand BrowseLogoCommand { get; private set; } = null!;

        // Profile commands
        public ICommand LaunchProfileProcessCommand { get; private set; } = null!;
        public ICommand SuspendProfileProcessCommand { get; private set; } = null!;
        public ICommand ResumeProfileProcessCommand { get; private set; } = null!;
        public ICommand KillProfileProcessCommand { get; private set; } = null!;
        public ICommand ToggleProfileProcessCommand { get; private set; } = null!;

        private int loadingProfileDepth;
        private bool isLoadingProfile => Volatile.Read(ref loadingProfileDepth) > 0;

        /// <summary>
        /// Helper class to manage isLoadingProfile flag with automatic cleanup.
        /// Usage: using (new LoadingScope(this)) { ... }
        /// </summary>
        private class LoadingScope : IDisposable
        {
            private readonly ProfilesPageViewModel _viewModel;

            public LoadingScope(ProfilesPageViewModel viewModel)
            {
                _viewModel = viewModel;
                Interlocked.Increment(ref _viewModel.loadingProfileDepth);
            }

            public void Dispose()
            {
                Interlocked.Decrement(ref _viewModel.loadingProfileDepth);
            }
        }

        private bool SetSelectedProfile(Profile value, bool updateProfileContext, bool forceNotification = false)
        {
            if (_selectedProfile == value && !forceNotification)
                return false;

            _selectedProfile = value;

            OnPropertyChanged(nameof(SelectedProfile));
            OnPropertyChanged(nameof(HasSelectedProfile));
            OnPropertyChanged(nameof(CanLaunchProcess));
            OnPropertyChanged(nameof(CanKillProcess));
            OnPropertyChanged(nameof(IsProfileProcessRunning));
            OnPropertyChanged(nameof(CanToggleProfileProcess));
            OnPropertyChanged(nameof(IsProfileEnabledToggleEnabled));
            OnPropertyChanged(nameof(IsControllerPassthroughEnabled));
            OnPropertyChanged(nameof(SelectedSubProfileViewModel));
            OnPropertyChanged(nameof(CanEditHIDMode));
            OnPropertyChanged(nameof(HasPrebuiltShaderDownload));
            OnPropertyChanged(nameof(PrebuiltShaderDownloadEnabled));
            OnPropertyChanged(nameof(Cover));
            OnPropertyChanged(nameof(Artwork));
            OnPropertyChanged(nameof(Logo));

            if (updateProfileContext)
                OnProfileChanged();

            return true;
        }

        private bool SetSelectedMainProfile(Profile value, bool updateSubProfiles, bool forceNotification = false)
        {
            if (_selectedMainProfile == value && !forceNotification)
                return false;

            _selectedMainProfile = value;
            OnPropertyChanged(nameof(SelectedMainProfile));
            OnPropertyChanged(nameof(SelectedMainProfileViewModel));
            OnPropertyChanged(nameof(IsProfileManagementEnabled));
            OnPropertyChanged(nameof(IsLibrarySettingsEnabled));
            OnPropertyChanged(nameof(CanLaunchProcess));
            OnPropertyChanged(nameof(CanKillProcess));
            OnPropertyChanged(nameof(IsProfileProcessRunning));
            OnPropertyChanged(nameof(CanToggleProfileProcess));

            if (updateSubProfiles)
                UpdateSubProfiles();

            return true;
        }

        /// <summary>
        /// Safely updates SubProfiles collection without triggering WPF binding side effects.
        /// Sets temporary selection to prevent SelectedProfile from being auto-nulled.
        /// </summary>
        private void SafeUpdateSubProfiles(IEnumerable<Profile> newProfiles, Profile profileToSelect)
        {
            foreach (var vm in SubProfiles)
                vm.Dispose();
            SubProfiles.Clear();
            foreach (var profile in newProfiles)
                SubProfiles.Add(new ProfileViewModel(profile, IsQuickTools));

            if (_selectedProfile?.Guid == profileToSelect.Guid)
                OnPropertyChanged(nameof(SelectedSubProfileViewModel));
        }

        public ProfilesPageViewModel(ProfilesPage profilesPage)
        {
            this.profilesPage = profilesPage;
            IsQuickTools = false;
            InitializeCommon();
            InitializePageSpecific();
        }

        public ProfilesPageViewModel(QuickProfilesPage quickProfilesPage)
        {
            this.quickProfilesPage = quickProfilesPage;
            IsQuickTools = true;
            InitializeCommon();
            InitializePageSpecific();
        }

        private void InitializeCommon()
        {
            InitializeFramerateOverrideRows();
            InitializeGraphicsOverrideRows();

            GyroHotkey = new(gyroButtonFlags) { IsInternal = true, Name = "HOTKEY_GYRO_ACTIVATION_QP" };
            ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(GyroHotkey);

            UpdateTimer = new Timer(UpdateInterval) { AutoReset = false };
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;

            BindingOperations.EnableCollectionSynchronization(ProfilePicker, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(LibraryPickers, _collectionLock2);
            BindingOperations.EnableCollectionSynchronization(ProfileExecutables, _collectionLock3);
            BindingOperations.EnableCollectionSynchronization(HotkeysList, _collectionLock4);
            BindingOperations.EnableCollectionSynchronization(MainProfiles, _collectionLock5);
            BindingOperations.EnableCollectionSynchronization(SubProfiles, _collectionLock5);
            BindingOperations.EnableCollectionSynchronization(IntegerScalingDividers, _collectionLock6);
            BindingOperations.EnableCollectionSynchronization(AllWindows, _collectionLock7);

            ProfilePickerCollectionViewDC = new ListCollectionView(ProfilePicker);
            ProfilePickerCollectionViewDC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));
            ProfilePickerCollectionViewAC = new ListCollectionView(ProfilePicker);
            ProfilePickerCollectionViewAC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));

            MainProfilesView = new ListCollectionView(MainProfiles);
            MainProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.Name), ListSortDirection.Ascending));

            SubProfilesView = new ListCollectionView(SubProfiles);
            SubProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.SortOrder), ListSortDirection.Ascending));
            SubProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.Name), ListSortDirection.Ascending));

            // Initialize MotionOutput modes
            foreach (var mode in Enum.GetValues<MotionOutput>())
            {
                MotionOutputModes.Add(new MotionOutputViewModel(mode));
            }

            // Initialize MotionInput modes
            foreach (var mode in (MotionInput[])Enum.GetValues(typeof(MotionInput)))
            {
                MotionInputModes.Add(new MotionInputViewModel(mode));
            }

            ProfileExecutables.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(HasProfileExecutables));
            };

            SetupCommands();
            SetupManagerEvents();
        }

        private void InitializePageSpecific()
        {
            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfile();
                    break;
            }

            if (IsQuickTools)
            {
                ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
                ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;
                InputsManager.StartedListening += InputsManager_StartedListening;
                InputsManager.StoppedListening += InputsManager_StoppedListening;

                switch (ManagerFactory.hotkeysManager.Status)
                {
                    default:
                    case ManagerStatus.Initializing:
                        ManagerFactory.hotkeysManager.Initialized += HotkeysManager_Initialized;
                        break;
                    case ManagerStatus.Initialized:
                        QueryGyroHotkey();
                        break;
                }
            }
            else
            {
                switch (ManagerFactory.libraryManager.Status)
                {
                    default:
                    case ManagerStatus.Initializing:
                        ManagerFactory.libraryManager.Initialized += LibraryManager_Initialized;
                        break;
                    case ManagerStatus.Initialized:
                        QueryLibrary();
                        break;
                }

                switch (ManagerFactory.processManager.Status)
                {
                    default:
                    case ManagerStatus.Initializing:
                        ManagerFactory.processManager.Initialized += ProcessManager_Initialized_Main;
                        break;
                    case ManagerStatus.Initialized:
                        QueryForeground_Main();
                        break;
                }

                ManagerFactory.processManager.ProcessStarted += ProcessManager_ProcessStarted;
                ManagerFactory.processManager.ProcessStopped += ProcessManager_ProcessStopped;
            }
        }

        private void SetupCommands()
        {
            LaunchExecutable = new DelegateCommand<object>(async param =>
            {
                bool runAsAdmin = Convert.ToBoolean(param);
                ProfileViewModel profileViewModel = new(SelectedProfile, false);
                profileViewModel.StartProcessCommand?.Execute(runAsAdmin);
            });

            LaunchProfileProcessCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile is null || string.IsNullOrEmpty(SelectedProfile.Path))
                    return;

                try
                {
                    ProfileViewModel profileViewModel = new(SelectedProfile, false);
                    profileViewModel.StartProcessCommand?.Execute(false);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to launch profile process: {0}", ex.Message);
                }
            });

            SuspendProfileProcessCommand = new DelegateCommand(() =>
            {
                try
                {
                    CurrentProcessViewModel?.SuspendProcessCommand?.Execute(null);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to suspend profile process: {0}", ex.Message);
                }
            });

            ResumeProfileProcessCommand = new DelegateCommand(() =>
            {
                try
                {
                    CurrentProcessViewModel?.ResumeProcessCommand?.Execute(null);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to resume profile process: {0}", ex.Message);
                }
            });

            KillProfileProcessCommand = new DelegateCommand(() =>
            {
                try
                {
                    CurrentProcessViewModel?.KillProcessCommand?.Execute(null);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to kill profile process: {0}", ex.Message);
                }
            });

            ToggleProfileProcessCommand = new DelegateCommand(() =>
            {
                try
                {
                    if (CanKillProcess)
                    {
                        KillProcessCommand?.Execute(null);
                        return;
                    }

                    if (CanLaunchProcess)
                        LaunchProfileProcessCommand?.Execute(null);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to toggle profile process: {0}", ex.Message);
                }
            });

            DisplayLibrary = new DelegateCommand(async () =>
            {
                _libraryTargetProfile = SelectedProfile;
                if (_libraryTargetProfile is null)
                    return;

                RequestShowLibraryDialog?.Invoke(this, EventArgs.Empty);
                RefreshLibrary.Execute(null);
            });

            RefreshLibrary = new DelegateCommand(async () =>
            {
                ClearLibrary();

                Profile? targetProfile = _libraryTargetProfile ?? SelectedProfile;
                LibraryEntry? savedEntry = targetProfile?.LibraryEntry;

                List<LibraryEntry> entries = (await ManagerFactory.libraryManager.GetGames(
                    (QuerySteamGrid ? LibraryFamily.SteamGrid : LibraryFamily.None) | (QueryIGDB ? LibraryFamily.IGDB : LibraryFamily.None),
                    LibrarySearchField)).OrderBy(entry => entry.Name).ToList();

                if (entries.Count != 0)
                {
                    entries = entries.OrderByDescending(entry => entry.Family).ThenBy(entry => entry.Name).ToList();

                    if (!Monitor.TryEnter(_collectionLock2, TimeSpan.FromSeconds(2)))
                        return;

                    try
                    {
                        foreach (LibraryEntry entry in entries)
                        {
                            if (savedEntry is not null && entry.Id == savedEntry.Id && entry.Family == savedEntry.Family)
                            {
                                entry.ManualCoverPath = savedEntry.ManualCoverPath;
                                entry.ManualArtworkPath = savedEntry.ManualArtworkPath;
                                entry.ManualLogoPath = savedEntry.ManualLogoPath;

                                if (entry is SteamGridEntry steamEntry && savedEntry is SteamGridEntry savedSteamEntry)
                                {
                                    steamEntry.Grid = savedSteamEntry.Grid;
                                    steamEntry.Hero = savedSteamEntry.Hero;
                                    steamEntry.Logo = savedSteamEntry.Logo;
                                }
                                else if (entry is IGDBEntry igdbEntry && savedEntry is IGDBEntry savedIGDBEntry)
                                {
                                    igdbEntry.Cover = savedIGDBEntry.Cover;
                                    igdbEntry.Artwork = savedIGDBEntry.Artwork;
                                }
                            }

                            LibraryPickers.Add(new(entry));
                        }
                    }
                    finally
                    {
                        Monitor.Exit(_collectionLock2);
                    }

                    LibraryEntry? restoredEntry = savedEntry is null
                        ? null
                        : entries.FirstOrDefault(entry => entry.Id == savedEntry.Id && entry.Family == savedEntry.Family);
                    if (restoredEntry is not null)
                        SelectedLibraryEntry = restoredEntry;
                    else
                        SelectedLibraryEntry = ManagerFactory.libraryManager.GetGame(entries, LibrarySearchField);
                }
                else
                {
                    // No online results  select manual
                    SelectedLibraryEntry = null;
                }

                // Notify that library entries are now available
                OnPropertyChanged(nameof(HasLibraryEntry));
                OnPropertyChanged(nameof(CanApplyLibrary));
            });

            DownloadLibrary = new DelegateCommand(async () =>
            {
                Profile? targetProfile = _libraryTargetProfile ?? SelectedProfile;
                if (targetProfile is null)
                    return;

                int coverId = (int)(LibraryCoversIndex != -1 && LibraryCoversIndex < LibraryCovers.Count ? LibraryCovers[LibraryCoversIndex].Id : 0);
                int artworkId = (int)(LibraryArtworksIndex != -1 && LibraryArtworksIndex < LibraryArtworks.Count ? LibraryArtworks[LibraryArtworksIndex].Id : 0);
                int logoId = (int)(LibraryLogosIndex != -1 && LibraryLogosIndex < LibraryLogos.Count ? LibraryLogos[LibraryLogosIndex].Id : 0);

                if (SelectedLibraryEntry is null)
                    return;

                await ManagerFactory.libraryManager.UpdateProfileArts(targetProfile, SelectedLibraryEntry, coverId, artworkId, logoId);
                ManagerFactory.profileManager.UpdateOrCreateProfile(targetProfile, UpdateSource.LibraryUpdate);

                // Refresh the Cover and Artwork properties to display the newly downloaded images
                OnPropertyChanged(nameof(Cover));
                OnPropertyChanged(nameof(Artwork));
                OnPropertyChanged(nameof(Logo));
            });

            AddProfileExecutable = new DelegateCommand<object>(async param =>
            {
                string? path = string.Empty;

                FileUtils.CommonFileDialog(out path, out _, out _, SelectedProfile.Path);
                if (string.IsNullOrEmpty(path))
                    return;

                SelectedProfile.Executables.Add(path);
                ManagerFactory.profileManager.UpdateOrCreateProfile(SelectedProfile, UpdateSource.ProfilesPage);
            });

            RemoveProfileExecutable = new DelegateCommand<object>(async param =>
            {
                if (ProfileExecutablesIdx >= 0 && ProfileExecutablesIdx < ProfileExecutables.Count)
                {
                    SelectedProfile.Executables.RemoveAt(ProfileExecutablesIdx);
                    ManagerFactory.profileManager.UpdateOrCreateProfile(SelectedProfile, UpdateSource.ProfilesPage);
                }
            });

            CreateProfileCommand = new DelegateCommand(() =>
            {
                RequestCreateProfile?.Invoke(this, EventArgs.Empty);
            });

            DeleteProfileCommand = new DelegateCommand(() =>
            {
                if (SelectedMainProfile != null)
                    RequestDeleteProfile?.Invoke(this, SelectedMainProfile);
            });

            RenameProfileCommand = new DelegateCommand(() =>
            {
                if (SelectedMainProfile != null)
                    RequestRenameProfile?.Invoke(this, SelectedMainProfile);
            });

            ToggleFavoriteCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile != null)
                {
                    SelectedProfile.IsLiked = !SelectedProfile.IsLiked;
                    SubmitProfile();
                }
            });

            CreateSubProfileCommand = new DelegateCommand(() =>
            {
                RequestCreateSubProfile?.Invoke(this, EventArgs.Empty);
            });

            DeleteSubProfileCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile != null && SelectedProfile.IsSubProfile)
                    RequestDeleteSubProfile?.Invoke(this, SelectedProfile);
            });

            RenameSubProfileCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile != null && SelectedProfile.IsSubProfile)
                    RequestRenameSubProfile?.Invoke(this, SelectedProfile);
                else if (SelectedMainProfile != null)
                    RequestRenameProfile?.Invoke(this, SelectedMainProfile);
            });

            OpenControllerLayoutCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile != null)
                {
                    // Unsubscribe from previous template if it exists
                    selectedTemplate?.Updated -= Template_Updated;
                    selectedTemplate = null;

                    selectedTemplate = new LayoutTemplate(SelectedProfile.Layout)
                    {
                        Name = SelectedProfile.LayoutTitle,
                        Description = Properties.Resources.ProfilesPage_Layout_Desc,
                        Author = Environment.UserName,
                        Executable = SelectedProfile.Executable,
                        Product = SelectedProfile.Name,
                    };
                    selectedTemplate.Updated += Template_Updated;

                    RequestOpenControllerLayout?.Invoke(this, selectedTemplate);
                }
            });

            OpenPowerProfileOnBatteryCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile != null)
                {
                    PowerProfile powerProfile = ManagerFactory.powerProfileManager.GetProfile(SelectedProfile.PowerProfiles[(int)PowerLineStatus.Offline]);
                    if (powerProfile != null)
                        RequestOpenPowerProfile?.Invoke(this, powerProfile);
                }
            });

            OpenPowerProfilePluggedCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile != null)
                {
                    PowerProfile powerProfile = ManagerFactory.powerProfileManager.GetProfile(SelectedProfile.PowerProfiles[(int)PowerLineStatus.Online]);
                    if (powerProfile != null)
                        RequestOpenPowerProfile?.Invoke(this, powerProfile);
                }
            });

            OpenProfilePageCommand = new DelegateCommand(() =>
            {
                RequestOpenProfilePage?.Invoke(this, EventArgs.Empty);
            });

            OpenProfileLayoutCommand = new DelegateCommand(() =>
            {
                RequestOpenProfileLayout?.Invoke(this, EventArgs.Empty);
            });

            CreatePowerProfileCommand = new DelegateCommand(() =>
            {
                // Generate default name if not provided
                if (string.IsNullOrWhiteSpace(CreateProfileName))
                {
                    CreateProfileName = ManagerFactory.powerProfileManager.GetProfileName(Properties.Resources.PowerProfileManualName);
                }

                PowerProfile powerProfile;

                if (CopyDefaultProfileSettings)
                {
                    // Clone the default profile
                    PowerProfile defaultProfile = ManagerFactory.powerProfileManager.GetDefault();
                    powerProfile = ManagerFactory.powerProfileManager.CloneProfile(defaultProfile);
                    powerProfile.Name = CreateProfileName;
                    powerProfile.Description = Properties.Resources.PowerProfileManualDescription;
                    // Generate new GUID for the cloned profile
                    powerProfile.Guid = Guid.NewGuid();
                    powerProfile.Default = false;
                }
                else
                {
                    // Create a new profile with default values
                    powerProfile = new PowerProfile(CreateProfileName, Properties.Resources.PowerProfileManualDescription)
                    {
                        TDPOverrideValues = new[]
                        {
                            IDevice.GetCurrent().nTDP[0],
                            IDevice.GetCurrent().nTDP[1],
                            IDevice.GetCurrent().nTDP[2]
                        }
                    };
                }

                ManagerFactory.powerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);
                RequestCreatePowerProfile?.Invoke(this, EventArgs.Empty);

                // Reset form state
                CreateProfileName = string.Empty;
                CopyDefaultProfileSettings = false;
            });

            ShowCreateProfileFlyoutCommand = new DelegateCommand(() =>
            {
                // Initialize the form with a generated name
                CreateProfileName = ManagerFactory.powerProfileManager.GetProfileName(Properties.Resources.PowerProfileManualName);
                CopyDefaultProfileSettings = false;
            });

            OpenAdditionalSettingsCommand = new DelegateCommand(() =>
            {
                RequestOpenAdditionalSettings?.Invoke(this, EventArgs.Empty);
            });

            BrowseCoverCommand = new DelegateCommand(() => BrowseManualArt(LibraryType.cover));
            BrowseArtworkCommand = new DelegateCommand(() => BrowseManualArt(LibraryType.artwork));
            BrowseLogoCommand = new DelegateCommand(() => BrowseManualArt(LibraryType.logo));
        }

        private void BrowseManualArt(LibraryType libraryType)
        {
            if (_SelectedLibraryIndex < 0 || _SelectedLibraryIndex >= LibraryPickers.Count)
                return;
            LibraryEntryViewModel pickerVM = LibraryPickers[_SelectedLibraryIndex];
            LibraryEntry libraryEntry = pickerVM.LibEntry;

            Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dlg = new();
            if (libraryType.HasFlag(LibraryType.logo))
            {
                // Logo requires transparency  PNG only
                dlg.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("PNG Image", "*.png"));
            }
            else
            {
                // Cover and artwork accept PNG or JPEG
                dlg.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("Image Files", "*.png;*.jpg;*.jpeg"));
                dlg.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("PNG Image", "*.png"));
                dlg.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("JPEG Image", "*.jpg;*.jpeg"));
            }

            if (dlg.ShowDialog() != Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                return;

            string? sourcePath = dlg.FileName;
            if (string.IsNullOrEmpty(sourcePath) || !System.IO.File.Exists(sourcePath))
                return;

            // Copy the file into the cache immediately so the library page and the dialog
            // can both load it via the normal GetGameArt path (incl. thumbnails sub-folder).
            long imageId;
            if (libraryType.HasFlag(LibraryType.cover))
                imageId = ManualEntry.ManualCoverId;
            else if (libraryType.HasFlag(LibraryType.artwork))
                imageId = ManualEntry.ManualArtworkId;
            else
                imageId = ManualEntry.ManualLogoId;

            string? cachedPath = ManagerFactory.libraryManager.CopyManualArt(libraryEntry.Id, libraryType, imageId, sourcePath);
            if (cachedPath is null)
                return;

            string extension = System.IO.Path.GetExtension(cachedPath);

            // Update the entry so the serialised JSON contains the cache path
            if (libraryType.HasFlag(LibraryType.cover))
                libraryEntry.ManualCoverPath = cachedPath;
            else if (libraryType.HasFlag(LibraryType.artwork))
                libraryEntry.ManualArtworkPath = cachedPath;
            else
                libraryEntry.ManualLogoPath = cachedPath;

            // Rebuild the single visual slot. Full-res keeps the source extension; thumbnail is
            // always PNG (WriteResizedThumbnail encodes PNG regardless of source format).
            pickerVM.RefreshManualVisual(libraryType, extension, thumbnailExtension: ".png");

            if (libraryType.HasFlag(LibraryType.cover))
                RefreshCover(0);
            else if (libraryType.HasFlag(LibraryType.artwork))
                RefreshArtwork(0);
            else
                RefreshLogo(0);

            OnPropertyChanged(nameof(SelectedLibraryEntry));
        }

        private void SetupManagerEvents()
        {
            switch (ManagerFactory.multimediaManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    MultimediaManager_Initialized();
                    break;
            }

            switch (ManagerFactory.gpuManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.gpuManager.Initialized += GpuManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryGPU();
                    break;
            }

            ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;

            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

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

            switch (ManagerFactory.powerProfileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryPowerProfile();
                    break;
            }
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            SubmitProfile();
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            OnPropertyChanged(nameof(GPUManagementEnabled));
            OnPropertyChanged(nameof(PerformanceManagerEnabled));
            OnPropertyChanged(nameof(ShowPowerSlots));
        }

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            if (name == "GPUManagementEnabled")
                OnPropertyChanged(nameof(GPUManagementEnabled));
            if (name == "PerformanceManagerEnabled")
                OnPropertyChanged(nameof(PerformanceManagerEnabled));
            if (name is "GPUManagementEnabled" or "PerformanceManagerEnabled")
                OnPropertyChanged(nameof(ShowPowerSlots));
        }

        private void QueryPlatforms()
        {
            PlatformManager.RTSS.Updated += RTSS_Updated;
            RTSS_Updated(PlatformManager.RTSS.Status);
        }

        private void PlatformManager_Initialized()
        {
            QueryPlatforms();
        }

        private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
        {
            // QuickTools: show the currently applied power profile name
            // ProfilesPage: show the selected profile's power profile for the current AC/DC state
            if (IsQuickTools)
                SelectedPowerProfileName = profile.Name;
            else
                UpdateSelectedPowerProfileName();
        }

        private void QueryPowerProfile()
        {
            ManagerFactory.powerProfileManager.Updated += PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted += PowerProfileManager_Deleted;

            using (new LoadingScope(this))
            {
                foreach (PowerProfile powerProfile in ManagerFactory.powerProfileManager.profiles.Values)
                    PowerProfileManager_Updated(powerProfile, UpdateSource.Serializer);

                // If a profile was already selected before power profiles were loaded,
                // the initial UpdatePowerProfileSelections() found ProfilePicker empty.
                // Re-apply now that it's populated.
                if (SelectedProfile != null)
                {
                    UpdatePowerProfileSelections();
                    UpdateSelectedPowerProfileName();
                }
            }
        }

        private void PowerProfileManager_Initialized()
        {
            QueryPowerProfile();
        }

        private void PowerProfileManager_Deleted(PowerProfile profile)
        {
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                ProfilesPickerViewModel? foundPreset = ProfilePicker.FirstOrDefault(p => p.LinkedPresetId == profile.Guid);
                if (foundPreset is not null)
                {
                    ProfilePicker.Remove(foundPreset);

                    if (SelectedPresetAC?.Guid == foundPreset.LinkedPresetId)
                        SelectedPickerAC = ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty);
                    if (SelectedPresetDC?.Guid == foundPreset.LinkedPresetId)
                        SelectedPickerDC = ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty);
                }
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
        }

        private void PowerProfileManager_Updated(PowerProfile profile, UpdateSource source)
        {
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                int index;
                ProfilesPickerViewModel? foundPreset = ProfilePicker.FirstOrDefault(p => p.LinkedPresetId == profile.Guid);
                if (foundPreset is not null)
                {
                    index = ProfilePicker.IndexOf(foundPreset);
                    foundPreset.Text = profile.Name;
                }
                else
                {
                    index = 0;
                    ProfilePicker.Insert(index, new() { LinkedPresetId = profile.Guid, Text = profile.Name, IsInternal = profile.IsDefault() || profile.IsDeviceDefault() });
                }
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }

            // the effective framerate limit may come from this power profile
            RefreshFramerateHints();
        }

        public void PowerProfile_Selected(PowerProfile powerProfile, bool AC)
        {
            if (SelectedProfile is null)
                return;

            // Don't update profile if we're loading from it (prevent circular updates)
            if (isLoadingProfile)
                return;

            int powerLineStatus = AC ? (int)PowerLineStatus.Online : (int)PowerLineStatus.Offline;
            Guid previousGuid = SelectedProfile.PowerProfiles[powerLineStatus];
            if (previousGuid == powerProfile.Guid)
                return;

            LogManager.LogInformation("Profile power selection changed: {0}, state: {1}, old: {2}, new: {3}",
                SelectedProfile.Name, AC ? "AC" : "DC", previousGuid, powerProfile.Guid);

            SelectedProfile.PowerProfiles[powerLineStatus] = powerProfile.Guid;
            RefreshFramerateOverrides();
            UpdateProfile();
        }

        private void MultimediaManager_Initialized()
        {
            ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
            ManagerFactory.multimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;
            ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
            UIHelper.TryBeginInvoke(UpdateDisplayMonitors);

            try
            {
                DesktopScreen? desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                if (desktopScreen is not null)
                {
                    lock (_collectionLock6)
                    {
                        IntegerScalingDividers.Clear();
                        foreach (var screenDivider in desktopScreen.screenDividers)
                            IntegerScalingDividers.Add(new ScreenDividerViewModel(screenDivider));
                    }

                    RefreshGraphicsOverrideRows();
                }
            }
            catch { }
        }

        private void GpuManager_Initialized()
        {
            QueryGPU();
        }

        private void QueryGPU()
        {
            ManagerFactory.gpuManager.Hooked += GPUManager_Hooked;
            ManagerFactory.gpuManager.Unhooked += GPUManager_Unhooked;

            GPU? gpu = GPUManager.GetCurrent();
            if (gpu is not null)
                GPUManager_Hooked(gpu);
        }

        private void GPUManager_Hooked(GPU GPU)
        {
            IsAMDGPU = GPU is AMDGPU;
            HasRSRSupport = false;
            HasAFMFSupport = false;

            if (GPU is AMDGPU amdGPU)
            {
                amdGPU.RSRStateChanged += OnRSRStateChanged;
                HasRSRSupport = amdGPU.HasRSRSupport();

                amdGPU.AFMFStateChanged += OnAFMFStateChanged;
                HasAFMFSupport = amdGPU.HasAFMFSupport();

                amdGPU.AFMF21StateChanged += OnAFMF21StateChanged;
                HasAFMF21Support = amdGPU.GetAFMFAlgorithmSupport();
            }

            GPU.IntegerScalingChanged += OnIntegerScalingChanged;
            GPU.GPUScalingChanged += OnGPUScalingChanged;

            HasScalingModeSupport = GPU.HasScalingModeSupport();
            HasIntegerScalingSupport = GPU.HasIntegerScalingSupport();
            HasGPUScalingSupport = GPU.HasGPUScalingSupport();

            UpdateGraphicsSettingsUI();
        }

        private void GPUManager_Unhooked(GPU GPU)
        {
            if (GPU is AMDGPU amdGPU)
            {
                amdGPU.RSRStateChanged -= OnRSRStateChanged;
                amdGPU.AFMFStateChanged -= OnAFMFStateChanged;
                amdGPU.AFMF21StateChanged -= OnAFMF21StateChanged;
            }

            GPU.IntegerScalingChanged -= OnIntegerScalingChanged;
            GPU.GPUScalingChanged -= OnGPUScalingChanged;

            IsAMDGPU = false;
            HasRSRSupport = false;
            HasAFMFSupport = false;
            HasAFMF21Support = false;
            HasGPUScalingSupport = false;
            HasIntegerScalingSupport = false;
            HasScalingModeSupport = false;
        }

        private void UpdateGraphicsSettingsUI()
        {
            OnPropertyChanged(nameof(HasRSRSupport));
            OnPropertyChanged(nameof(HasAFMFSupport));
            OnPropertyChanged(nameof(HasAFMF21Support));
            OnPropertyChanged(nameof(HasGPUScalingSupport));
            OnPropertyChanged(nameof(HasIntegerScalingSupport));
            OnPropertyChanged(nameof(HasScalingModeSupport));
            OnPropertyChanged(nameof(HasPrebuiltShaderDownload));
            OnPropertyChanged(nameof(PrebuiltShaderDownloadEnabled));
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

        private void OnAFMF21StateChanged(bool AlgorithmSupported, int Algorithm, int SearchMode, int PerformanceMode, int FastMotionResponse)
        {
            if (AlgorithmSupported != HasAFMF21Support)
            {
                HasAFMF21Support = AlgorithmSupported;
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
            IsRTSSReady = status == PlatformStatus.Ready || status == PlatformStatus.Started;
        }

        private void ProcessManager_Initialized_Main()
        {
            QueryForeground_Main();
        }

        private void QueryForeground_Main()
        {
            // When ProcessManager is initialized, check if any running process matches the selected profile
            // This handles the case where a process is already running when ProfilesPage is first loaded
            UpdateCurrentProcessViewModel();
        }

        /// <summary>
        /// ProfilesPage only: Updates CurrentProcessViewModel when a new process starts.
        /// Checks if the new process matches any of the selected profile's executables.
        /// </summary>
        private void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            if (OnStartup)
                return;

            UpdateCurrentProcessViewModel();
        }

        /// <summary>
        /// ProfilesPage only: Updates CurrentProcessViewModel when a process stops.
        /// If the stopped process was the one we're tracking, clears CurrentProcessViewModel.
        /// </summary>
        private void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            if (CurrentProcessViewModel?.Process == processEx)
            {
                CurrentProcessViewModel?.Dispose();
                CurrentProcessViewModel = null;
            }
        }

        private void LibraryManager_Initialized()
        {
            QueryLibrary();
        }

        private void QueryLibrary()
        {
            ManagerFactory.libraryManager.StatusChanged += LibraryManager_StatusChanged;
            ManagerFactory.libraryManager.NetworkAvailabilityChanged += LibraryManager_NetworkAvailabilityChanged;

            // Initial update
            OnPropertyChanged(nameof(IsLibraryConnected));
            OnPropertyChanged(nameof(IsLibrarySettingsEnabled));
            OnPropertyChanged(nameof(IsLibraryBusy));
            OnPropertyChanged(nameof(CanApplyLibrary));
        }

        private void LibraryManager_NetworkAvailabilityChanged(bool status)
        {
            OnPropertyChanged(nameof(IsLibraryConnected));
            OnPropertyChanged(nameof(IsLibrarySettingsEnabled));
        }

        private void LibraryManager_StatusChanged(ManagerStatus status)
        {
            OnPropertyChanged(nameof(IsLibraryBusy));
            OnPropertyChanged(nameof(CanApplyLibrary));
        }

        private void ProfileApplied(Profile profile, UpdateSource source)
        {
            if (IsQuickTools)
            {
                // QuickProfilesPage always processes Applied events to stay in sync with foreground app
                // The isLoadingProfile flag prevents infinite loops
                HandleProfileApplied(profile, source);
            }
            else
            {
                if (SelectedMainProfile?.Guid == profile.Guid ||
                    (!profile.IsSubProfile && SelectedMainProfile?.Guid == profile.ParentGuid) ||
                    SelectedProfile?.Guid == profile.Guid ||
                    SelectedProfile?.ParentGuid == profile.Guid)
                {
                    HandleProfileApplied(profile, source);
                }
            }
        }

        /// <summary>
        /// Handles profile application from ProfileManager.
        /// 
        /// CRITICAL: Must handle collection updates carefully:
        /// 1. Set SelectedMainProfile first (updates backing field only to avoid triggering UpdateSubProfiles prematurely)
        /// 2. Manually populate SubProfiles using SafeUpdateSubProfiles
        /// 3. Set SelectedProfile last (triggers OnProfileChanged with isLoadingProfile=true)
        /// 
        /// This order prevents WPF binding from setting SelectedProfile=null during SubProfiles.Clear().
        /// </summary>
        private void HandleProfileApplied(Profile profile, UpdateSource source)
        {
            UIHelper.TryBeginInvoke(() =>
            {
                lock (_collectionLock5)
                {
                    if (isLoadingProfile)
                        return;

                    using (new LoadingScope(this))
                    {
                        if (UpdateTimer.Enabled)
                        {
                            UpdateTimer.Stop();
                            SubmitProfile();
                        }

                        // update profile
                        Profile mainProfile = ManagerFactory.profileManager.GetParent(profile);
                        if (SelectedMainProfile?.Guid != mainProfile.Guid)
                            SelectedMainProfile = mainProfile;

                        // update subprofiles
                        IEnumerable<Profile> subProfiles = ManagerFactory.profileManager.GetSubProfilesFromProfile(mainProfile, true);
                        SafeUpdateSubProfiles(subProfiles, profile);

                        int selectedIndex = subProfiles.Select((p, i) => new { p, i }).FirstOrDefault(x => x.p.Guid == profile.Guid)?.i ?? 0;
                        if (SelectedSubProfileIndex != selectedIndex)
                            SelectedSubProfileIndex = selectedIndex;

                        if (IsQuickTools && profile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? currentAction))
                        {
                            if (currentAction is GyroActions gyroActions && gyroActions.MotionTrigger != null)
                            {
                                if (gyroActions.MotionTrigger.Clone() is ButtonState buttonState)
                                {
                                    GyroHotkey.inputsChord.ButtonState = buttonState;
                                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(GyroHotkey, UpdateSource.Background);
                                }
                            }
                        }

                        UpdateUI();
                    }
                }
            });
        }

        public void SelectProfileFromToast(Profile profile)
        {
            HandleProfileApplied(profile, UpdateSource.Background);
        }

        /// <summary>
        /// Handles external profile updates from ProfileManager.
        /// 
        /// IMPORTANT: Uses backing field (_selectedProfile) instead of property to avoid
        /// triggering OnProfileChanged() and causing redundant updates.
        /// </summary>
        public void ProfileUpdated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // Serializer loads are handled in bulk by ProfileManagerLoaded; skip per-profile
            // UI work here to avoid N blocking Dispatcher.Invoke calls and N CollectionChanged
            // notifications that are immediately discarded when ProfileManagerLoaded clears the collection.
            if (source == UpdateSource.Serializer)
                return;

            // the default profile is an inherit layer for every game profile
            if (profile.Default)
            {
                RefreshFramerateHints();
                UIHelper.TryBeginInvoke(RefreshDisplayModeOverrides);
                UIHelper.TryBeginInvoke(RefreshGraphicsOverrideRows);
            }

            isCurrent |= SelectedProfile?.Guid == profile.Guid;
            if (!IsQuickTools)
                isCurrent |= source.HasFlag(UpdateSource.Creation);

            if (source == UpdateSource.QuickProfilesPage && !isCurrent)
                return;

            UIHelper.TryInvoke(() =>
            {
                if (!profile.IsSubProfile)
                {
                    var existingVm = MainProfiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);
                    if (existingVm == null)
                    {
                        MainProfiles.Add(new ProfileViewModel(profile, false));

                        if (!IsQuickTools && source.HasFlag(UpdateSource.Creation))
                            SelectedMainProfile = profile;
                        return;
                    }

                    existingVm.Profile = profile;
                    MainProfilesView.Refresh();
                    if (SelectedMainProfile?.Guid == profile.Guid)
                        SetSelectedMainProfile(profile, false, true);
                }
                else
                {
                    var existingSubProfile = SubProfiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);
                    if (existingSubProfile == null && SelectedMainProfile != null && profile.ParentGuid == SelectedMainProfile.Guid)
                    {
                        SubProfiles.Add(new ProfileViewModel(profile, IsQuickTools));
                        if (!IsQuickTools && source.HasFlag(UpdateSource.Creation))
                            SelectedProfile = profile;
                        return;
                    }
                    else if (existingSubProfile != null)
                    {
                        existingSubProfile.Profile = profile;
                        SubProfilesView.Refresh();
                    }
                }

                if (SelectedProfile?.Guid == profile.Guid)
                {
                    SetSelectedProfile(profile, false, true);
                    OnPropertyChanged(nameof(Cover));
                    OnPropertyChanged(nameof(Artwork));
                    OnPropertyChanged(nameof(Logo));

                    if (profile.IsSubProfile && !IsQuickTools)
                    {
                        ProfileViewModel? subProfile = SubProfiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);
                        int subProfileIndex = subProfile is null ? -1 : SubProfiles.IndexOf(subProfile);
                        if (subProfileIndex >= 0 && subProfileIndex != _SelectedSubProfileIndex)
                            SelectedSubProfileIndex = subProfileIndex;
                    }

                    RefreshProfileExecutables();
                    UpdateUI();
                }
                else if (!profile.IsSubProfile && SelectedProfile?.ParentGuid == profile.Guid)
                {
                    UpdateUI();
                }
            });
        }

        /// <summary>
        /// Handles profile deletion from ProfileManager.
        /// 
        /// CRITICAL for SubProfiles: Must set SelectedProfile BEFORE removing from SubProfiles collection.
        /// WPF binding will automatically set SelectedProfile=null when the selected item is removed from the bound collection.
        /// </summary>
        public void ProfileDeleted(Profile profile)
        {
            UIHelper.TryInvoke(() =>
            {
                if (!profile.IsSubProfile)
                {
                    // CRITICAL: Set new selection BEFORE removing from collection.
                    // WPF will auto-null SelectedItem when the selected item is removed.
                    if (SelectedMainProfile?.Guid == profile.Guid)
                        SelectedMainProfile = ManagerFactory.profileManager.GetDefault();

                    var existingVm = MainProfiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);
                    if (existingVm != null)
                    {
                        MainProfiles.Remove(existingVm);
                        existingVm.Dispose();
                    }
                }
                else
                {
                    if (SelectedProfile?.Guid == profile.Guid && SelectedMainProfile != null)
                    {
                        SelectedProfile = SelectedMainProfile;
                    }

                    var existingSubVm = SubProfiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);
                    if (existingSubVm != null)
                    {
                        SubProfiles.Remove(existingSubVm);
                        existingSubVm.Dispose();
                    }
                }

                if (IsQuickTools && SelectedProfile == profile)
                {
                    SelectedProfile = ManagerFactory.profileManager.GetDefault();
                }
            });
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
        }

        private void QueryProfile()
        {
            ManagerFactory.profileManager.Deleted += ProfileDeleted;
            ManagerFactory.profileManager.Updated += ProfileUpdated;
            ManagerFactory.profileManager.Applied += ProfileApplied;
            ManagerFactory.profileManager.LastMonitorChanged += ProfileManager_LastMonitorChanged;
            ManagerFactory.processManager.ProcessStarted += DisplayBadges_ProcessStarted;
            ManagerFactory.processManager.ProcessStopped += DisplayBadges_ProcessStopped;

            UIHelper.TryInvoke(() =>
            {
                foreach (var vm in MainProfiles)
                    vm.Dispose();
                MainProfiles.Clear();
                var profiles = ManagerFactory.profileManager.GetProfiles(false);
                foreach (var profile in profiles.OrderBy(p => p.Name))
                    MainProfiles.Add(new ProfileViewModel(profile, false));

                Profile defaultProfile = ManagerFactory.profileManager.GetDefault();
                SelectedMainProfile = defaultProfile;
            });
        }

        private void OnProfileChanged()
        {
            LibrarySearchField = SelectedProfile?.Name ?? "";

            ClearLibrary();

            OnPropertyChanged(nameof(Cover));
            OnPropertyChanged(nameof(Artwork));
            OnPropertyChanged(nameof(Logo));

            ClearWindows();

            lock (_collectionLock7)
            {
                AllWindows.Clear();
                if (SelectedProfile != null)
                {
                    foreach (var kvp in SelectedProfile.WindowsSettings)
                        AllWindows.Add(new WindowListItemViewModel(kvp.Key, kvp.Value));
                }
            }

            OnPropertyChanged(nameof(HasAnyWindows));

            selectedProcess = null;
            if (SelectedProfile != null)
            {
                List<string> execs = SelectedProfile.GetExecutables(true);
                selectedProcess = ProcessManager.GetProcesses().FirstOrDefault(p => execs.Contains(p.Path));
            }

            if (selectedProcess is not null)
            {
                selectedProcess.WindowAttached += SelectedProcess_WindowAttached_Merged;
                selectedProcess.WindowDetached += SelectedProcess_WindowDetached_Merged;

                foreach (ProcessWindow processWindow in selectedProcess.ProcessWindows.Values)
                    SelectedProcess_WindowAttached_Merged(processWindow);
            }

            // Update CurrentProcessViewModel based on the selected profile's process
            UpdateCurrentProcessViewModel();

            RefreshProfileExecutables();

            UpdateUI();
        }

        /// <summary>
        /// Refreshes the ProfileExecutables collection from SelectedProfile.Executables.
        /// Called when the profile changes or when executables are added/removed.
        /// </summary>
        private void RefreshProfileExecutables()
        {
            lock (_collectionLock3)
            {
                ProfileExecutables.Clear();
                if (SelectedProfile != null)
                {
                    foreach (string path in SelectedProfile.Executables)
                        ProfileExecutables.Add(path);

                    var idx = SelectedProfile.Executables.IndexOf(SelectedProfile.Path);
                    if (ProfileExecutables.Count > 0 && idx == -1) idx = 0;
                    ProfileExecutablesIdx = (ProfileExecutables.Count == 0) ? -1 : Math.Min(idx, ProfileExecutables.Count - 1);
                }
            }
        }

        private void UpdateCurrentProcessViewModel()
        {
            ProcessEx? profileProcess = null;
            if (SelectedProfile != null)
            {
                List<string> execs = SelectedProfile.GetExecutables(true);
                profileProcess = ProcessManager.GetProcesses().FirstOrDefault(p => execs.Contains(p.Path));
            }

            CurrentProcessViewModel?.Dispose();
            if (profileProcess is null)
                CurrentProcessViewModel = null;
            else
                CurrentProcessViewModel = new ProcessExViewModel(profileProcess, false);
        }

        /// <summary>
        /// Updates UI properties from the selected profile.
        /// 
        /// CRITICAL: Always call within a LoadingScope or set isLoadingProfile=true
        /// to prevent property setters from triggering profile updates (circular loop).
        /// </summary>
        private void UpdateUI()
        {
            if (SelectedProfile == null)
                return;

            using (new LoadingScope(this))
            {
                if (SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? gyroActions))
                {
                    if (gyroActions is AxisActions axisActions)
                    {
                        AntiDeadzoneValue = axisActions.AxisAntiDeadZone;
                        GyroWeightValue = axisActions.gyroWeight;

                        OutputMode = axisActions.Axis switch
                        {
                            AxisLayoutFlags.LeftStick => (int)MotionOutput.LeftStick,
                            AxisLayoutFlags.RightStick => (int)MotionOutput.RightStick,
                            _ => (int)MotionOutput.RightStick
                        };
                    }
                    else if (gyroActions is MouseActions mouseActions)
                    {
                        OutputMode = (int)MotionOutput.MoveCursor;
                    }
                    else if (gyroActions is TouchpadActions { TargetType: TouchpadTargetType.Axis } touchpadActions)
                    {
                        GyroWeightValue = touchpadActions.gyroWeight;
                        OutputMode = touchpadActions.Axis switch
                        {
                            AxisLayoutFlags.LeftPad => (int)MotionOutput.LeftPad,
                            AxisLayoutFlags.RightPad => (int)MotionOutput.RightPad,
                            _ => (int)MotionOutput.Disabled
                        };
                    }

                    if (gyroActions is GyroActions gyroAction)
                    {
                        InputMode = (int)gyroAction.MotionInput;
                        MotionMode = (int)gyroAction.MotionMode;
                    }
                }
                else
                {
                    OutputMode = (int)MotionOutput.Disabled;
                }

                SensitivityXValue = SelectedProfile.MotionSensivityX;
                SensitivityYValue = SelectedProfile.MotionSensivityY;
                GyroMultiplier = SelectedProfile.GyrometerMultiplier;
                AcceleroMultiplier = SelectedProfile.AccelerometerMultiplier;

                ProfileEnabled = IsQuickTools ? !SelectedProfile.Default : SelectedProfile.Enabled;
                CurrentProfileName = SelectedProfile.Name;
                CurrentProfileDescription = SelectedProfile.LibraryEntry?.Description ?? string.Empty;
                ProfileArguments = SelectedProfile.Arguments;
                ProfileLaunchString = SelectedProfile.LaunchString;

                UpdatePowerProfileSelections();
                UpdateSelectedPowerProfileName();
                GetPerformanceOverrides(FramerateSlotDC, true);
                GetPerformanceOverrides(FramerateSlotAC, true);
                RevalidateFramerateCustom();
                RefreshFramerateOverrides();
                RefreshDisplayModeOverrides();
                RefreshGraphicsOverrideRows();
                UpdateControlsEnabledState();
                NotifyWrapperProperties();
            }
        }

        /// <summary>
        /// Updates power profile selections from the selected profile.
        /// Uses SelectedItem (SelectedPickerDC/AC) instead of indices to avoid
        /// race conditions when collections are modified from background threads.
        /// </summary>
        private void UpdatePowerProfileSelections()
        {
            if (SelectedProfile.PowerProfiles.ContainsKey((int)PowerLineStatus.Offline))
            {
                Guid offlineGuid = SelectedProfile.PowerProfiles[(int)PowerLineStatus.Offline];
                var pickerViewModel = ProfilePicker.FirstOrDefault(p => p.LinkedPresetId == offlineGuid);
                if (pickerViewModel != null)
                {
                    SelectedPickerDC = pickerViewModel;
                }
            }

            if (SelectedProfile.PowerProfiles.ContainsKey((int)PowerLineStatus.Online))
            {
                Guid onlineGuid = SelectedProfile.PowerProfiles[(int)PowerLineStatus.Online];
                var pickerViewModel = ProfilePicker.FirstOrDefault(p => p.LinkedPresetId == onlineGuid);
                if (pickerViewModel != null)
                {
                    SelectedPickerAC = pickerViewModel;
                }
            }
        }

        /// <summary>
        /// Notifies all wrapper property changes.
        /// Separated to reduce clutter in UpdateUI.
        /// </summary>
        private void NotifyWrapperProperties()
        {
            OnPropertyChanged(nameof(SteeringAxisIndex));
            OnPropertyChanged(nameof(HIDModeIndex));
            OnPropertyChanged(nameof(XInputPlusIndex));
            OnPropertyChanged(nameof(ProfilePath));
            OnPropertyChanged(nameof(ProfileWhitelisted));
            OnPropertyChanged(nameof(ShowInLibrary));
            OnPropertyChanged(nameof(MotionInvertHorizontal));
            OnPropertyChanged(nameof(MotionInvertVertical));
            OnPropertyChanged(nameof(SuspendOnQT));
            OnPropertyChanged(nameof(SuspendOnSleep));
            OnPropertyChanged(nameof(FullScreenOptimization));
            OnPropertyChanged(nameof(HighDPIAware));
            OnPropertyChanged(nameof(IsLiked));
        }

        private void UpdateSelectedPowerProfileName()
        {
            if (SelectedProfile == null)
                return;

            PowerLineStatus currentStatus = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus;

            Guid powerProfileGuid = currentStatus == PowerLineStatus.Online
                ? SelectedProfile.PowerProfiles[(int)PowerLineStatus.Online]
                : SelectedProfile.PowerProfiles[(int)PowerLineStatus.Offline];

            PowerProfile powerProfile = ManagerFactory.powerProfileManager.GetProfile(powerProfileGuid);
            if (powerProfile?.Name != null)
                SelectedPowerProfileName = powerProfile.Name;
        }

        private void UpdateControlsEnabledState()
        {
            if (SelectedProfile == null)
                return;

            // XInput+ wrapper controls
            // Disable injection if: Running, MissingExecutable, MissingPath, or Default
            // Disable redirection additionally if: MissingPermission
            bool disableWrapperControls = SelectedProfile.ErrorCode.HasFlag(ProfileErrorCode.Running)
                || SelectedProfile.ErrorCode.HasFlag(ProfileErrorCode.MissingExecutable)
                || SelectedProfile.ErrorCode.HasFlag(ProfileErrorCode.MissingPath)
                || SelectedProfile.ErrorCode.HasFlag(ProfileErrorCode.Default);

            bool disableRedirection = disableWrapperControls || SelectedProfile.ErrorCode.HasFlag(ProfileErrorCode.MissingPermission);

            IsWrapperInjectionEnabled = !disableWrapperControls;
            IsWrapperRedirectionEnabled = !disableRedirection;

            // ProfileDetailsExpander (Path/Arguments/Executables) - disabled only for Default profile
            IsProfileDetailsExpanderEnabled = !SelectedProfile.ErrorCode.HasFlag(ProfileErrorCode.Default);

            // Warning InfoBar - show for any error except None
            HasWarning = SelectedProfile.ErrorCode != ProfileErrorCode.None;
            WarningMessage = EnumUtils.GetDescriptionFromEnumValue(SelectedProfile.ErrorCode);

            OnPropertyChanged(nameof(IsProfileEnabledToggleEnabled));
            OnPropertyChanged(nameof(IsControllerPassthroughEnabled));
        }

        private void UpdateSubProfiles(Profile? updatedProfile = null)
        {
            if (SelectedMainProfile is null)
                return;

            lock (_collectionLock5)
            {
                try
                {
                    IEnumerable<Profile> profiles = ManagerFactory.profileManager.GetSubProfilesFromProfile(SelectedMainProfile, true);

                    int selectedIndex;
                    if (updatedProfile != null && profiles.Contains(updatedProfile))
                        selectedIndex = profiles.Select((p, i) => new { p, i }).FirstOrDefault(x => x.p.Guid == updatedProfile.Guid)?.i ?? 0;
                    else
                        selectedIndex = profiles.Select((p, i) => new { p, i }).FirstOrDefault(x => x.p.IsFavoriteSubProfile)?.i ?? 0;

                    Profile profileToSelect = profiles.ElementAtOrDefault(selectedIndex) ?? SelectedMainProfile;

                    SafeUpdateSubProfiles(profiles, profileToSelect);
                    SelectedProfile = profileToSelect;
                }
                catch { }
            }
        }

        private void SelectedProcess_WindowAttached_Merged(ProcessWindow processWindow)
        {
            lock (_collectionLock7)
            {
                var item = AllWindows.FirstOrDefault(w => w.Hwnd == processWindow.Hwnd && w.Hwnd != 0);
                if (item is null)
                    AllWindows.Add(item = new WindowListItemViewModel(processWindow));
                else
                    item.UpdateFrom(processWindow);
            }

            OnPropertyChanged(nameof(HasAnyWindows));
        }

        private void SelectedProcess_WindowDetached_Merged(ProcessWindow processWindow)
        {
            var item = AllWindows.FirstOrDefault(w => w.Hwnd == processWindow.Hwnd);
            item?.ProcessWindow = null;

            OnPropertyChanged(nameof(HasAnyWindows));
        }

        private void ClearWindows()
        {
            lock (_collectionLock7)
                AllWindows.Clear();

            if (selectedProcess is not null)
            {
                selectedProcess.WindowAttached -= SelectedProcess_WindowAttached_Merged;
                selectedProcess.WindowDetached -= SelectedProcess_WindowDetached_Merged;
            }
        }

        private void ClearLibrary()
        {
            LibraryArtworksIndex = -1;
            LibraryCoversIndex = -1;
            LibraryLogosIndex = -1;
            SelectedLibraryIndex = -1;
            lock (_collectionLock2)
                LibraryPickers.Clear();

            // Notify that library entries have been cleared
            OnPropertyChanged(nameof(HasLibraryEntry));
            OnPropertyChanged(nameof(CanApplyLibrary));
        }

        private void SelectedLibraryChanged()
        {
            // These collections depend on SelectedLibraryIndex. Refresh the
            // FlipView item sources before synchronizing their selected indices.
            OnPropertyChanged(nameof(LibraryCovers));
            OnPropertyChanged(nameof(LibraryArtworks));
            OnPropertyChanged(nameof(LibraryLogos));

            LibraryArtworksIndex = GetLibraryVisualIndex(LibraryArtworks, SelectedLibraryEntry?.GetArtworkId() ?? 0);
            LibraryCoversIndex = GetLibraryVisualIndex(LibraryCovers, SelectedLibraryEntry?.GetCoverId() ?? 0);
            LibraryLogosIndex = GetLibraryVisualIndex(LibraryLogos, SelectedLibraryEntry?.GetLogoId() ?? 0);

            // An index can have the same numeric value for two different item
            // sources, in which case its setter does not raise a notification.
            OnPropertyChanged(nameof(LibraryCoversIndex));
            OnPropertyChanged(nameof(LibraryArtworksIndex));
            OnPropertyChanged(nameof(LibraryLogosIndex));

            OnPropertyChanged(nameof(LibraryCoversPageCount));
            OnPropertyChanged(nameof(LibraryArtworksPageCount));
            OnPropertyChanged(nameof(LibraryLogosPageCount));
            OnPropertyChanged(nameof(IsLibraryOrManualEnabled));
        }

        private static int GetLibraryVisualIndex(ObservableCollection<LibraryVisualViewModel> visuals, long selectedId)
        {
            if (visuals.Count == 0)
                return -1;

            int selectedIndex = visuals
                .Select((visual, index) => new { visual.Id, index })
                .FirstOrDefault(item => item.Id == selectedId)?.index ?? -1;

            return selectedIndex >= 0 ? selectedIndex : 0;
        }

        private async Task TriggerGameArtDownloadAsync(int value, LibraryType libraryType)
        {
            if (_SelectedLibraryEntry is not null)
                await ManagerFactory.libraryManager.DownloadGameArt(_SelectedLibraryEntry, value, libraryType);

            if (libraryType.HasFlag(LibraryType.cover))
                RefreshCover(value);
            else if (libraryType.HasFlag(LibraryType.artwork))
                RefreshArtwork(value);
            else if (libraryType.HasFlag(LibraryType.logo))
                RefreshLogo(value);
        }

        private void RefreshCover(int index)
        {
            try
            {
                if (index >= 0 && index < LibraryCovers.Count)
                    LibraryCovers[index].RefreshImage();

                OnPropertyChanged(nameof(LibraryCovers));
                OnPropertyChanged(nameof(LibraryCoversPageCount));
                SetLibraryCoversIndex(index);
            }
            catch { }
        }

        private void RefreshArtwork(int index)
        {
            try
            {
                if (index >= 0 && index < LibraryArtworks.Count)
                    LibraryArtworks[index].RefreshImage();

                OnPropertyChanged(nameof(LibraryArtworks));
                OnPropertyChanged(nameof(LibraryArtworksPageCount));
                SetLibraryArtworksIndex(index);
            }
            catch { }
        }

        private void RefreshLogo(int index)
        {
            try
            {
                if (index >= 0 && index < LibraryLogos.Count)
                    LibraryLogos[index].RefreshImage();

                OnPropertyChanged(nameof(LibraryLogos));
                OnPropertyChanged(nameof(LibraryLogosPageCount));
                SetLibraryLogosIndex(index);
            }
            catch { }
        }

        /// <summary>
        /// QuickProfilesPage only: Handles foreground process changes.
        /// Updates CurrentProcessViewModel to track the currently focused application.
        /// </summary>
        private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessFilter filter)
        {
            switch (filter)
            {
                case ProcessFilter.HandheldCompanion:
                    return;
            }

            try
            {
                currentProcess = processEx;
                string path = currentProcess is not null ? currentProcess.Path : string.Empty;

                if (currentProcess is null || currentProcess.Filter != ProcessFilter.Allowed)
                {
                    ProcessIcon = null;
                    IsProcessCardEnabled = false;
                    ProcessName = Properties.Resources.QuickProfilesPage_Waiting;
                    ProcessPath = string.Empty;
                    IsProcessPathVisible = false;
                    IsSubProfilesVisible = false;
                }
                else
                {
                    ProcessIcon = currentProcess?.ProcessIcon;
                    IsProcessCardEnabled = true;
                    ProcessName = currentProcess?.Executable ?? string.Empty;
                    ProcessPath = path;
                    IsProcessPathVisible = true;
                    IsSubProfilesVisible = true;
                }

                if (IsQuickTools)
                {
                    // Unsubscribe from old ViewModel
                    CurrentProcessViewModel?.PropertyChanged -= CurrentProcessViewModel_PropertyChanged;
                    CurrentProcessViewModel?.Dispose();

                    if (currentProcess is null || currentProcess.Filter != ProcessFilter.Allowed)
                        CurrentProcessViewModel = null;
                    else
                        CurrentProcessViewModel = new ProcessExViewModel(currentProcess, true);

                    Profile foregroundProfile = currentProcess is not null && currentProcess.Filter == ProcessFilter.Allowed
                        ? ManagerFactory.profileManager.GetProfileFromPath(currentProcess.Path, false)
                        : ManagerFactory.profileManager.GetDefault();

                    if (SelectedProfile?.Guid != foregroundProfile.Guid)
                        HandleProfileApplied(foregroundProfile, UpdateSource.Background);
                }
            }
            catch { }
        }

        private void HotkeysManager_Initialized()
        {
            QueryGyroHotkey();
        }

        private void QueryGyroHotkey()
        {
            using (new LoadingScope(this))
            {
                foreach (Hotkey hotkey in ManagerFactory.hotkeysManager.GetHotkeys())
                    HotkeysManager_Updated(hotkey);
            }
        }

        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.ButtonFlags != gyroButtonFlags)
                return;

            GyroHotkey = hotkey;

            lock (_collectionLock4)
            {
                HotkeyViewModel? foundHotkey = HotkeysList.FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
                if (foundHotkey is null)
                    HotkeysList.Add(new HotkeyViewModel(hotkey));
                else
                    foundHotkey.Hotkey = hotkey;
            }

            if (ManagerFactory.hotkeysManager.Status != ManagerStatus.Initialized || isLoadingProfile || SelectedProfile is null)
                return;

            if (SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? gyroActions))
            {
                if (gyroActions is GyroActions gyroAction)
                {
                    ButtonState newButtonState = (ButtonState)hotkey.inputsChord.ButtonState.Clone();
                    if (!gyroAction.MotionTrigger.Equals(newButtonState))
                    {
                        gyroAction.MotionTrigger = newButtonState;
                        UpdateProfile();
                    }
                }
            }
        }

        private void InputsManager_StartedListening(ButtonFlags buttonFlags, InputsChordTarget chordTarget)
        {
            if (buttonFlags != gyroButtonFlags)
                return;

            HotkeyViewModel? hotkeyViewModel = HotkeysList.FirstOrDefault(h => h.Hotkey.ButtonFlags == buttonFlags);
            hotkeyViewModel?.SetListening(true, chordTarget);
        }

        private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
        {
            if (buttonFlags != gyroButtonFlags)
                return;

            HotkeyViewModel? hotkeyViewModel = HotkeysList.FirstOrDefault(h => h.Hotkey.ButtonFlags == buttonFlags);
            hotkeyViewModel?.SetListening(false, storedChord.chordTarget);
        }

        /// <summary>
        /// Debounces profile updates - waits 500ms after the last change before saving.
        /// Useful for rapid UI changes like slider movements or text input.
        /// </summary>
        public void UpdateProfile()
        {
            if (UpdateTimer.Enabled)
                UpdateTimer.Stop();
            UpdateTimer.Start();
        }

        /// <summary>
        /// Called when the LayoutTemplate is updated from the LayoutPage.
        /// Syncs layout changes back to the current profile.
        /// </summary>
        private void Template_Updated(LayoutTemplate layoutTemplate)
        {
            if (SelectedProfile is null)
                return;

            lock (SelectedProfile.SyncRoot)
            {
                SelectedProfile.LayoutTitle = layoutTemplate.Name;
                SelectedProfile.Layout.ButtonLayout = layoutTemplate.Layout.ButtonLayout;
                SelectedProfile.Layout.AxisLayout = layoutTemplate.Layout.AxisLayout;
                SelectedProfile.Layout.GyroLayout = layoutTemplate.Layout.GyroLayout;
            }

            UpdateProfile();
        }

        public void SubmitProfile(UpdateSource source = UpdateSource.ProfilesPage)
        {
            if (SelectedProfile is null)
                return;

            // Override source if called from QuickProfilesPage without explicit source
            if (source == UpdateSource.ProfilesPage && IsQuickTools)
                source = UpdateSource.QuickProfilesPage;

            ManagerFactory.profileManager.UpdateOrCreateProfile(SelectedProfile, source);
        }

        public void PowerProfileChanged(PowerProfile powerProfileAC, PowerProfile powerProfileDC)
        {
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                UIHelper.TryBeginInvoke(() =>
                {
                    SelectedPickerAC = ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == powerProfileAC.Guid);
                    SelectedPickerDC = ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == powerProfileDC.Guid);
                });
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
        }

        public void Close()
        {
            Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                selectedTemplate?.Updated -= Template_Updated;
                selectedTemplate = null;

                ManagerFactory.profileManager.Deleted -= ProfileDeleted;
                ManagerFactory.profileManager.Updated -= ProfileUpdated;
                ManagerFactory.profileManager.Applied -= ProfileApplied;
                ManagerFactory.profileManager.LastMonitorChanged -= ProfileManager_LastMonitorChanged;
                ManagerFactory.processManager.ProcessStarted -= DisplayBadges_ProcessStarted;
                ManagerFactory.processManager.ProcessStopped -= DisplayBadges_ProcessStopped;
                ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;

                if (IsQuickTools)
                {
                    ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
                    ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
                    InputsManager.StartedListening -= InputsManager_StartedListening;
                    InputsManager.StoppedListening -= InputsManager_StoppedListening;
                    ManagerFactory.hotkeysManager.Initialized -= HotkeysManager_Initialized;
                }
                else
                {
                    ManagerFactory.libraryManager.Initialized -= LibraryManager_Initialized;
                    ManagerFactory.libraryManager.StatusChanged -= LibraryManager_StatusChanged;
                    ManagerFactory.libraryManager.NetworkAvailabilityChanged -= LibraryManager_NetworkAvailabilityChanged;
                    ManagerFactory.processManager.Initialized -= ProcessManager_Initialized_Main;
                    ManagerFactory.processManager.ProcessStarted -= ProcessManager_ProcessStarted;
                    ManagerFactory.processManager.ProcessStopped -= ProcessManager_ProcessStopped;
                }

                ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
                ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
                ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
                ManagerFactory.gpuManager.Initialized -= GpuManager_Initialized;
                ManagerFactory.gpuManager.Hooked -= GPUManager_Hooked;
                ManagerFactory.gpuManager.Unhooked -= GPUManager_Unhooked;
                ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
                ManagerFactory.powerProfileManager.Updated -= PowerProfileManager_Updated;
                ManagerFactory.powerProfileManager.Deleted -= PowerProfileManager_Deleted;
                ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
                PlatformManager.RTSS.Updated -= RTSS_Updated;
                ManagerFactory.platformManager.Initialized -= PlatformManager_Initialized;
                ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
                ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

                UpdateTimer.Elapsed -= UpdateTimer_Elapsed;
                UpdateTimer.Stop();
                UpdateTimer.Dispose();

                CurrentProcessViewModel?.Dispose();
                CurrentProcessViewModel = null;

                foreach (var vm in MainProfiles)
                    vm.Dispose();
                MainProfiles.Clear();

                foreach (var vm in SubProfiles)
                    vm.Dispose();
                SubProfiles.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
