using HandheldCompanion.Devices;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using static HandheldCompanion.Processors.IntelProcessor;
using Resources = HandheldCompanion.Properties.Resources;

namespace HandheldCompanion.ViewModels
{
    // ViewModel for Profiles Picker
    public class ProfilesPickerViewModel : BaseViewModel
    {
        public string Header => IsInternal ? Resources.PowerProfilesPage_DevicePresets : Resources.PowerProfilesPage_UserPresets;

        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set
            {
                if (value != Text)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }
        public bool IsInternal { get; set; }
        public Guid? LinkedPresetId { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }

    public class PerformancePageViewModel : BaseViewModel
    {
        public ObservableCollection<ScreenFramelimitViewModel> FramerateLimits { get; } = [];

        private PowerProfile _selectedPreset;
        public PowerProfile SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != value)
                {
                    // update variable
                    _selectedPreset = value;

                    IsCustomFrameLimitSelected = ShouldUseCustomFrameLimit(_selectedPreset.FramerateValue);
                    if (IsCustomFrameLimitSelected)
                        _customFrameLimitValue = Math.Clamp(_selectedPreset.FramerateValue, 0, FrameLimitMaximum);

                    // page-specific behaviors
                    switch (IsQuickTools)
                    {
                        case false:
                            _selectedPresetPicker = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == _selectedPreset.Guid);
                            break;
                    }

                    // refresh all properties
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        public readonly bool IsQuickTools;
        public bool IsMainPage => !IsQuickTools;

        #region Binding Properties

        public double GPUFreqMinimum => IDevice.GetCurrent().GfxClock[0];
        public double GPUFreqMaximum => IDevice.GetCurrent().GfxClock[1];

        public double CPUFreqMinimum => MotherboardInfo.ProcessorMaxTurboSpeed / 4.0d;
        public double CPUFreqMaximum => MotherboardInfo.ProcessorMaxTurboSpeed;

        public double CPUCoreMaximum => MotherboardInfo.NumberOfCores;

        public bool SupportsSoftwareFanMode
        {
            get
            {
                if (!IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.FanControl))
                    return false;

                // Legion Go 2 has EC fan control override
                if (IDevice.GetCurrent() is Devices.Lenovo.LegionGoTablet2)
                    return true;

                if (IDevice.GetCurrent() is Devices.Lenovo.LegionGo)
                    return SelectedPreset.OEMPowerMode == 0xFF;

                return true;
            }
        }

        public bool SupportsIntelEnduranceGaming => GPUManager.GetCurrent() is IntelGPU intelGPU && intelGPU.HasEnduranceGaming(out _, out _, out _);

        // Platform Manager
        public bool IsRunningRTSS => ManagerFactory.platformManager.IsReady && PlatformManager.RTSS.IsInstalled;
        public bool SupportsAutoTDP
        {
            get
            {
                if (!IsRunningRTSS)
                    return false;

                return PerformanceManager.GetProcessor()?.CanChangeTDP ?? false;
            }
        }

        public bool SupportsTDP => PerformanceManager.GetProcessor()?.CanChangeTDP ?? false;
        public bool SupportsGPUFreq => PerformanceManager.GetProcessor()?.CanChangeGPU ?? false;

        public bool SupportsFramerateLimiter => IsRunningRTSS;

        public bool PerformanceManagerEnabled => ManagerFactory.settingsManager.GetBoolean("PerformanceManagerEnabled");

        public bool CanChangePreset => true; // !SelectedPreset.DeviceDefault;
        public bool CanDeletePreset => !SelectedPreset.Default && !SelectedPreset.DeviceDefault;

        public bool HasWarning => !string.IsNullOrEmpty(Warning);

        public string Warning
        {
            get
            {
                if (SelectedPreset.DeviceDefault)
                    return Resources.ProfilesPage_DefaultDeviceProfile;

                return string.Empty;
            }
        }

        public string PresetName
        {
            get => SelectedPreset.Name;
            set
            {
                if (value != PresetName)
                {
                    SelectedPreset.Name = value;
                    OnPropertyChanged(nameof(PresetName));
                }
            }
        }

        public string PresetDescription
        {
            get => SelectedPreset.Description;
            set
            {
                if (value != PresetDescription)
                {
                    SelectedPreset.Description = value;
                    OnPropertyChanged(nameof(PresetDescription));
                }
            }
        }

        public double ConfigurableTDPOverrideDown
        {
            get => ManagerFactory.settingsManager.GetDouble(Settings.ConfigurableTDPOverrideDown);
        }

        public double ConfigurableTDPOverrideUp
        {
            get => ManagerFactory.settingsManager.GetDouble(Settings.ConfigurableTDPOverrideUp);
        }

        public double AutoTDPMaximum
        {
            get
            {
                if (!ManagerFactory.multimediaManager.IsReady || ManagerFactory.multimediaManager.PrimaryDesktop is null)
                    return 60.0d;

                return ManagerFactory.multimediaManager.PrimaryDesktop.GetMaximumFrequency();
            }
        }

        public bool TDPOverrideEnabled
        {
            get => SelectedPreset.TDPOverrideEnabled;
            set
            {
                if (value != TDPOverrideEnabled)
                {
                    SelectedPreset.TDPOverrideEnabled = value;
                    OnPropertyChanged(nameof(TDPOverrideEnabled));
                }
            }
        }

        private bool _coerceGuard;
        private double RequiredDelta
        {
            get
            {
                if (PerformanceManager.GetProcessor() is IntelProcessor ip)
                {
                    // Official specification for Lunar Lake states that PL2 should always be at least 1 W higher than PL1
                    if (ip.MicroArch == IntelMicroArch.LunarLake)
                        return 1.0d;
                }

                return 0.0d;
            }
        }

