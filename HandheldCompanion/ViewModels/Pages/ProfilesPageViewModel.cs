using HandheldCompanion.Actions;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
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
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public ObservableCollection<Profile> MainProfiles { get; } = [];

        // Sub-profiles collection for cb_SubProfilePicker ComboBox
        public ObservableCollection<Profile> SubProfiles { get; } = [];

        private ObservableCollection<ProfilesPickerViewModel> ProfilePicker = [];
        public ListCollectionView ProfilePickerCollectionViewAC { get; set; }
        public ListCollectionView ProfilePickerCollectionViewDC { get; set; }

        public ObservableCollection<LibraryEntryViewModel> LibraryPickers { get; } = [];
        public ObservableCollection<WindowListItemViewModel> AllWindows { get; } = [];
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        // ComboBox collections
        public ObservableCollection<ScreenFramelimitViewModel> FramerateLimits { get; } = [];
        public ObservableCollection<ScreenDividerViewModel> IntegerScalingDividers { get; } = [];

        public bool HasAnyWindows => AllWindows.Any();

        // True if library search results are available in LibraryPickers (for enabling ComboBox and showing preview)
        public bool HasLibraryEntry => LibraryPickers.Any();

        // True if library manager has network connectivity (for enabling library features)
        public bool IsLibraryConnected => ManagerFactory.libraryManager.IsConnected;

        // True if library manager is busy downloading/searching (for showing ProgressRing in dialog)
        public bool IsLibraryBusy => ManagerFactory.libraryManager.Status.HasFlag(ManagerStatus.Busy);

        public bool HasProfileExecutables => SelectedProfile?.Executables.Any() ?? false;

        // True if a profile is selected (not null) - used to enable/disable the entire page
        public bool HasSelectedProfile => SelectedProfile != null;

        // True if the selected profile can be renamed/deleted (not Default)
        public bool IsProfileManagementEnabled => SelectedMainProfile != null && !SelectedMainProfile.Default;

        // True if the ProfileEnabled toggle can be modified (not Default profile)
        public bool IsProfileEnabledToggleEnabled => SelectedProfile != null && !SelectedProfile.Default;

        private readonly bool IsQuickTools;
        private ProfilesPage profilesPage;
        private QuickProfilesPage quickProfilesPage;
        private Timer UpdateTimer;
        private ProcessEx currentProcess;
        private ProcessEx selectedProcess;
        private Hotkey GyroHotkey;
        private LayoutTemplate selectedTemplate;

        #region Profile
        private Profile _selectedProfile;
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
                if (_selectedProfile != value)
                {
                    using (new LoadingScope(this))
                    {
                        _selectedProfile = value;
                        OnPropertyChanged(nameof(SelectedProfile));
                        OnPropertyChanged(nameof(HasSelectedProfile));
                        OnPropertyChanged(nameof(CanLaunchProcess));
                        OnPropertyChanged(nameof(CanKillProcess));
                        OnPropertyChanged(nameof(IsProfileProcessRunning));
                        OnPropertyChanged(nameof(CanToggleProfileProcess));
                        OnProfileChanged();
                    }
                }
            }
        }

        private Profile _selectedMainProfile;
        public Profile SelectedMainProfile
        {
            get => _selectedMainProfile;
            set
            {
                if (_selectedMainProfile != value)
                {
                    _selectedMainProfile = value;
                    OnPropertyChanged(nameof(SelectedMainProfile));
                    OnPropertyChanged(nameof(IsProfileManagementEnabled));
                    OnPropertyChanged(nameof(CanLaunchProcess));
                    OnPropertyChanged(nameof(CanKillProcess));
                    OnPropertyChanged(nameof(IsProfileProcessRunning));
                    OnPropertyChanged(nameof(CanToggleProfileProcess));
                    UpdateSubProfiles();
                }
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
            get => _HasRSRSupport;
            set { if (value != _HasRSRSupport) { _HasRSRSupport = value; OnPropertyChanged(nameof(HasRSRSupport)); } }
        }

        private bool _HasAFMFSupport;
        public bool HasAFMFSupport
        {
            get => _HasAFMFSupport;
            set { if (value != _HasAFMFSupport) { _HasAFMFSupport = value; OnPropertyChanged(nameof(HasAFMFSupport)); } }
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
            set { if (value != _HasIntegerScalingSupport) { _HasIntegerScalingSupport = value; OnPropertyChanged(nameof(HasIntegerScalingSupport)); } }
        }

        private bool _HasGPUScalingSupport;
        public bool HasGPUScalingSupport
        {
            get => _HasGPUScalingSupport;
            set { if (value != _HasGPUScalingSupport) { _HasGPUScalingSupport = value; OnPropertyChanged(nameof(HasGPUScalingSupport)); } }
        }

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

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && SelectedProfile.Enabled != value)
                    {
                        SelectedProfile.Enabled = value;
                        UpdateProfile();
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

        private System.Windows.Media.ImageSource _ProcessIcon;
        public System.Windows.Media.ImageSource ProcessIcon
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
                        Profile newSelectedProfile = SubProfiles[value];
                        ManagerFactory.profileManager.UpdateOrCreateProfile(newSelectedProfile,
                            IsQuickTools ? UpdateSource.QuickProfilesPage : UpdateSource.ProfilesPage);
                    }
                }
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
                            break;
                        case MotionOutput.LeftStick:
                            if (gyroActions is not AxisActions)
                            {
                                gyroActions = new AxisActions()
                                {
                                    AxisAntiDeadZone = GyroActions.DefaultAxisAntiDeadZone,
                                    Axis = AxisLayoutFlags.LeftStick,
                                    MotionTrigger = GyroHotkey.inputsChord.ButtonState.Clone() as ButtonState,
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
                                    MotionTrigger = GyroHotkey.inputsChord.ButtonState.Clone() as ButtonState,
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
                                    MotionTrigger = GyroHotkey.inputsChord.ButtonState.Clone() as ButtonState,
                                    MotionInput = preservedMotionInput,
                                    MotionMode = preservedMotionMode
                                };
                            }
                            break;
                    }

                    if (gyroActions is not null)
                        SelectedProfile.Layout.UpdateLayout(AxisLayoutFlags.Gyroscope, gyroActions);

                    SubmitProfile(UpdateSource.Creation);
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
                    HIDmode.DInputController => 3,
                    _ => 0 // NotSelected
                };
            }
            set
            {
                HIDmode mode = value switch
                {
                    1 => HIDmode.Xbox360Controller,
                    2 => HIDmode.DualShock4Controller,
                    3 => HIDmode.DInputController,
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

        private ScreenFramelimitViewModel _SelectedFrameLimit;
        public ScreenFramelimitViewModel SelectedFrameLimit
        {
            get => _SelectedFrameLimit;
            set
            {
                if (value != _SelectedFrameLimit)
                {
                    _SelectedFrameLimit = value;
                    OnPropertyChanged(nameof(SelectedFrameLimit));

                    // Only write back to profile if we're not loading from it
                    if (!isLoadingProfile && SelectedProfile != null && value != null && value.FrameLimit.limit != SelectedProfile.FramerateValue)
                    {
                        SelectedProfile.FramerateValue = value.FrameLimit.limit;
                        UpdateProfile();
                    }
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
        private string _LibrarySearchField;
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

        private LibraryEntry _SelectedLibraryEntry;
        public LibraryEntry SelectedLibraryEntry
        {
            get => _SelectedLibraryEntry;
            set
            {
                if (_SelectedLibraryEntry != value)
                {
                    _SelectedLibraryEntry = value;
                    if (value != null)
                        _SelectedLibraryIndex = LibraryPickers.IndexOf(LibraryPickers.FirstOrDefault(p => p.Id == value.Id));
                    else
                        _SelectedLibraryIndex = -1;

                    OnPropertyChanged(nameof(SelectedLibraryEntry));
                    OnPropertyChanged(nameof(SelectedLibraryIndex));
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
                    if (value >= 0 && value < LibraryPickers.Count)
                        _SelectedLibraryEntry = LibraryPickers[value].LibEntry;
                    else
                        _SelectedLibraryEntry = null;

                    OnPropertyChanged(nameof(SelectedLibraryEntry));
                    OnPropertyChanged(nameof(SelectedLibraryIndex));
                    SelectedLibraryChanged();
                }
            }
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
        private PowerProfile _selectedPresetDC;
        public PowerProfile SelectedPresetDC
        {
            get => _selectedPresetDC;
            set
            {
                if (_selectedPresetDC != value)
                {
                    _selectedPresetDC = value;
                    ProfilesPickerViewModel profilesPickerViewModel = ProfilePicker.First(p => p.LinkedPresetId == _selectedPresetDC.Guid);
                    _selectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(profilesPickerViewModel);

                    // Only update profile if we're not loading from it
                    if (!isLoadingProfile)
                        PowerProfile_Selected(_selectedPresetDC, false);

                    OnPropertyChanged(nameof(SelectedPresetDC));
                    OnPropertyChanged(nameof(SelectedPresetIndexDC));
                }
            }
        }

        private int _selectedPresetIndexDC;
        public int SelectedPresetIndexDC
        {
            get => _selectedPresetIndexDC;
            set
            {
                if (value != _selectedPresetIndexDC && value >= 0 && value < ProfilePickerCollectionViewDC.Count)
                {
                    _selectedPresetIndexDC = value;
                    ProfilesPickerViewModel profilesPickerViewModel = ProfilePickerCollectionViewDC.GetItemAt(_selectedPresetIndexDC) as ProfilesPickerViewModel;
                    SelectedPresetDC = ManagerFactory.powerProfileManager.GetProfile(profilesPickerViewModel.LinkedPresetId.Value);
                    OnPropertyChanged(nameof(SelectedPresetIndexDC));
                }
            }
        }

        private PowerProfile _selectedPresetAC;
        public PowerProfile SelectedPresetAC
        {
            get => _selectedPresetAC;
            set
            {
                if (_selectedPresetAC != value)
                {
                    _selectedPresetAC = value;
                    ProfilesPickerViewModel profilesPickerViewModel = ProfilePicker.First(p => p.LinkedPresetId == _selectedPresetAC.Guid);
                    _selectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(profilesPickerViewModel);

                    // Only update profile if we're not loading from it
                    if (!isLoadingProfile)
                        PowerProfile_Selected(_selectedPresetAC, true);

                    OnPropertyChanged(nameof(SelectedPresetAC));
                    OnPropertyChanged(nameof(SelectedPresetIndexAC));
                }
            }
        }

        private int _selectedPresetIndexAC;
        public int SelectedPresetIndexAC
        {
            get => _selectedPresetIndexAC;
            set
            {
                if (value != _selectedPresetIndexAC && value >= 0 && value < ProfilePickerCollectionViewAC.Count)
                {
                    _selectedPresetIndexAC = value;
                    ProfilesPickerViewModel profilesPickerViewModel = ProfilePickerCollectionViewAC.GetItemAt(_selectedPresetIndexAC) as ProfilesPickerViewModel;
                    SelectedPresetAC = ManagerFactory.powerProfileManager.GetProfile(profilesPickerViewModel.LinkedPresetId.Value);
                    OnPropertyChanged(nameof(SelectedPresetIndexAC));
                }
            }
        }
        #endregion

        #region Process Control
        private ProcessExViewModel _CurrentProcessViewModel;
        /// <summary>
        /// Tracks the currently running process for the selected profile (ProfilesPage) or foreground app (QuickProfilesPage).
        /// 
        /// Setting this property triggers notifications for:
        /// - Can* properties (CanLaunchProcess, CanSuspendProcess, etc.)
        /// - Command properties (SuspendProcessCommand, ResumeProcessCommand, KillProcessCommand)
        /// 
        /// This ensures MenuItem bindings update correctly.
        /// </summary>
        public ProcessExViewModel CurrentProcessViewModel
        {
            get => _CurrentProcessViewModel;
            private set
            {
                if (_CurrentProcessViewModel != value)
                {
                    // Unsubscribe from old ViewModel
                    if (_CurrentProcessViewModel != null)
                        _CurrentProcessViewModel.PropertyChanged -= CurrentProcessViewModel_PropertyChanged;

                    _CurrentProcessViewModel = value;

                    // Subscribe to new ViewModel
                    if (_CurrentProcessViewModel != null)
                        _CurrentProcessViewModel.PropertyChanged += CurrentProcessViewModel_PropertyChanged;

                    OnPropertyChanged(nameof(CurrentProcessViewModel));

                    // Notify that the Can* properties changed
                    OnPropertyChanged(nameof(CanLaunchProcess));
                    OnPropertyChanged(nameof(CanSuspendProcess));
                    OnPropertyChanged(nameof(CanResumeProcess));
                    OnPropertyChanged(nameof(CanKillProcess));
                    OnPropertyChanged(nameof(IsProfileProcessRunning));
                    OnPropertyChanged(nameof(CanToggleProfileProcess));

                    OnPropertyChanged(nameof(KillProcessCommand));
                }
            }
        }
        #endregion

        private void CurrentProcessViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProcessExViewModel.IsRunning):
                    OnPropertyChanged(nameof(CanLaunchProcess));
                    OnPropertyChanged(nameof(CanKillProcess));
                    OnPropertyChanged(nameof(IsProfileProcessRunning));
                    OnPropertyChanged(nameof(CanToggleProfileProcess));
                    break;
                case nameof(ProcessExViewModel.IsSuspended):
                    OnPropertyChanged(nameof(CanSuspendProcess));
                    OnPropertyChanged(nameof(CanResumeProcess));
                    break;
            }
        }

        public bool IsProfileProcessRunning => CurrentProcessViewModel?.IsRunning == true;
        public bool CanToggleProfileProcess => CanLaunchProcess || CanKillProcess;
        public bool CanLaunchProcess => SelectedProfile != null && !string.IsNullOrEmpty(SelectedProfile.Path) && CurrentProcessViewModel?.IsRunning != true;
        public bool CanSuspendProcess => CurrentProcessViewModel?.CanSuspend == true;
        public bool CanResumeProcess => CurrentProcessViewModel?.CanResume == true;
        public bool CanKillProcess => CurrentProcessViewModel?.IsRunning == true;

        // Delegate process commands to CurrentProcessViewModel, except Launch which uses Profile
        public ICommand SuspendProcessCommand => CurrentProcessViewModel?.SuspendProcessCommand;
        public ICommand ResumeProcessCommand => CurrentProcessViewModel?.ResumeProcessCommand;
        public ICommand KillProcessCommand => CurrentProcessViewModel?.KillProcessCommand;

        // Events for View to handle UI-specific operations
        public event EventHandler RequestCreateProfile;
        public event EventHandler<Profile> RequestDeleteProfile;
        public event EventHandler<Profile> RequestRenameProfile;
        public event EventHandler RequestCreateSubProfile;
        public event EventHandler<Profile> RequestDeleteSubProfile;
        public event EventHandler<Profile> RequestRenameSubProfile;
        public event EventHandler<LayoutTemplate> RequestOpenControllerLayout;
        public event EventHandler<PowerProfile> RequestOpenPowerProfile;
        public event EventHandler RequestOpenProfilePage;
        public event EventHandler RequestOpenProfileLayout;
        public event EventHandler RequestCreatePowerProfile;
        public event EventHandler RequestOpenAdditionalSettings;

        // Commands
        public ICommand RefreshLibrary { get; private set; }
        public ICommand DisplayLibrary { get; private set; }
        public ICommand DownloadLibrary { get; private set; }
        public ICommand LaunchExecutable { get; private set; }
        public ICommand ToggleProfileProcessCommand { get; private set; }
        public ICommand LaunchProfileProcessCommand { get; private set; }
        public ICommand AddProfileExecutable { get; private set; }
        public ICommand RemoveProfileExecutable { get; private set; }
        public ICommand CreateProfileCommand { get; private set; }
        public ICommand DeleteProfileCommand { get; private set; }
        public ICommand RenameProfileCommand { get; private set; }
        public ICommand ToggleFavoriteCommand { get; private set; }
        public ICommand CreateSubProfileCommand { get; private set; }
        public ICommand DeleteSubProfileCommand { get; private set; }
        public ICommand RenameSubProfileCommand { get; private set; }
        public ICommand OpenControllerLayoutCommand { get; private set; }
        public ICommand OpenPowerProfileOnBatteryCommand { get; private set; }
        public ICommand OpenPowerProfilePluggedCommand { get; private set; }
        public ICommand OpenProfilePageCommand { get; private set; }
        public ICommand OpenProfileLayoutCommand { get; private set; }
        public ICommand CreatePowerProfileCommand { get; private set; }
        public ICommand OpenAdditionalSettingsCommand { get; private set; }

        private ContentDialog contentDialog;

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

        /// <summary>
        /// Safely updates SubProfiles collection without triggering WPF binding side effects.
        /// Sets temporary selection to prevent SelectedProfile from being auto-nulled.
        /// </summary>
        private void SafeUpdateSubProfiles(IEnumerable<Profile> newProfiles, Profile profileToSelect)
        {
            SubProfiles.Clear();
            foreach (var profile in newProfiles)
                SubProfiles.Add(profile);
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
            BindingOperations.EnableCollectionSynchronization(HotkeysList, _collectionLock3);
            BindingOperations.EnableCollectionSynchronization(MainProfiles, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(SubProfiles, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(FramerateLimits, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(IntegerScalingDividers, _collectionLock);

            ProfilePickerCollectionViewDC = new ListCollectionView(ProfilePicker);
            ProfilePickerCollectionViewDC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));
            ProfilePickerCollectionViewAC = new ListCollectionView(ProfilePicker);
            ProfilePickerCollectionViewAC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));

            ProfileExecutables.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(HasProfileExecutables));
            };

            SetupCommands();
            SetupManagerEvents();
        }

        private void InitializePageSpecific()
        {
            ManagerFactory.profileManager.Deleted += ProfileDeleted;
            ManagerFactory.profileManager.Updated += ProfileUpdated;
            ManagerFactory.profileManager.Applied += ProfileApplied;
            ManagerFactory.profileManager.Initialized += ProfileManagerLoaded;

            if (IsQuickTools)
            {
                ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
                ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;

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
                profileViewModel.StartProcessCommand.Execute(runAsAdmin);
            });

            LaunchProfileProcessCommand = new DelegateCommand(() =>
            {
                if (SelectedProfile is null || string.IsNullOrEmpty(SelectedProfile.Path))
                    return;

                try
                {
                    ProfileViewModel profileViewModel = new(SelectedProfile, false);
                    profileViewModel.StartProcessCommand.Execute(false);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to launch profile process: {0}", ex.Message);
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
                if (profilesPage?.IGGBDialog == null)
                    return;

                ContentDialog storedDialog = profilesPage.IGGBDialog;
                object content = storedDialog.Content;

                contentDialog = new ContentDialog
                {
                    Title = storedDialog.Title,
                    CloseButtonText = storedDialog.CloseButtonText,
                    PrimaryButtonText = storedDialog.PrimaryButtonText,
                    IsEnabled = storedDialog.IsEnabled,
                    Content = content,
                    DataContext = this,
                };

                contentDialog.ShowAsync();
                RefreshLibrary.Execute(null);
            });

            RefreshLibrary = new DelegateCommand(async () =>
            {
                ClearLibrary();

                IEnumerable<LibraryEntry> entries = await ManagerFactory.libraryManager.GetGames(
                    (QuerySteamGrid ? LibraryFamily.SteamGrid : LibraryFamily.None) | (QueryIGDB ? LibraryFamily.IGDB : LibraryFamily.None),
                    LibrarySearchField);

                if (entries.Count() != 0)
                {
                    entries = entries.OrderByDescending(entry => entry.Family);
                    entries = entries.OrderBy(entry => entry.Name);

                    foreach (LibraryEntry entry in entries)
                        LibraryPickers.SafeAdd(new(entry));

                    if (SelectedProfile?.LibraryEntry is not null && entries.Contains(SelectedProfile.LibraryEntry))
                        SelectedLibraryEntry = SelectedProfile.LibraryEntry;
                    else
                        SelectedLibraryEntry = ManagerFactory.libraryManager.GetGame(entries, LibrarySearchField);
                }

                // Notify that library entries are now available
                OnPropertyChanged(nameof(HasLibraryEntry));
            });

            DownloadLibrary = new DelegateCommand(async () =>
            {
                int coverId = (int)LibraryCovers[LibraryCoversIndex].Id;
                int artworkId = (int)LibraryArtworks[LibraryArtworksIndex].Id;
                int logoId = (int)LibraryLogos[LibraryLogosIndex].Id;

                await ManagerFactory.libraryManager.UpdateProfileArts(SelectedProfile, SelectedLibraryEntry, coverId, artworkId, logoId);
                contentDialog?.Hide();
                contentDialog = null;
                ManagerFactory.profileManager.UpdateOrCreateProfile(SelectedProfile, UpdateSource.LibraryUpdate);

                // Refresh the Cover and Artwork properties to display the newly downloaded images
                OnPropertyChanged(nameof(Cover));
                OnPropertyChanged(nameof(Artwork));
                OnPropertyChanged(nameof(Logo));
            });

            AddProfileExecutable = new DelegateCommand<object>(async param =>
            {
                string path = string.Empty;
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
                    if (selectedTemplate is not null)
                    {
                        selectedTemplate.Updated -= Template_Updated;
                        selectedTemplate = null;
                    }

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
                int idx = ManagerFactory.powerProfileManager.profiles.Values.Where(p => !p.IsDefault()).Count() + 1;
                string Name = string.Format(Properties.Resources.PowerProfileManualName, idx);
                PowerProfile powerProfile = new PowerProfile(Name, Properties.Resources.PowerProfileManualDescription)
                {
                    TDPOverrideValues = IDevice.GetCurrent().nTDP
                };

                ManagerFactory.powerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);
                RequestCreatePowerProfile?.Invoke(this, EventArgs.Empty);
            });

            OpenAdditionalSettingsCommand = new DelegateCommand(() =>
            {
                RequestOpenAdditionalSettings?.Invoke(this, EventArgs.Empty);
            });
        }

        private void SetupManagerEvents()
        {
            ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;
            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
            ManagerFactory.gpuManager.Hooked += GPUManager_Hooked;
            ManagerFactory.gpuManager.Unhooked += GPUManager_Unhooked;
            ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;

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
            UIHelper.TryInvoke(() =>
            {
                // QuickTools: show the currently applied power profile name
                // ProfilesPage: show the selected profile's power profile for the current AC/DC state
                if (IsQuickTools)
                    SelectedPowerProfileName = profile.Name;
                else
                    UpdateSelectedPowerProfileName();
            });
        }

        private void QueryPowerProfile()
        {
            ManagerFactory.powerProfileManager.Updated += PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted += PowerProfileManager_Deleted;

            UIHelper.TryInvoke(() =>
            {
                foreach (PowerProfile powerProfile in ManagerFactory.powerProfileManager.profiles.Values)
                    PowerProfileManager_Updated(powerProfile, UpdateSource.Creation);
            });
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
                        SelectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty));
                    if (SelectedPresetDC?.Guid == foundPreset.LinkedPresetId)
                        SelectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty));
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

            UIHelper.TryInvoke(() =>
            {
                switch (AC)
                {
                    case false:
                        SelectedProfile.PowerProfiles[(int)PowerLineStatus.Offline] = powerProfile.Guid;
                        break;
                    case true:
                        SelectedProfile.PowerProfiles[(int)PowerLineStatus.Online] = powerProfile.Guid;
                        break;
                }
            });
            UpdateProfile();
        }

        private void MultimediaManager_Initialized()
        {
            UIHelper.TryBeginInvoke(() =>
            {
                try
                {
                    DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                    if (desktopScreen is not null)
                    {
                        IntegerScalingDividers.Clear();
                        foreach (var screenDivider in desktopScreen.screenDividers)
                            IntegerScalingDividers.Add(new ScreenDividerViewModel(screenDivider));
                    }
                }
                catch { }
            });
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
            }

            GPU.IntegerScalingChanged -= OnIntegerScalingChanged;
            GPU.GPUScalingChanged -= OnGPUScalingChanged;

            IsAMDGPU = false;
            HasRSRSupport = false;
            HasAFMFSupport = false;
            HasGPUScalingSupport = false;
            HasIntegerScalingSupport = false;
            HasScalingModeSupport = false;
        }

        private void UpdateGraphicsSettingsUI()
        {
            UIHelper.TryInvoke(() =>
            {
                OnPropertyChanged(nameof(HasRSRSupport));
                OnPropertyChanged(nameof(HasAFMFSupport));
                OnPropertyChanged(nameof(HasGPUScalingSupport));
                OnPropertyChanged(nameof(HasIntegerScalingSupport));
                OnPropertyChanged(nameof(HasScalingModeSupport));
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
            UIHelper.TryInvoke(() =>
            {
                IsRTSSReady = status == PlatformStatus.Ready || status == PlatformStatus.Started;
            });
        }

        private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
        {
            try
            {
                List<ScreenFramelimit> frameLimits = desktopScreen.GetFramelimits();

                UIHelper.TryInvoke(() =>
                {
                    // Store the current selection before clearing (if any)
                    int? currentSelectedLimit = SelectedFrameLimit?.FrameLimit.limit;

                    FramerateLimits.Clear();
                    foreach (ScreenFramelimit frameLimit in frameLimits)
                        FramerateLimits.Add(new ScreenFramelimitViewModel(frameLimit));

                    // Restore the selection if we had one
                    if (currentSelectedLimit.HasValue && FramerateLimits.Any())
                    {
                        var matchingLimit = FramerateLimits.FirstOrDefault(vm => vm.FrameLimit.limit == currentSelectedLimit.Value);
                        if (matchingLimit != null)
                            SelectedFrameLimit = matchingLimit;
                    }
                });
            }
            catch { }
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
            OnPropertyChanged(nameof(IsLibraryBusy));
        }

        private void LibraryManager_NetworkAvailabilityChanged(bool status)
        {
            OnPropertyChanged(nameof(IsLibraryConnected));
        }

        private void LibraryManager_StatusChanged(ManagerStatus status)
        {
            OnPropertyChanged(nameof(IsLibraryBusy));
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
                lock (_collectionLock)
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

                        Profile mainProfile = ManagerFactory.profileManager.GetParent(profile);

                        if (SelectedMainProfile?.Guid != mainProfile.Guid)
                        {
                            _selectedMainProfile = mainProfile;
                            OnPropertyChanged(nameof(SelectedMainProfile));
                            OnPropertyChanged(nameof(IsProfileManagementEnabled));
                        }

                        IEnumerable<Profile> subProfiles = ManagerFactory.profileManager.GetSubProfilesFromProfile(mainProfile, true);
                        int selectedIndex = subProfiles.Select((p, i) => new { p, i })
                            .FirstOrDefault(x => x.p.Guid == profile.Guid)?.i ?? 0;

                        SafeUpdateSubProfiles(subProfiles, profile);

                        if (_SelectedSubProfileIndex != selectedIndex)
                        {
                            _SelectedSubProfileIndex = selectedIndex;
                            OnPropertyChanged(nameof(SelectedSubProfileIndex));
                        }

                        SelectedProfile = profile;

                        if (IsQuickTools && profile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? currentAction))
                        {
                            if (currentAction is GyroActions gyroActions && gyroActions.MotionTrigger != null)
                            {
                                GyroHotkey.inputsChord.ButtonState = gyroActions.MotionTrigger.Clone() as ButtonState;
                                ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(GyroHotkey, UpdateSource.Background);
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
            isCurrent = SelectedProfile?.Guid == profile?.Guid;
            isCurrent |= source.HasFlag(UpdateSource.Creation);

            if (source == UpdateSource.QuickProfilesPage && !isCurrent)
                return;

            UIHelper.TryInvoke(() =>
            {
                if (!profile.IsSubProfile)
                {
                    var existingProfile = MainProfiles.FirstOrDefault(p => p.Guid == profile.Guid);
                    if (existingProfile == null)
                    {
                        MainProfiles.Add(profile);

                        if (source.HasFlag(UpdateSource.Creation) && SelectedMainProfile == null)
                        {
                            SelectedMainProfile = profile;
                        }
                        return;
                    }

                    if (SelectedMainProfile?.Guid == profile.Guid)
                    {
                        _selectedMainProfile = profile;
                        OnPropertyChanged(nameof(SelectedMainProfile));
                    }
                }
                else
                {
                    var existingSubProfile = SubProfiles.FirstOrDefault(p => p.Guid == profile.Guid);
                    if (existingSubProfile == null && SelectedMainProfile != null && profile.ParentGuid == SelectedMainProfile.Guid)
                    {
                        SubProfiles.Add(profile);
                        return;
                    }
                }

                if (SelectedProfile?.Guid == profile.Guid)
                {
                    _selectedProfile = profile;
                    OnPropertyChanged(nameof(SelectedProfile));
                    OnPropertyChanged(nameof(HasSelectedProfile));
                    OnPropertyChanged(nameof(CanLaunchProcess));
                    OnPropertyChanged(nameof(CanKillProcess));
                    OnPropertyChanged(nameof(IsProfileProcessRunning));
                    OnPropertyChanged(nameof(CanToggleProfileProcess));

                    if (profile.IsSubProfile && !IsQuickTools)
                    {
                        int subProfileIndex = SubProfiles.IndexOf(SubProfiles.FirstOrDefault(p => p.Guid == profile.Guid));
                        if (subProfileIndex >= 0 && subProfileIndex != _SelectedSubProfileIndex)
                        {
                            _SelectedSubProfileIndex = subProfileIndex;
                            OnPropertyChanged(nameof(SelectedSubProfileIndex));
                        }
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
                    var existingProfile = MainProfiles.FirstOrDefault(p => p.Guid == profile.Guid);
                    if (existingProfile != null)
                    {
                        MainProfiles.Remove(existingProfile);
                    }

                    if (SelectedMainProfile?.Guid == profile.Guid)
                    {
                        SelectedMainProfile = ManagerFactory.profileManager.GetDefault();
                    }
                }
                else
                {
                    if (SelectedProfile?.Guid == profile.Guid && SelectedMainProfile != null)
                    {
                        SelectedProfile = SelectedMainProfile;
                    }

                    var existingSubProfile = SubProfiles.FirstOrDefault(p => p.Guid == profile.Guid);
                    if (existingSubProfile != null)
                    {
                        SubProfiles.Remove(existingSubProfile);
                    }
                }

                if (IsQuickTools && SelectedProfile == profile)
                {
                    SelectedProfile = ManagerFactory.profileManager.GetDefault();
                }
            });
        }

        private void ProfileManagerLoaded()
        {
            UIHelper.TryInvoke(() =>
            {
                MainProfiles.Clear();
                var profiles = ManagerFactory.profileManager.GetProfiles(false);
                foreach (var profile in profiles.OrderBy(p => p.Name))
                    MainProfiles.Add(profile);

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

            AllWindows.SafeClear();
            if (SelectedProfile != null)
            {
                foreach (var kvp in SelectedProfile.WindowsSettings)
                    AllWindows.SafeAdd(new WindowListItemViewModel(kvp.Key, kvp.Value));
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
            ProfileExecutables.SafeClear();
            if (SelectedProfile != null)
            {
                foreach (string path in SelectedProfile.Executables)
                    ProfileExecutables.SafeAdd(path);

                var idx = SelectedProfile.Executables.IndexOf(SelectedProfile.Path);
                if (ProfileExecutables.Count > 0 && idx == -1) idx = 0;
                ProfileExecutablesIdx = (ProfileExecutables.Count == 0) ? -1 : Math.Min(idx, ProfileExecutables.Count - 1);
            }
        }

        private void UpdateCurrentProcessViewModel()
        {
            ProcessEx profileProcess = null;
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
                UIHelper.TryInvoke(() =>
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

                    ProfileEnabled = SelectedProfile.Enabled;
                    ProfileArguments = SelectedProfile.Arguments;
                    ProfileLaunchString = SelectedProfile.LaunchString;

                    if (FramerateLimits.Any())
                    {
                        var desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                        if (desktopScreen != null)
                        {
                            ScreenFramelimit closest = desktopScreen.GetClosest(SelectedProfile.FramerateValue);
                            SelectedFrameLimit = FramerateLimits.FirstOrDefault(vm => vm.FrameLimit.limit == closest.limit);
                        }
                    }

                    UpdatePowerProfileIndices();
                    UpdateSelectedPowerProfileName();
                    UpdateControlsEnabledState();
                    NotifyWrapperProperties();
                });
            }
        }

        /// <summary>
        /// Updates power profile indices from the selected profile.
        /// Separated to reduce complexity in UpdateUI.
        /// </summary>
        private void UpdatePowerProfileIndices()
        {
            if (SelectedProfile.PowerProfiles.ContainsKey((int)PowerLineStatus.Offline))
            {
                Guid offlineGuid = SelectedProfile.PowerProfiles[(int)PowerLineStatus.Offline];
                var pickerViewModel = ProfilePicker.FirstOrDefault(p => p.LinkedPresetId == offlineGuid);
                if (pickerViewModel != null)
                {
                    _selectedPresetDC = ManagerFactory.powerProfileManager.GetProfile(offlineGuid);
                    _selectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(pickerViewModel);
                    OnPropertyChanged(nameof(SelectedPresetDC));
                    OnPropertyChanged(nameof(SelectedPresetIndexDC));
                }
            }

            if (SelectedProfile.PowerProfiles.ContainsKey((int)PowerLineStatus.Online))
            {
                Guid onlineGuid = SelectedProfile.PowerProfiles[(int)PowerLineStatus.Online];
                var pickerViewModel = ProfilePicker.FirstOrDefault(p => p.LinkedPresetId == onlineGuid);
                if (pickerViewModel != null)
                {
                    _selectedPresetAC = ManagerFactory.powerProfileManager.GetProfile(onlineGuid);
                    _selectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(pickerViewModel);
                    OnPropertyChanged(nameof(SelectedPresetAC));
                    OnPropertyChanged(nameof(SelectedPresetIndexAC));
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
        }

        private void UpdateSubProfiles(Profile updatedProfile = null)
        {
            if (SelectedMainProfile is null)
                return;

            lock (_collectionLock)
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

                    UIHelper.TryInvoke(() =>
                    {
                        SafeUpdateSubProfiles(profiles, profileToSelect);
                        SelectedProfile = profileToSelect;
                    });
                }
                catch { }
            }
        }

        private void SelectedProcess_WindowAttached_Merged(ProcessWindow processWindow)
        {
            var item = AllWindows.FirstOrDefault(w => w.Hwnd == processWindow.Hwnd && w.Hwnd != 0);
            if (item is null)
                AllWindows.SafeAdd(item = new WindowListItemViewModel(processWindow));
            else
                item.UpdateFrom(processWindow);

            OnPropertyChanged(nameof(HasAnyWindows));
        }

        private void SelectedProcess_WindowDetached_Merged(ProcessWindow processWindow)
        {
            var item = AllWindows.FirstOrDefault(w => w.Hwnd == processWindow.Hwnd);
            if (item != null)
                item.ProcessWindow = null;

            OnPropertyChanged(nameof(HasAnyWindows));
        }

        private void ClearWindows()
        {
            AllWindows.SafeClear();

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
            LibraryPickers.SafeClear();

            // Notify that library entries have been cleared
            OnPropertyChanged(nameof(HasLibraryEntry));
        }

        private void SelectedLibraryChanged()
        {
            LibraryArtworksIndex = -1;
            LibraryArtworksIndex = 0;
            LibraryCoversIndex = -1;
            LibraryCoversIndex = 0;
            LibraryLogosIndex = -1;
            LibraryLogosIndex = 0;
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
                _LibraryCoversIndex = index;
                OnPropertyChanged(nameof(LibraryCoversIndex));
            }
            catch { }
        }

        private void RefreshArtwork(int index)
        {
            try
            {
                OnPropertyChanged(nameof(LibraryArtworks));
                _LibraryArtworksIndex = index;
                OnPropertyChanged(nameof(LibraryArtworksIndex));
            }
            catch { }
        }

        private void RefreshLogo(int index)
        {
            try
            {
                OnPropertyChanged(nameof(LibraryLogos));
                _LibraryLogosIndex = index;
                OnPropertyChanged(nameof(LibraryLogosIndex));
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

                UIHelper.TryInvoke(() =>
                {
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
                });

                if (IsQuickTools)
                {
                    CurrentProcessViewModel?.Dispose();

                    if (currentProcess is null || currentProcess.Filter != ProcessFilter.Allowed)
                        CurrentProcessViewModel = null;
                    else
                        CurrentProcessViewModel = new ProcessExViewModel(currentProcess, true);
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

            lock (_collectionLock3)
            {
                HotkeyViewModel? foundHotkey = HotkeysList.FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
                if (foundHotkey is null)
                    HotkeysList.SafeAdd(new HotkeyViewModel(hotkey));
                else
                    foundHotkey.Hotkey = hotkey;
            }

            if (ManagerFactory.hotkeysManager.Status != ManagerStatus.Initialized || isLoadingProfile || SelectedProfile is null)
                return;

            if (SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? gyroActions))
            {
                if (gyroActions is GyroActions gyroAction)
                {
                    ButtonState newButtonState = hotkey.inputsChord.ButtonState.Clone() as ButtonState;
                    if (!gyroAction.MotionTrigger.Equals(newButtonState))
                    {
                        gyroAction.MotionTrigger = newButtonState;
                        UpdateProfile();
                    }
                }
            }
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

            UIHelper.TryInvoke(() =>
            {
                SelectedProfile.LayoutTitle = layoutTemplate.Name;
            });

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
                    SelectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == powerProfileAC.Guid));
                    SelectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(ProfilePicker.FirstOrDefault(a => a.LinkedPresetId == powerProfileDC.Guid));
                }
            });
        }

        public void Close()
        {
            if (selectedTemplate is not null)
            {
                selectedTemplate.Updated -= Template_Updated;
                selectedTemplate = null;
            }

            ManagerFactory.profileManager.Deleted -= ProfileDeleted;
            ManagerFactory.profileManager.Updated -= ProfileUpdated;
            ManagerFactory.profileManager.Applied -= ProfileApplied;
            ManagerFactory.profileManager.Initialized -= ProfileManagerLoaded;

            if (IsQuickTools)
            {
                ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
                ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
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
            ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
            ManagerFactory.gpuManager.Hooked -= GPUManager_Hooked;
            ManagerFactory.gpuManager.Unhooked -= GPUManager_Unhooked;
            ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
            ManagerFactory.powerProfileManager.Updated -= PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted -= PowerProfileManager_Deleted;
            ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
            PlatformManager.RTSS.Updated -= RTSS_Updated;
            ManagerFactory.platformManager.Initialized -= PlatformManager_Initialized;

            UpdateTimer.Elapsed -= UpdateTimer_Elapsed;
            UpdateTimer.Stop();
            UpdateTimer.Dispose();

            Dispose();
        }

        public override void Dispose()
        {
            CurrentProcessViewModel?.Dispose();
            CurrentProcessViewModel = null;

            base.Dispose();
        }
    }
}
