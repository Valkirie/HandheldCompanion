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
using System.Linq;
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
            set { if (value != _IsAMDGPU) { _IsAMDGPU = value; OnPropertyChanged(nameof(IsAMDGPU)); } }
        }

        private bool _HasRSRSupport;
        public bool HasRSRSupport
        {
            get => _HasRSRSupport && _GPUScalingEnabled;
            set { if (value != _HasRSRSupport) { _HasRSRSupport = value; OnPropertyChanged(nameof(HasRSRSupport)); } }
        }

        private bool _HasAFMFSupport;
        public bool HasAFMFSupport
        {
            get => _HasAFMFSupport;
            set { if (value != _HasAFMFSupport) { _HasAFMFSupport = value; OnPropertyChanged(nameof(HasAFMFSupport)); } }
        }

        private bool _HasAFMF21Support;
        public bool HasAFMF21Support
        {
            get => _HasAFMF21Support;
            set { if (value != _HasAFMF21Support) { _HasAFMF21Support = value; OnPropertyChanged(nameof(HasAFMF21Support)); } }
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
            get => _HasIntegerScalingSupport && _GPUScalingEnabled;
            set { if (value != _HasIntegerScalingSupport) { _HasIntegerScalingSupport = value; OnPropertyChanged(nameof(HasIntegerScalingSupport)); } }
        }

        private bool _HasGPUScalingSupport;
        public bool HasGPUScalingSupport
        {
            get => _HasGPUScalingSupport;
            set { if (value != _HasGPUScalingSupport) { _HasGPUScalingSupport = value; OnPropertyChanged(nameof(HasGPUScalingSupport)); } }
        }

        public bool GPUManagementEnabled => ManagerFactory.settingsManager.GetBoolean("GPUManagementEnabled");
        public bool PerformanceManagerEnabled => ManagerFactory.settingsManager.GetBoolean("PerformanceManagerEnabled");

        private bool _GPUScalingEnabled;
        public bool GPUScalingEnabled
        {
            get => _GPUScalingEnabled;
            set
            {
                if (value != _GPUScalingEnabled)
                {
                    _GPUScalingEnabled = value;
                    OnPropertyChanged(nameof(GPUScalingEnabled));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.GPUScaling != value)
                    {
                        SelectedProfile.GPUScaling = value;
                        UpdateProfile();
                    }
                }
            }
        }

        private bool _RSREnabled;
        public bool RSREnabled
        {
            get => _RSREnabled;
            set
            {
                if (value != _RSREnabled)
                {
                    _RSREnabled = value;
                    OnPropertyChanged(nameof(RSREnabled));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.RSREnabled != value)
                    {
                        SelectedProfile.RSREnabled = value;
                        UpdateProfile();
                    }
                }
            }
        }

        private double _RSRValue;
        public double RSRValue
        {
            get => _RSRValue;
            set
            {
                if (value != _RSRValue)
                {
                    _RSRValue = value;
                    OnPropertyChanged(nameof(RSRValue));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.RSRSharpness != (int)value)
                    {
                        SelectedProfile.RSRSharpness = (int)value;
                        UpdateProfile();
                    }
                }
            }
        }

        private bool _IntegerScalingEnabled;
        public bool IntegerScalingEnabled
        {
            get => _IntegerScalingEnabled;
            set
            {
                if (value != _IntegerScalingEnabled)
                {
                    _IntegerScalingEnabled = value;
                    OnPropertyChanged(nameof(IntegerScalingEnabled));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.IntegerScalingEnabled != value)
                    {
                        SelectedProfile.IntegerScalingEnabled = value;
                        UpdateProfile();
                    }
                }
            }
        }

        private bool _RISEnabled;
        public bool RISEnabled
        {
            get => _RISEnabled;
            set
            {
                if (value != _RISEnabled)
                {
                    _RISEnabled = value;
                    OnPropertyChanged(nameof(RISEnabled));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.RISEnabled != value)
                    {
                        SelectedProfile.RISEnabled = value;
                        UpdateProfile();
                    }
                }
            }
        }

        private double _RISValue;
        public double RISValue
        {
            get => _RISValue;
            set
            {
                if (value != _RISValue)
                {
                    _RISValue = value;
                    OnPropertyChanged(nameof(RISValue));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.RISSharpness != (int)value)
                    {
                        SelectedProfile.RISSharpness = (int)value;
                        UpdateProfile();
                    }
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
        /// Works correctly with sorted views — no index mapping required.
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
                    if (gyroActions is GyroActions existingGyroAction)
                    {
                        preservedMotionInput = existingGyroAction.MotionInput;
                        preservedMotionMode = existingGyroAction.MotionMode;
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
                                    MotionMode = preservedMotionMode
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
                                    MotionMode = preservedMotionMode
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
                                    MotionMode = preservedMotionMode
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

        public int IntegerScalingDividerIndex
        {
            get
            {
                if (SelectedProfile == null)
                    return 0;

                // Find the index of the ScreenDivider that matches the profile's divider value
                var matchingDivider = IntegerScalingDividers.FirstOrDefault(d => d.Divider == SelectedProfile.IntegerScalingDivider);
                return matchingDivider != null ? IntegerScalingDividers.IndexOf(matchingDivider) : 0;
            }
            set
            {
                if (SelectedProfile != null && value >= 0 && value < IntegerScalingDividers.Count)
                {
                    // Get the actual divider value from the selected index
                    int newDividerValue = IntegerScalingDividers[value].Divider;

                    if (SelectedProfile.IntegerScalingDivider != newDividerValue)
                    {
                        SelectedProfile.IntegerScalingDivider = newDividerValue;
                        OnPropertyChanged(nameof(IntegerScalingDividerIndex));

                        if (!isLoadingProfile)
                            UpdateProfile();
                    }
                }
            }
        }

        public int IntegerScalingTypeIndex
        {
            get => SelectedProfile?.IntegerScalingType ?? 0;
            set
            {
                if (SelectedProfile != null && SelectedProfile.IntegerScalingType != (byte)value)
                {
                    SelectedProfile.IntegerScalingType = (byte)value;
                    OnPropertyChanged(nameof(IntegerScalingTypeIndex));

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

        public int ScalingModeIndex
        {
            get => SelectedProfile?.ScalingMode ?? 0;
            set
            {
                if (SelectedProfile != null && SelectedProfile.ScalingMode != value)
                {
                    SelectedProfile.ScalingMode = value;
                    OnPropertyChanged(nameof(ScalingModeIndex));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public bool AFMFEnabled
        {
            get => SelectedProfile?.AFMFEnabled ?? false;
            set
            {
                if (SelectedProfile != null && SelectedProfile.AFMFEnabled != value)
                {
                    SelectedProfile.AFMFEnabled = value;
                    OnPropertyChanged(nameof(AFMFEnabled));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public int AFMFAlgorithm
        {
            get => SelectedProfile?.AFMFAlgorithm ?? 0;
            set
            {
                if (SelectedProfile != null && SelectedProfile.AFMFAlgorithm != value)
                {
                    SelectedProfile.AFMFAlgorithm = value;
                    OnPropertyChanged(nameof(AFMFAlgorithm));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public int AFMFSearchMode
        {
            get => SelectedProfile?.AFMFSearchMode ?? 0;
            set
            {
                if (SelectedProfile != null && SelectedProfile.AFMFSearchMode != value)
                {
                    SelectedProfile.AFMFSearchMode = value;
                    OnPropertyChanged(nameof(AFMFSearchMode));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public int AFMFPerformanceMode
        {
            get => SelectedProfile?.AFMFPerformanceMode ?? 0;
            set
            {
                if (SelectedProfile != null && SelectedProfile.AFMFPerformanceMode != value)
                {
                    SelectedProfile.AFMFPerformanceMode = value;
                    OnPropertyChanged(nameof(AFMFPerformanceMode));

                    if (!isLoadingProfile)
                        UpdateProfile();
                }
            }
        }

        public int AFMFFastMotionResponse
        {
            get => SelectedProfile?.AFMFFastMotionResponse ?? 0;
            set
            {
                if (SelectedProfile != null && SelectedProfile.AFMFFastMotionResponse != value)
                {
                    SelectedProfile.AFMFFastMotionResponse = value;
                    OnPropertyChanged(nameof(AFMFFastMotionResponse));

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
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.cover | LibraryType.thumbnails);
                else
                    RefreshCover(value);
            }
        }

        private void SetLibraryCoversIndex(int value)
        {
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

        private int _LibraryArtworksIndex;
        public int LibraryArtworksIndex
        {
            get => _LibraryArtworksIndex;
            set
            {
                if (value != -1)
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.artwork | LibraryType.thumbnails);
                else
                    RefreshArtwork(value);
            }
        }

        private void SetLibraryArtworksIndex(int value)
        {
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

        private int _LibraryLogosIndex;
        public int LibraryLogosIndex
        {
            get => _LibraryLogosIndex;
            set
            {
                if (value != -1)
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.logo | LibraryType.thumbnails);
                else
                    RefreshLogo(value);
            }
        }

        private void SetLibraryLogosIndex(int value)
        {
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

        public bool QuerySteamGrid { get; set; } = true;
        public bool QueryIGDB { get; set; } = true;

        // True when the currently selected library entry is a manual (file-browse) entry
        public bool IsManualEntry => _SelectedLibraryIndex >= 0
            && _SelectedLibraryIndex < LibraryPickers.Count
            && LibraryPickers[_SelectedLibraryIndex].IsManualEntry;

        // True if the dialog should be interactive: either we're online (for IGDB/SteamGrid) or a manual entry is selected
        public bool IsLibraryOrManualEnabled => IsLibraryConnected || IsManualEntry;

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

        private bool isLoadingProfile = false;
        public bool IsLoadingProfile => isLoadingProfile;

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
                _viewModel.isLoadingProfile = true;
            }

            public void Dispose()
            {
                _viewModel.isLoadingProfile = false;
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
                RequestShowLibraryDialog?.Invoke(this, EventArgs.Empty);
                RefreshLibrary.Execute(null);
            });

            RefreshLibrary = new DelegateCommand(async () =>
            {
                ClearLibrary();

                // Always add a "Manual" entry at the top so the user can browse images without an online search
                ManualEntry manualEntry;
                if (SelectedProfile?.LibraryEntry is ManualEntry existingManual)
                    manualEntry = existingManual;
                else
                    manualEntry = new ManualEntry(SelectedProfile?.Guid.GetHashCode() ?? 0L, SelectedProfile?.Name ?? string.Empty);

                lock (_collectionLock2)
                    LibraryPickers.Add(new(manualEntry));

                IEnumerable<LibraryEntry> entries = await ManagerFactory.libraryManager.GetGames(
                    (QuerySteamGrid ? LibraryFamily.SteamGrid : LibraryFamily.None) | (QueryIGDB ? LibraryFamily.IGDB : LibraryFamily.None),
                    LibrarySearchField);

                if (entries.Count() != 0)
                {
                    entries = entries.OrderByDescending(entry => entry.Family);
                    entries = entries.OrderBy(entry => entry.Name);

                    lock (_collectionLock2)
                    {
                        foreach (LibraryEntry entry in entries)
                            LibraryPickers.Add(new(entry));
                    }

                    if (SelectedProfile?.LibraryEntry is ManualEntry)
                        SelectedLibraryEntry = manualEntry;
                    else if (SelectedProfile?.LibraryEntry is not null && entries.Contains(SelectedProfile.LibraryEntry))
                        SelectedLibraryEntry = SelectedProfile.LibraryEntry;
                    else
                        SelectedLibraryEntry = ManagerFactory.libraryManager.GetGame(entries, LibrarySearchField);
                }
                else
                {
                    // No online results — select manual
                    SelectedLibraryEntry = manualEntry;
                }

                // Notify that library entries are now available
                OnPropertyChanged(nameof(HasLibraryEntry));
                OnPropertyChanged(nameof(CanApplyLibrary));
            });

            DownloadLibrary = new DelegateCommand(async () =>
            {
                int coverId = (int)(LibraryCoversIndex != -1 && LibraryCoversIndex < LibraryCovers.Count ? LibraryCovers[LibraryCoversIndex].Id : 0);
                int artworkId = (int)(LibraryArtworksIndex != -1 && LibraryArtworksIndex < LibraryArtworks.Count ? LibraryArtworks[LibraryArtworksIndex].Id : 0);
                int logoId = (int)(LibraryLogosIndex != -1 && LibraryLogosIndex < LibraryLogos.Count ? LibraryLogos[LibraryLogosIndex].Id : 0);

                if (SelectedLibraryEntry is null)
                    return;

                await ManagerFactory.libraryManager.UpdateProfileArts(SelectedProfile, SelectedLibraryEntry, coverId, artworkId, logoId);
                ManagerFactory.profileManager.UpdateOrCreateProfile(SelectedProfile, UpdateSource.LibraryUpdate);

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
            if (pickerVM.LibEntry is not ManualEntry manualEntry)
                return;

            Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dlg = new();
            if (libraryType.HasFlag(LibraryType.logo))
            {
                // Logo requires transparency — PNG only
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

            string? cachedPath = ManagerFactory.libraryManager.CopyManualArt(manualEntry.Id, libraryType, imageId, sourcePath);
            if (cachedPath is null)
                return;

            string extension = System.IO.Path.GetExtension(cachedPath);

            // Update the entry so the serialised JSON contains the cache path
            if (libraryType.HasFlag(LibraryType.cover))
                manualEntry.ManualCoverPath = cachedPath;
            else if (libraryType.HasFlag(LibraryType.artwork))
                manualEntry.ManualArtworkPath = cachedPath;
            else
                manualEntry.ManualLogoPath = cachedPath;

            // Rebuild the single visual slot. Full-res keeps the source extension; thumbnail is
            // always PNG (WriteResizedThumbnail encodes PNG regardless of source format).
            pickerVM.RefreshManualVisual(libraryType, extension, thumbnailExtension: ".png");

            if (libraryType.HasFlag(LibraryType.cover))
                RefreshCover(0);
            else if (libraryType.HasFlag(LibraryType.artwork))
                RefreshArtwork(0);
            else
                RefreshLogo(0);
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
        }

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            if (name == "GPUManagementEnabled")
                OnPropertyChanged(nameof(GPUManagementEnabled));
            if (name == "PerformanceManagerEnabled")
                OnPropertyChanged(nameof(PerformanceManagerEnabled));
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

            foreach (PowerProfile powerProfile in ManagerFactory.powerProfileManager.profiles.Values)
                PowerProfileManager_Updated(powerProfile, UpdateSource.Creation);

            // If a profile was already selected before power profiles were loaded,
            // the initial UpdatePowerProfileSelections() found ProfilePicker empty.
            // Re-apply now that it's populated.
            if (SelectedProfile != null)
            {
                UpdatePowerProfileSelections();
                UpdateSelectedPowerProfileName();
            }
        }

        private void PowerProfileManager_Initialized()
        {
            QueryPowerProfile();
        }

        private void PowerProfileManager_Deleted(PowerProfile profile)
        {
            lock (_collectionLock)
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
        }

        private void PowerProfileManager_Updated(PowerProfile profile, UpdateSource source)
        {
            lock (_collectionLock)
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
        }

        public void PowerProfile_Selected(PowerProfile powerProfile, bool AC)
        {
            if (SelectedProfile is null)
                return;

            // Don't update profile if we're loading from it (prevent circular updates)
            if (isLoadingProfile)
                return;

            switch (AC)
            {
                case false:
                    SelectedProfile.PowerProfiles[(int)PowerLineStatus.Offline] = powerProfile.Guid;
                    break;
                case true:
                    SelectedProfile.PowerProfiles[(int)PowerLineStatus.Online] = powerProfile.Guid;
                    break;
            }
            UpdateProfile();
        }

        private void MultimediaManager_Initialized()
        {
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
                GPUScalingEnabled = SelectedProfile.GPUScaling;
                RSREnabled = SelectedProfile.RSREnabled;
                RSRValue = SelectedProfile.RSRSharpness;
                IntegerScalingEnabled = SelectedProfile.IntegerScalingEnabled;
                RISEnabled = SelectedProfile.RISEnabled;
                RISValue = SelectedProfile.RISSharpness;

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
            OnPropertyChanged(nameof(IntegerScalingDividerIndex));
            OnPropertyChanged(nameof(IntegerScalingTypeIndex));
            OnPropertyChanged(nameof(ScalingModeIndex));
            OnPropertyChanged(nameof(ProfilePath));
            OnPropertyChanged(nameof(ProfileWhitelisted));
            OnPropertyChanged(nameof(AFMFEnabled));
            OnPropertyChanged(nameof(AFMFAlgorithm));
            OnPropertyChanged(nameof(AFMFSearchMode));
            OnPropertyChanged(nameof(AFMFPerformanceMode));
            OnPropertyChanged(nameof(AFMFFastMotionResponse));
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
            LibraryArtworksIndex = -1;
            LibraryArtworksIndex = 0;
            LibraryCoversIndex = -1;
            LibraryCoversIndex = 0;
            LibraryLogosIndex = -1;
            LibraryLogosIndex = 0;
            OnPropertyChanged(nameof(IsManualEntry));
            OnPropertyChanged(nameof(IsLibraryOrManualEnabled));
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
                OnPropertyChanged(nameof(LibraryCovers));
                SetLibraryCoversIndex(index);
            }
            catch { }
        }

        private void RefreshArtwork(int index)
        {
            try
            {
                OnPropertyChanged(nameof(LibraryArtworks));
                SetLibraryArtworksIndex(index);
            }
            catch { }
        }

        private void RefreshLogo(int index)
        {
            try
            {
                OnPropertyChanged(nameof(LibraryLogos));
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

            SelectedProfile.LayoutTitle = layoutTemplate.Name;

            SelectedProfile.Layout.ButtonLayout = layoutTemplate.Layout.ButtonLayout;
            SelectedProfile.Layout.AxisLayout = layoutTemplate.Layout.AxisLayout;
            SelectedProfile.Layout.GyroLayout = layoutTemplate.Layout.GyroLayout;

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
            UIHelper.TryBeginInvoke(() =>
            {
                lock (_collectionLock)
                {
                    SelectedPickerAC = ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == powerProfileAC.Guid);
                    SelectedPickerDC = ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == powerProfileDC.Guid);
                }
            });
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