        // PL1 = Long/Sustained
        // On AMD also = STAPM ?
        public double PL1OverrideValue
        {
            get
            {
                double[] tdp = SelectedPreset?.TDPOverrideValues ?? IDevice.GetCurrent().nTDP;
                return tdp[(int)PowerType.Slow];
            }
            set
            {
                if (Math.Abs(value - PL1OverrideValue) < double.Epsilon) return;

                double clamped = Math.Max(ConfigurableTDPOverrideDown,
                                  Math.Min(value, ConfigurableTDPOverrideUp));

                if (SelectedPreset is null)
                    return;

                var selectedPreset = SelectedPreset;
                if (selectedPreset is null)
                    return;

                double[] tdpOverrideValues = selectedPreset.TDPOverrideValues ??= (double[])IDevice.GetCurrent().nTDP.Clone();

                tdpOverrideValues[(int)PowerType.Slow] = clamped;
                tdpOverrideValues[(int)PowerType.Stapm] = clamped;

                // If PL1 crosses PL2, bump PL2 up to maintain PL2 >= PL1 + Δ
                double minPl2 = clamped + RequiredDelta;

                if (!_coerceGuard && PL2OverrideValue < minPl2)
                {
                    try
                    {
                        _coerceGuard = true;
                        tdpOverrideValues[(int)PowerType.Fast] = Math.Min(ConfigurableTDPOverrideUp, minPl2);
                        OnPropertyChanged(nameof(PL2OverrideValue));
                    }
                    finally { _coerceGuard = false; }
                }

                OnPropertyChanged(nameof(PL1OverrideValue));
            }
        }

        // PL2 = Fast/Short
        public double PL2OverrideValue
        {
            get
            {
                double[] tdp = SelectedPreset?.TDPOverrideValues ?? IDevice.GetCurrent().nTDP;
                return tdp[(int)PowerType.Fast];
            }
            set
            {
                if (Math.Abs(value - PL2OverrideValue) < double.Epsilon) return;

                double minPl2 = PL1OverrideValue + RequiredDelta;
                double clamped = Math.Max(minPl2, Math.Min(value, ConfigurableTDPOverrideUp));

                var selectedPreset = SelectedPreset;
                if (selectedPreset is null)
                    return;

                double[] tdpOverrideValues = selectedPreset.TDPOverrideValues ??= (double[])IDevice.GetCurrent().nTDP.Clone();

                if (tdpOverrideValues[(int)PowerType.Fast] != clamped)
                {
                    tdpOverrideValues[(int)PowerType.Fast] = clamped;
                    OnPropertyChanged(nameof(PL2OverrideValue));
                }
            }
        }

        public bool CPUOverrideEnabled
        {
            get => SelectedPreset.CPUOverrideEnabled;
            set
            {
                if (value != CPUOverrideEnabled)
                {
                    SelectedPreset.CPUOverrideEnabled = value;
                    OnPropertyChanged(nameof(CPUOverrideEnabled));
                }
            }
        }

        public double CPUOverrideValue
        {
            get => SelectedPreset.CPUOverrideValue != 0 ? SelectedPreset.CPUOverrideValue : CPUFreqMaximum;
            set
            {
                if (value != CPUOverrideValue)
                {
                    SelectedPreset.CPUOverrideValue = value;
                    OnPropertyChanged(nameof(CPUOverrideValue));
                }
            }
        }

        public bool GPUOverrideEnabled
        {
            get => SelectedPreset.GPUOverrideEnabled;
            set
            {
                if (value != GPUOverrideEnabled)
                {
                    SelectedPreset.GPUOverrideEnabled = value;
                    OnPropertyChanged(nameof(GPUOverrideEnabled));
                }
            }
        }

        public double GPUOverrideValue
        {
            get => SelectedPreset.GPUOverrideValue != 0 ? SelectedPreset.GPUOverrideValue : GPUFreqMaximum;
            set
            {
                if (value != GPUOverrideValue)
                {
                    SelectedPreset.GPUOverrideValue = value;
                    OnPropertyChanged(nameof(GPUOverrideValue));
                }
            }
        }

        public bool AutoTDPEnabled
        {
            get => SelectedPreset.AutoTDPEnabled;
            set
            {
                if (value != AutoTDPEnabled)
                {
                    SelectedPreset.AutoTDPEnabled = value;
                    OnPropertyChanged(nameof(AutoTDPEnabled));
                }
            }
        }

        public float AutoTDPRequestedFPS
        {
            get => SelectedPreset.AutoTDPRequestedFPS;
            set
            {
                if (value != AutoTDPRequestedFPS)
                {
                    SelectedPreset.AutoTDPRequestedFPS = value;
                    OnPropertyChanged(nameof(AutoTDPRequestedFPS));
                }
            }
        }

        public int FrameLimitMaximum
        {
            get
            {
                DesktopScreen? desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                if (desktopScreen is null)
                    return 60;

                return desktopScreen.GetCurrentFrequency();
            }
        }

        public int FrameLimitMinimum => 10;

        private bool _isCustomFrameLimitSelected;
        public bool IsCustomFrameLimitSelected
        {
            get => _isCustomFrameLimitSelected;
            private set
            {
                if (value == _isCustomFrameLimitSelected)
                    return;

                _isCustomFrameLimitSelected = value;
                OnPropertyChanged(nameof(IsCustomFrameLimitSelected));
            }
        }

        private int? _customFrameLimitValue;
        public int CustomFrameLimitValue
        {
            get
            {
                if (_customFrameLimitValue.HasValue)
                    return _customFrameLimitValue.Value;

                if (SelectedPreset is null)
                    return 0;

                return Math.Clamp(SelectedPreset.FramerateValue, 0, FrameLimitMaximum);
            }
            set
            {
                if (SelectedPreset is null)
                    return;

                int clamped = Math.Clamp(value, 0, FrameLimitMaximum);
                bool customValueChanged = _customFrameLimitValue != clamped;
                bool selectionChanged = !IsCustomFrameLimitSelected;
                bool presetChanged = SelectedPreset.FramerateValue != clamped;

                if (customValueChanged)
                {
                    _customFrameLimitValue = clamped;
                    OnPropertyChanged(nameof(CustomFrameLimitValue));
                }

                if (selectionChanged)
                    IsCustomFrameLimitSelected = true;

                if (!selectionChanged && !presetChanged)
                    return;

                SelectedPreset.FramerateValue = clamped;
                OnPropertyChanged(nameof(SelectedFrameLimit));
                SubmitSelectedPreset();
            }
        }

        public ScreenFramelimitViewModel? SelectedFrameLimit
        {
            get
            {
                lock (_collectionLock)
                {
                    if (!FramerateLimits.Any())
                        return null;

                    if (IsCustomFrameLimitSelected)
                        return FramerateLimits.FirstOrDefault(vm => vm.IsCustom);

                    ScreenFramelimitViewModel? exactLimit = FramerateLimits.FirstOrDefault(vm => !vm.IsCustom && vm.FrameLimit.limit == SelectedPreset.FramerateValue);
                    if (exactLimit is not null)
                        return exactLimit;

                    return FramerateLimits.FirstOrDefault(vm => vm.IsCustom);
                }
            }
            set
            {
                if (value is null)
                    return;

                if (value.IsCustom)
                {
                    int customValue = _customFrameLimitValue.HasValue
                        ? _customFrameLimitValue.Value
                        : Math.Clamp(SelectedPreset.FramerateValue, 0, FrameLimitMaximum);

                    if (!_customFrameLimitValue.HasValue)
                    {
                        _customFrameLimitValue = customValue;
                        OnPropertyChanged(nameof(CustomFrameLimitValue));
                    }

                    if (!IsCustomFrameLimitSelected || SelectedPreset.FramerateValue != customValue)
                    {
                        IsCustomFrameLimitSelected = true;
                        SelectedPreset.FramerateValue = customValue;
                        OnPropertyChanged(nameof(SelectedFrameLimit));
                        SubmitSelectedPreset();
                    }

                    return;
                }

                bool selectionChanged = IsCustomFrameLimitSelected;
                if (selectionChanged)
                    IsCustomFrameLimitSelected = false;

                if (SelectedPreset.FramerateValue != value.FrameLimit.limit)
                {
                    SelectedPreset.FramerateValue = value.FrameLimit.limit;
                    OnPropertyChanged(nameof(SelectedFrameLimit));
                    SubmitSelectedPreset();
                }
                else if (selectionChanged)
                {
                    OnPropertyChanged(nameof(SelectedFrameLimit));
                }
            }
        }

        public bool EPPOverrideEnabled
        {
            get => SelectedPreset.EPPOverrideEnabled;
            set
            {
                if (value != EPPOverrideEnabled)
                {
                    SelectedPreset.EPPOverrideEnabled = value;
                    OnPropertyChanged(nameof(EPPOverrideEnabled));
                }
            }
        }

        public uint EPPOverrideValue
        {
            get => SelectedPreset.EPPOverrideValue;
            set
            {
                if (value != EPPOverrideValue)
                {
                    SelectedPreset.EPPOverrideValue = value;
                    OnPropertyChanged(nameof(EPPOverrideValue));
                }
            }
        }

        public bool CPUCoreEnabled
        {
            get => SelectedPreset.CPUCoreEnabled;
            set
            {
                if (value != CPUCoreEnabled)
                {
                    SelectedPreset.CPUCoreEnabled = value;
                    OnPropertyChanged(nameof(CPUCoreEnabled));
                }
            }
        }

        public int CPUCoreCount
        {
            get => SelectedPreset.CPUCoreCount;
            set
            {
                if (value != CPUCoreCount)
                {
                    SelectedPreset.CPUCoreCount = value;
                    OnPropertyChanged(nameof(CPUCoreCount));
                }
            }
        }

        public int CPUBoostLevel
        {
            get => (int)SelectedPreset.CPUBoostLevel;
            set
            {
                if (value != CPUBoostLevel)
                {
                    SelectedPreset.CPUBoostLevel = (CPUBoostLevel)value;
                    OnPropertyChanged(nameof(CPUBoostLevel));
                }
            }
        }

        public int OSPowerMode
        {
            get => Array.IndexOf(PerformanceManager.PowerModes, SelectedPreset.OSPowerMode);
            set
            {
                if (value != OSPowerMode)
                {
                    SelectedPreset.OSPowerMode = PerformanceManager.PowerModes[value];
                    OnPropertyChanged(nameof(OSPowerMode));
                }
            }
        }

        public int CPUParkingMode
        {
            get => (int)SelectedPreset.CPUParkingMode;
            set
            {
                if (value != CPUParkingMode)
                {
                    SelectedPreset.CPUParkingMode = (CoreParkingMode)value;
                    OnPropertyChanged(nameof(CPUParkingMode));
                }
            }
        }

        public int FanMode
        {
            get => (int)SelectedPreset.FanProfile.fanMode;
            set
            {
                if (value != FanMode)
                {
                    SelectedPreset.FanProfile.fanMode = (FanMode)value;
                    OnPropertyChanged(nameof(FanMode));
                }
            }
        }

        public bool EnduranceGamingEnabled
        {
            get => SelectedPreset.IntelEnduranceGamingEnabled;
            set
            {
                if (value != EnduranceGamingEnabled)
                {
                    SelectedPreset.IntelEnduranceGamingEnabled = value;
                    OnPropertyChanged(nameof(EnduranceGamingEnabled));
                }
            }
        }

        public int IntelEnduranceGamingPreset
        {
            get => SelectedPreset.IntelEnduranceGamingPreset;
            set
            {
                if (value != IntelEnduranceGamingPreset)
                {
                    SelectedPreset.IntelEnduranceGamingPreset = value;
                    OnPropertyChanged(nameof(IntelEnduranceGamingPreset));
                }
            }
        }

        public ICommand DeletePresetCommand { get; private set; }

        #endregion

        #region Main Window specific Bindings

        public Func<double, string> Formatter { get; private set; }
        public Func<double, string> TempTickFormatter { get; } = v => $"{v * 10:N0} °C";    // bottom axis ticks: 0,10,...100
        public Func<double, string> CpuXAxisFormatter { get; private set; }    // top axis precise label

        private int _dragIndex = -1;
        private bool _isDragging;
        private const double Epsilon = 0.05; // 0.05% fan speed granularity

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampIndex(double x)
        {
            // index space (0..10)
            int idx = (int)Math.Round(x);
            if (idx < 0) return 0;
            if (idx > 10) return 10;
            return idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Clamp01_100(double y)
        {
            if (y < 0) return 0;
            if (y > 100) return 100;
            return y;
        }

        private double _cpuTempC = double.NaN;
        public double CpuTempC
        {
            get => _cpuTempC;
            private set
            {
                if (value != _cpuTempC)
                {
                    _cpuTempC = value;
                    OnPropertyChanged(nameof(CpuTempC));
                }
            }
        }

        private double _cpuTempX = -5;
        public double CpuTempX
        {
            get => _cpuTempX;
            private set
            {
                if (value != _cpuTempX)
                {
                    _cpuTempX = value;
                    OnPropertyChanged(nameof(CpuTempX));
                }
            }
        }

        private double _xPointer = -5;
        public double XPointer
        {
            get => _xPointer;
            set
            {
                if (value != _xPointer)
                {
                    _xPointer = value;
                    OnPropertyChanged(nameof(XPointer));
                }
            }
        }

        private double _yPointer = -5;
        public double YPointer
        {
            get => _yPointer;
            set
            {
                if (value != _yPointer)
                {
                    _yPointer = value;
                    OnPropertyChanged(nameof(YPointer));
                }
            }
        }

        private int _selectedPresetIndex;

        private ProfilesPickerViewModel? _selectedPresetPicker;
        public ProfilesPickerViewModel? SelectedPresetPicker
        {
            get => _selectedPresetPicker;
            set
            {
                if (value != _selectedPresetPicker && value?.LinkedPresetId is not null)
                {
                    _selectedPresetPicker = value;
                    OnPropertyChanged(nameof(SelectedPresetPicker));
                    SelectedPreset = ManagerFactory.powerProfileManager.GetProfile(value.LinkedPresetId.Value);
                }
            }
        }

        private string _modifyPresetName = string.Empty;
        public string ModifyPresetName
        {
            get => _modifyPresetName;
            set
            {
                if (value != _modifyPresetName)
                {
                    _modifyPresetName = value;
                    OnPropertyChanged(nameof(ModifyPresetName));
                }
            }
        }

        private string _modifyPresetDescription = string.Empty;
        public string ModifyPresetDescription
        {
            get => _modifyPresetDescription;
            set
            {
                if (value != _modifyPresetDescription)
                {
                    _modifyPresetDescription = value;
                    OnPropertyChanged(nameof(ModifyPresetDescription));
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

        private ObservableCollection<ProfilesPickerViewModel> _profilePickerItems = [];
        public ObservableCollection<ProfilesPickerViewModel> ProfilePickerItems => _profilePickerItems;
        public ICommand OpenModifyDialogCommand { get; private set; } = new DelegateCommand(() => { });
        public ICommand ConfirmModifyCommand { get; private set; } = new DelegateCommand(() => { });
        public ICommand CreatePresetCommand { get; private set; } = new DelegateCommand(() => { });
        public ICommand ConfirmCreateProfileCommand { get; private set; } = new DelegateCommand(() => { });
        public ICommand FanPresetSilentCommand { get; private set; } = new DelegateCommand(() => { });
        public ICommand FanPresetPerformanceCommand { get; private set; } = new DelegateCommand(() => { });
        public ICommand FanPresetTurboCommand { get; private set; } = new DelegateCommand(() => { });

        #endregion

        private ChartPoint? _storedChartPoint;
        private CartesianChart? _fanGraph;
        private LineSeries? _fanGraphLineSeries;
        private ContentDialog? _modifyDialog;

        private bool _updatingFanCurveUI;
        private bool _fanCurveDirty;

        /// <summary>
        /// Raised when the fan curve data needs to be pushed to the chart UI.
        /// The View subscribes and updates the LineSeries on the UI thread.
        /// </summary>
        public event Action<double[]>? FanCurveUpdateRequested;

        /// <summary>
        /// Allows the View to suppress CollectionChanged feedback while pushing fan curve values.
        /// </summary>
        public void SetUpdatingFanCurveUI(bool value) => _updatingFanCurveUI = value;

        /// <summary>
        /// Properties that should trigger FanCurveUpdateRequested event on MainPage.
        /// These affect the fan curve chart visualization (axis labels, point selection, mode).
        /// </summary>
        private static HashSet<string> _fanCurveUIProperties =
        [
            nameof(FanMode),
            nameof(CpuTempX),
            nameof(CpuTempC),
            nameof(XPointer),
            nameof(YPointer),
        ];

        /// <summary>
        /// Properties that should NOT trigger SubmitSelectedPreset() (preset persistence).
        /// Includes: form fields, device capabilities, UI state, and fan curve UI properties.
        /// </summary>
        private static HashSet<string> _skipPropertyChangedUpdate =
        [
            // Form fields specific to create/modify dialogs
            nameof(ModifyPresetName),
            nameof(ModifyPresetDescription),
            nameof(CopyDefaultProfileSettings),

            // TDP configuration
            nameof(AutoTDPMaximum),
            nameof(ConfigurableTDPOverrideDown),
            nameof(ConfigurableTDPOverrideUp),
            nameof(SupportsTDP),

            // UI state (display-only or indirect updates)
            nameof(SelectedPresetPicker),
            nameof(ProfilePickerItems),
            nameof(HasWarning),

            // Device capabilities (read-only)
            nameof(SupportsGPUFreq),
            nameof(SupportsIntelEnduranceGaming),
            nameof(SupportsAutoTDP),
            nameof(SupportsFramerateLimiter),
            nameof(IsRunningRTSS),
            nameof(PerformanceManagerEnabled),

            // Framerate limiter UI state
            nameof(FramerateLimits),
            nameof(SelectedFrameLimit),
            nameof(IsCustomFrameLimitSelected),
            nameof(CustomFrameLimitValue),
            nameof(FrameLimitMaximum),

            // Fan curve UI properties (handled separately for event notification)
            nameof(FanMode),
            nameof(CpuTempX),
            nameof(CpuTempC),
            nameof(XPointer),
            nameof(YPointer),

            "CreateProfileName",

            // Bulk refresh trigger
            string.Empty,
        ];

        private ContentDialog? contentDialog;

        public PerformancePageViewModel(bool isQuickTools)
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(_profilePickerItems, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(FramerateLimits, _collectionLock);

            _selectedPreset = ManagerFactory.powerProfileManager.GetProfile(Guid.Empty);

            IsQuickTools = isQuickTools;

            Formatter = x => x.ToString("N2");
            CpuXAxisFormatter = v => Math.Abs(v - CpuTempX) < 0.0001 ? $"{CpuTempX * 10:N2} °C" : string.Empty;

            #region General Setup

            // raise events
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

            // raise events
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

            // raise events
            switch (ManagerFactory.multimediaManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryMedia();
                    break;
            }

            // raise events
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

            // manage events
            PerformanceManager.Initialized += PerformanceManager_Initialized;

            // raise events
            if (PerformanceManager.IsInitialized && PerformanceManager.GetProcessor() is Processor processor)
                PerformanceManager_Initialized(processor.CanChangeTDP, processor.CanChangeGPU);

            PropertyChanged += (sender, e) =>
            {
                if (SelectedPreset is null || SelectedPreset.Name is null)
                    return;

                // Handle fan curve UI updates on MainPage for relevant property changes
                if (IsMainPage && e.PropertyName is not null && _fanCurveUIProperties.Contains(e.PropertyName))
                {
                    FanCurveUpdateRequested?.Invoke(SelectedPreset.FanProfile.fanSpeeds);
                    return;
                }

                // Skip properties that don't need preset persistence
                if (e.PropertyName is not null && _skipPropertyChangedUpdate.Contains(e.PropertyName))
                    return;

                // Trigger power profile update but don't freeze UI
                // todo: implement proper debounce
                SubmitSelectedPreset();
            };

            CreatePresetCommand = new DelegateCommand(() =>
            {
                // Reset form state with generated profile name
                CreateProfileName = ManagerFactory.powerProfileManager.GetProfileName(Resources.PowerProfileManualName);
                CopyDefaultProfileSettings = false;
            });

            DeletePresetCommand = new DelegateCommand(async () =>
            {
                Dialog dialog = new Dialog(isQuickTools ? OverlayQuickTools.GetCurrent() : MainWindow.GetCurrent())
                {
                    Title = string.Format(Resources.ProfilesPage_AreYouSureDelete1, SelectedPreset.Name),
                    Content = Resources.ProfilesPage_AreYouSureDelete2,
                    CloseButtonText = Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Resources.ProfilesPage_Delete
                };

                ContentDialogResult result = await dialog.ShowAsync();
                switch (result)
                {
                    case ContentDialogResult.None:
                        dialog.Hide();
                        break;
                    case ContentDialogResult.Primary:
                        ManagerFactory.powerProfileManager.DeleteProfile(SelectedPreset);
                        break;
                }
            });

            ConfirmCreateProfileCommand = new DelegateCommand(() =>
            {
                if (string.IsNullOrWhiteSpace(CreateProfileName))
                {
                    // Generate default name if not provided
                    CreateProfileName = ManagerFactory.powerProfileManager.GetProfileName(Resources.PowerProfileManualName);
                }

                PowerProfile powerProfile;

                if (CopyDefaultProfileSettings)
                {
                    // Clone the default profile
                    PowerProfile defaultProfile = ManagerFactory.powerProfileManager.GetDefault();
                    powerProfile = ManagerFactory.powerProfileManager.CloneProfile(defaultProfile);
                    powerProfile.Name = CreateProfileName;
                    powerProfile.Description = Resources.PowerProfileManualDescription;
                    // Generate new GUID for the cloned profile
                    powerProfile.Guid = Guid.NewGuid();
                    powerProfile.Default = false;
                }
                else
                {
                    // Create a new profile with default values
                    powerProfile = new(CreateProfileName, Resources.PowerProfileManualDescription)
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

                // Reset form state
                CreateProfileName = string.Empty;
                CopyDefaultProfileSettings = false;
            });

            #endregion

            #region Main Window Setup
            if (IsMainPage)
            {
                OpenModifyDialogCommand = new DelegateCommand(async () =>
                {
                    // capture dialog content
                    ContentDialog storedDialog = MainWindow.performancePage.PowerProfileSettingsDialog;
                    object content = storedDialog.Content;

                    contentDialog = new ContentDialog
                    {
                        Title = storedDialog.Title,
                        CloseButtonText = storedDialog.CloseButtonText,
                        PrimaryButtonText = storedDialog.PrimaryButtonText,
                        PrimaryButtonCommand = storedDialog.PrimaryButtonCommand,
                        IsEnabled = storedDialog.IsEnabled,
                        Content = content,
                        DataContext = this,
                    };

                    // update vars
                    ModifyPresetName = PresetName;
                    ModifyPresetDescription = PresetDescription;

                    try { await contentDialog.ShowAsync(); } catch { }
                });

                ConfirmModifyCommand = new DelegateCommand(() =>
                {
                    // Update the name of the selected preset
                    SelectedPreset.Name = ModifyPresetName;

                    // Update the corresponding item in ProfilePickerItems
                    var selectedItem = _profilePickerItems.FirstOrDefault(item => item.LinkedPresetId == SelectedPreset.Guid);
                    if (selectedItem != null)
                    {
                        PresetName = ModifyPresetName;
                        PresetDescription = ModifyPresetDescription;

                        selectedItem.Text = ModifyPresetName;
                        OnPropertyChanged("ModifyPresets");
                    }
                });

                FanPresetSilentCommand = new DelegateCommand(() =>
                {
                    if (_fanGraphLineSeries is null)
                        return;

                    // update charts
                    for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                        _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[0][idx];

                    // Temporary until view dependencies could be removed
                    OnPropertyChanged("FanGraphPreset");
                });

                FanPresetPerformanceCommand = new DelegateCommand(() =>
                {
                    if (_fanGraphLineSeries is null)
                        return;

                    // update charts
                    for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                        _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[1][idx];

                    // Temporary until view dependencies could be removed
                    OnPropertyChanged("FanGraphPreset");
                });

                FanPresetTurboCommand = new DelegateCommand(() =>
                {
                    if (_fanGraphLineSeries is null)
                        return;

                    // update charts
                    for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                        _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[2][idx];

                    // Temporary until view dependencies could be removed
                    OnPropertyChanged("FanGraphPreset");
                });
            }
            #endregion
        }

        private void QueryPlatforms()
        {
            // manage events
            PlatformManager.LibreHardware.CPUTemperatureChanged += LibreHardwareMonitor_CpuTemperatureChanged;

            OnPropertyChanged(nameof(IsRunningRTSS));
            OnPropertyChanged(nameof(SupportsFramerateLimiter));
            OnPropertyChanged(nameof(SupportsAutoTDP));
        }

        private void SubmitSelectedPreset()
        {
            Task.Run(() =>
            {
                ManagerFactory.powerProfileManager.UpdateOrCreateProfile(SelectedPreset, IsQuickTools ? UpdateSource.QuickProfilesPage : UpdateSource.ProfilesPage);
            });
        }

        private void PlatformManager_Initialized()
        {
            QueryPlatforms();
        }

        private void LibreHardwareMonitor_CpuTemperatureChanged(float? value)
        {
            if (!value.HasValue) return;

            // Clamp to your axis range and convert °C -> X index (0..10)
            double tempC = Math.Max(0, Math.Min(100, value.Value));
            double x = tempC / 10.0;

            CpuTempC = tempC;
            CpuTempX = x;
        }

        private void QueryGPU()
        {
            // manage events
            ManagerFactory.gpuManager.Hooked += GPUManager_Hooked;
            ManagerFactory.gpuManager.Unhooked += GpuManager_Unhooked;

            GPU? gpu = GPUManager.GetCurrent();
            if (gpu is not null)
                GPUManager_Hooked(gpu);
        }

        private void GpuManager_Initialized()
        {
            QueryGPU();
        }

        private void GPUManager_Hooked(GPU GPU)
        {
            if (GPU is AMDGPU amdGPU)
            {
                // do something
            }
            else if (GPU is IntelGPU intelGPU)
            {
                intelGPU.EnduranceGamingState += IntelGPU_EnduranceGamingState;
            }

            UpdateGraphicsSettingsUI();
        }

        private void GpuManager_Unhooked(GPU GPU)
        {
            if (GPU is AMDGPU amdGPU)
            {
                // do something
            }
            else if (GPU is IntelGPU intelGPU)
            {
                intelGPU.EnduranceGamingState -= IntelGPU_EnduranceGamingState;
            }

            UpdateGraphicsSettingsUI();
        }

        private void IntelGPU_EnduranceGamingState(bool Supported, IGCL.IGCLBackend.ctl_3d_endurance_gaming_control_t Control, IGCL.IGCLBackend.ctl_3d_endurance_gaming_mode_t Mode)
        {
            UpdateGraphicsSettingsUI();
        }

        private void UpdateGraphicsSettingsUI()
        {
            OnPropertyChanged(nameof(SupportsIntelEnduranceGaming));
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            OnPropertyChanged("ConfigurableTDPOverride");
            OnPropertyChanged("ConfigurableTDPOverrideDown");
            OnPropertyChanged("ConfigurableTDPOverrideUp");
            OnPropertyChanged(nameof(PerformanceManagerEnabled));
        }

        private void QueryPowerProfile()
        {
            // manage events
            ManagerFactory.powerProfileManager.Updated += PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted += PowerProfileManager_Deleted;

            if (IsMainPage)
            {
                foreach (PowerProfile powerProfile in ManagerFactory.powerProfileManager.profiles.Values)
                    PowerProfileManager_Updated(powerProfile, UpdateSource.Creation);

                // Reset to Default
                ProfilesPickerViewModel? profile = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == Guid.Empty);
                if (profile is not null)
                    SelectedPresetPicker = profile;
            }
        }

        private void PowerProfileManager_Initialized()
        {
            QueryPowerProfile();
        }

        private void QueryMedia()
        {
            // manage events
            ManagerFactory.multimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;

            MultimediaManager_PrimaryScreenChanged(ManagerFactory.multimediaManager.PrimaryDesktop);
        }

        private void MultimediaManager_Initialized()
        {
            QueryMedia();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
                ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
                ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
                ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
                PerformanceManager.EPPChanged -= PerformanceManager_EPPChanged;
                PerformanceManager.Initialized -= PerformanceManager_Initialized;
                ManagerFactory.powerProfileManager.Updated -= PowerProfileManager_Updated;
                ManagerFactory.powerProfileManager.Deleted -= PowerProfileManager_Deleted;
                ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
                ManagerFactory.gpuManager.Hooked -= GPUManager_Hooked;
                ManagerFactory.gpuManager.Unhooked -= GpuManager_Unhooked;
                ManagerFactory.gpuManager.Initialized -= GpuManager_Initialized;
                PlatformManager.LibreHardware.CPUTemperatureChanged -= LibreHardwareMonitor_CpuTemperatureChanged;
                ManagerFactory.platformManager.Initialized -= PlatformManager_Initialized;

                if (IsMainPage)
                {
                    _fanGraphLineSeries?.ActualValues.CollectionChanged -= ActualValues_CollectionChanged;

                    if (_fanGraph is not null)
                    {
                        _fanGraph.DataClick -= ChartOnDataClick;
                        _fanGraph.MouseLeave -= ChartMouseLeave;
                        _fanGraph.MouseMove -= ChartMouseMove;
                        _fanGraph.MouseUp -= ChartMouseUp;
                        _fanGraph.TouchMove -= ChartTouchMove;
                    }
                }
            }

            base.Dispose(disposing);
        }

        #region Events

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            if (value is null)
                return;

            switch (name)
            {
                case "ConfigurableTDPOverride":
                case "ConfigurableTDPOverrideDown":
                case "ConfigurableTDPOverrideUp":
                    OnPropertyChanged(name);
                    break;
                case "PerformanceManagerEnabled":
                    OnPropertyChanged(nameof(PerformanceManagerEnabled));
                    break;
            }
        }

        private void MultimediaManager_PrimaryScreenChanged(DesktopScreen? screen)
        {
            lock (_collectionLock)
            {
                FramerateLimits.Clear();

                if (screen is not null)
                {
                    foreach (ScreenFramelimit frameLimit in screen.GetFramelimits())
                        FramerateLimits.Add(new ScreenFramelimitViewModel(frameLimit));
                }

                FramerateLimits.Add(new ScreenFramelimitViewModel(new ScreenFramelimit(-1, 0), Resources.Enum_InputsHotkeyType_Custom, true));
            }

            if (SelectedPreset is not null && ShouldUseCustomFrameLimit(SelectedPreset.FramerateValue))
                _customFrameLimitValue = Math.Clamp(SelectedPreset.FramerateValue, 0, FrameLimitMaximum);

            OnPropertyChanged(nameof(FramerateLimits));
            OnPropertyChanged(nameof(IsCustomFrameLimitSelected));
            OnPropertyChanged(nameof(SelectedFrameLimit));
            OnPropertyChanged(nameof(AutoTDPMaximum));
            OnPropertyChanged(nameof(FrameLimitMaximum));
            OnPropertyChanged(nameof(CustomFrameLimitValue));
        }

        private void PerformanceManager_Initialized(bool CanChangeTDP, bool CanChangeGPU)
        {
            // manage events
            PerformanceManager.EPPChanged += PerformanceManager_EPPChanged;

            OnPropertyChanged(nameof(SupportsTDP));
            OnPropertyChanged(nameof(SupportsGPUFreq));
        }

        private void PerformanceManager_EPPChanged(uint epp)
        {
            if (SelectedPreset is not null)
                EPPOverrideValue = epp;
        }

        private void PowerProfileManager_Updated(PowerProfile preset, UpdateSource source)
        {
            // skip if self update
            if (source == (IsQuickTools ? UpdateSource.QuickProfilesPage : UpdateSource.ProfilesPage))
                return;

            // skip if not current preset
            if (source != UpdateSource.QuickProfilesCreation && source != UpdateSource.Creation)
                if (SelectedPreset?.Guid != preset.Guid)
                    return;

            // Update all properties
            if (ShouldUseCustomFrameLimit(preset.FramerateValue))
                _customFrameLimitValue = Math.Clamp(preset.FramerateValue, 0, FrameLimitMaximum);

            OnPropertyChanged(string.Empty);

            // Main Window only
            if (IsMainPage)
            {
                int index;
                ProfilesPickerViewModel? foundPreset = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == preset.Guid);
                if (foundPreset is not null)
                {
                    index = _profilePickerItems.IndexOf(foundPreset);
                    foundPreset.Text = preset.Name;
                }
                else
                {
                    index = 0;
                    _profilePickerItems.Insert(index, new() { LinkedPresetId = preset.Guid, Text = preset.Name, IsInternal = preset.IsDefault() || preset.IsDeviceDefault() });
                }

                OnPropertyChanged(nameof(ProfilePickerItems));
                SelectedPresetPicker = foundPreset ?? _profilePickerItems[index];
            }
        }

        private bool ShouldUseCustomFrameLimit(int framerateValue)
        {
            if (framerateValue == 0)
                return false;

            lock (_collectionLock)
            {
                return FramerateLimits.Any(vm => vm.IsCustom) && !FramerateLimits.Any(vm => !vm.IsCustom && vm.FrameLimit.limit == framerateValue);
            }
        }

        private void PowerProfileManager_Deleted(PowerProfile preset)
        {
            if (IsQuickTools)
            {
                if (SelectedPreset?.Guid == preset.Guid && OverlayQuickTools.GetCurrent().ContentFrame.CanGoBack)
                    OverlayQuickTools.GetCurrent().ContentFrame.GoBack();
            }
            else if (IsMainPage)
            {
                ProfilesPickerViewModel foundVm = _profilePickerItems.First(p => p.LinkedPresetId == preset.Guid);
                _profilePickerItems.Remove(foundVm);
                OnPropertyChanged(nameof(ProfilePickerItems));

                if (SelectedPreset?.Guid == preset.Guid)
                {
                    ProfilesPickerViewModel? defaultPicker = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == Guid.Empty);
                    if (defaultPicker is not null)
                        SelectedPresetPicker = defaultPicker;
                }
            }
        }

        #endregion

        // TODO: Get rid of View dependencies
        public void InitializeViewDependencies(CartesianChart fanGraph, LineSeries fanGraphLineSeries, ContentDialog modifyDialog)
        {
            _fanGraph = fanGraph;
            _fanGraphLineSeries = fanGraphLineSeries;
            _modifyDialog = modifyDialog;

            _fanGraphLineSeries.ActualValues.CollectionChanged += ActualValues_CollectionChanged;
            _fanGraph.DataClick += ChartOnDataClick;
            _fanGraph.MouseLeave += ChartMouseLeave;
            _fanGraph.MouseMove += ChartMouseMove;
            _fanGraph.MouseUp += ChartMouseUp;
            _fanGraph.TouchMove += ChartTouchMove;
            _fanGraph.PreviewTouchDown += _fanGraph_PreviewTouchDown;
        }

        private void _fanGraph_PreviewTouchDown(object? sender, TouchEventArgs e)
        {
            // used to prevent the page from scrolling during touch manipulation
            e.Handled = true;
        }

        private void ActualValues_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_updatingFanCurveUI || _fanGraphLineSeries is null) return;

            if (_isDragging)
            {
                _fanCurveDirty = true;
                return;
            }

            for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                SelectedPreset.FanProfile.fanSpeeds[idx] = Convert.ToDouble(_fanGraphLineSeries.ActualValues[idx] ?? 0.0d);
        }

        private void CommitFanCurveFromChart(bool submitPreset)
        {
            if (_fanGraphLineSeries is null || SelectedPreset is null)
                return;

            double[] fanSpeeds = SelectedPreset.FanProfile.fanSpeeds;
            int count = Math.Min(_fanGraphLineSeries.ActualValues.Count, fanSpeeds.Length);
            bool changed = false;

            for (int idx = 0; idx < count; idx++)
            {
                double value = Convert.ToDouble(_fanGraphLineSeries.ActualValues[idx] ?? 0.0d);
                if (Math.Abs(fanSpeeds[idx] - value) < Epsilon)
                    continue;

                fanSpeeds[idx] = value;
                changed = true;
            }

            _fanCurveDirty = false;

            if (changed && submitPreset)
                SubmitSelectedPreset();
        }

        private void ChartMovePoint(Point p)
        {
            if (_fanGraphLineSeries is null)
                return;

            int idx = ClampIndex(p.X);
            XPointer = idx;
            YPointer = Clamp01_100(p.Y);

            if (!_isDragging || _dragIndex < 0) return;

            double newY = Clamp01_100(p.Y);
            double currentY = Convert.ToDouble(_fanGraphLineSeries.ActualValues[_dragIndex] ?? 0.0d);
            if (Math.Abs(newY - currentY) < Epsilon) return;

            // NO _updatingFanCurveUI here — we WANT CollectionChanged to sync into SelectedPreset
            _fanGraphLineSeries.ActualValues[_dragIndex] = newY;

            // keep monotonic shape (forward)
            double carry = newY;
            for (int i = _dragIndex + 1; i < _fanGraphLineSeries.ActualValues.Count; i++)
            {
                double yi = Convert.ToDouble(_fanGraphLineSeries.ActualValues[i] ?? 0.0d);
                if (yi + Epsilon < carry) _fanGraphLineSeries.ActualValues[i] = carry;
                else carry = yi;
            }
            // backward
            carry = newY;
            for (int i = _dragIndex - 1; i >= 0; i--)
            {
                double yi = Convert.ToDouble(_fanGraphLineSeries.ActualValues[i] ?? 0.0d);
                if (yi - Epsilon > carry) _fanGraphLineSeries.ActualValues[i] = carry;
                else carry = yi;
            }
        }

        private void ChartMouseMove(object sender, MouseEventArgs e)
        {
            if (_fanGraph is null)
                return;

            ChartMovePoint(_fanGraph.ConvertToChartValues(e.GetPosition(_fanGraph)));
            e.Handled = true;
        }

        private void ChartTouchMove(object? sender, TouchEventArgs e)
        {
            if (_fanGraph is null)
                return;

            ChartMovePoint(_fanGraph.ConvertToChartValues(e.GetTouchPoint(_fanGraph).Position));
            e.Handled = true;
        }

        private void ChartMouseUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
        }

        private void ChartMouseLeave(object sender, MouseEventArgs e)
        {
            EndDrag();
        }

        private void EndDrag()
        {
            if (_fanGraph is null)
                return;

            if (!_isDragging) return;

            _isDragging = false;
            _dragIndex = -1;

            if (_fanCurveDirty)
                CommitFanCurveFromChart(submitPreset: true);

            if (Mouse.Captured == _fanGraph)
                _fanGraph.ReleaseMouseCapture();
        }

        private void ChartOnDataClick(object sender, ChartPoint chartPoint)
        {
            if (chartPoint == null || _fanGraph is null) return;

            // Convert the click position; cheaper than ClosestPointTo for your gridlike series
            Point p = _fanGraph.ConvertToChartValues(Mouse.GetPosition(_fanGraph));
            _dragIndex = ClampIndex(p.X);
            _isDragging = true;
            _fanGraph.CaptureMouse();
        }
    }
}
