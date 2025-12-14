using HandheldCompanion.Devices;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
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

                    // page-specific behaviors
                    switch (IsQuickTools)
                    {
                        case false:
                            ProfilesPickerViewModel profile = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == _selectedPreset.Guid);
                            if (profile is not null)
                                _selectedPresetIndex = ProfilePickerCollectionView.IndexOf(profile);
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

        public bool SupportsSoftwareFanMode => IDevice.GetCurrent().Capabilities.HasFlag(Devices.DeviceCapabilities.FanControl);
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

                return ManagerFactory.multimediaManager.PrimaryDesktop.devMode.dmDisplayFrequency;
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

                SelectedPreset.TDPOverrideValues[(int)PowerType.Slow] = clamped;
                SelectedPreset.TDPOverrideValues[(int)PowerType.Stapm] = clamped;

                // If PL1 crosses PL2, bump PL2 up to maintain PL2 >= PL1 + Δ
                double minPl2 = clamped + RequiredDelta;

                if (!_coerceGuard && PL2OverrideValue < minPl2)
                {
                    try
                    {
                        _coerceGuard = true;
                        SelectedPreset.TDPOverrideValues[(int)PowerType.Fast] = Math.Min(ConfigurableTDPOverrideUp, minPl2);
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

                if (SelectedPreset.TDPOverrideValues[(int)PowerType.Fast] != clamped)
                {
                    SelectedPreset.TDPOverrideValues[(int)PowerType.Fast] = clamped;
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
        public Func<double, string> TempTickFormatter => v => $"{v * 10:N0} °C";    // bottom axis ticks: 0,10,...100
        public Func<double, string> CpuXAxisFormatter => v => Math.Abs(v - CpuTempX) < 0.0001 ? $"{CpuTempX * 10:N2} °C" : string.Empty;    // top axis precise label

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
        public int SelectedPresetIndex
        {
            get => _selectedPresetIndex;
            set
            {
                // Ensure the index is within the bounds of the collection
                if (value != _selectedPresetIndex && value >= 0 && value < ProfilePickerCollectionView.Count)
                {
                    Guid? presetId = ((ProfilesPickerViewModel)ProfilePickerCollectionView.GetItemAt(value)).LinkedPresetId;
                    if (presetId is null)
                        return;

                    _selectedPresetIndex = value;
                    SelectedPreset = ManagerFactory.powerProfileManager.GetProfile(presetId.Value);
                }
            }
        }

        private string _modifyPresetName;
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

        private string _modifyPresetDescription;
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

        private ObservableCollection<ProfilesPickerViewModel> _profilePickerItems = [];
        public ListCollectionView ProfilePickerCollectionView { get; set; }
        public ICommand OpenModifyDialogCommand { get; private set; }
        public ICommand ConfirmModifyCommand { get; private set; }
        public ICommand CreatePresetCommand { get; private set; }
        public ICommand FanPresetSilentCommand { get; private set; }
        public ICommand FanPresetPerformanceCommand { get; private set; }
        public ICommand FanPresetTurboCommand { get; private set; }

        #endregion

        private ChartPoint? _storedChartPoint;
        private CartesianChart _fanGraph;
        private LineSeries _fanGraphLineSeries;
        private ContentDialog _modifyDialog;

        private bool _updatingFanCurveUI;

        private static HashSet<string> _skipPropertyChangedUpdate =
        [
            nameof(CpuTempC),
            nameof(CpuTempX),
            nameof(XPointer),
            nameof(YPointer),
            nameof(SelectedPresetIndex),
            nameof(ProfilePickerCollectionView),
            nameof(SupportsGPUFreq),
            nameof(SupportsIntelEnduranceGaming),
            nameof(SupportsAutoTDP),
            nameof(HasWarning),
            string.Empty,
        ];

        private ContentDialog contentDialog;

        public PerformancePageViewModel(bool isQuickTools)
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(_profilePickerItems, new object());

            ProfilePickerCollectionView = new ListCollectionView(_profilePickerItems);
            ProfilePickerCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("Header"));

            _selectedPreset = ManagerFactory.powerProfileManager.GetProfile(Guid.Empty);
            _selectedPresetIndex = 1;

            IsQuickTools = isQuickTools;

            Formatter = x => x.ToString("N2");

            #region General Setup

            // manage events
            PerformanceManager.Initialized += PerformanceManager_Initialized;
            PerformanceManager.EPPChanged += PerformanceManager_EPPChanged;

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

            PropertyChanged += (sender, e) =>
            {
                if (SelectedPreset is null || SelectedPreset.Name is null)
                    return;

                // skip PropertyChanged updates for specific properties
                switch (e.PropertyName)
                {
                    case "ModifyPresetName":
                    case "ModifyPresetDescription":
                    case "AutoTDPMaximum":
                    case "ConfigurableTDPOverride":
                    case "ConfigurableTDPOverrideDown":
                    case "ConfigurableTDPOverrideUp":
                    case "SupportsTDP":
                        return;
                }

                // TODO: Get rid of UI update here of fan graph UI dependency
                if (IsMainPage)
                {
                    switch (e.PropertyName)
                    {
                        case "":
                            UIHelper.TryInvoke(() =>
                            {
                                _updatingFanCurveUI = true;
                                for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                                    _fanGraphLineSeries.ActualValues[idx] = SelectedPreset.FanProfile.fanSpeeds[idx];
                                _updatingFanCurveUI = false;
                            });
                            break;
                    }
                }

                // No need to update 
                if (_skipPropertyChangedUpdate.Contains(e.PropertyName))
                    return;

                // trigger power profile update but don't freeze UI
                // todo: implement proper debounce
                Task.Run(() =>
                {
                    ManagerFactory.powerProfileManager.UpdateOrCreateProfile(SelectedPreset, IsQuickTools ? UpdateSource.QuickProfilesPage : UpdateSource.ProfilesPage);
                });
            };

            CreatePresetCommand = new DelegateCommand(() =>
            {
                // Get the count of profiles that are not default, then start with +1 of that
                int count = ManagerFactory.powerProfileManager.profiles.Values.Count(p => !p.IsDefault());
                int idx = count + 1;

                // Create a base name for the new profile
                string baseName = Resources.PowerProfileManualName;

                // Check for duplicates and increment the index
                while (ManagerFactory.powerProfileManager.profiles.Values.Any(p => p.Name == string.Format(baseName, idx)))
                    idx++;

                // Format the name with the updated index
                string name = string.Format(baseName, idx);

                // Create the new power profile
                PowerProfile powerProfile = new PowerProfile(name, Resources.PowerProfileManualDescription)
                {
                    TDPOverrideValues = IDevice.GetCurrent().nTDP
                };

                // Update or create the profile
                ManagerFactory.powerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);
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

            #endregion

            #region Main Window Setup
            if (IsMainPage)
            {
                OpenModifyDialogCommand = new DelegateCommand(() =>
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

                    contentDialog.ShowAsync();
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
                    UIHelper.TryInvoke(() =>
                    {
                        // update charts
                        for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                            _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[0][idx];
                    });

                    // Temporary until view dependencies could be removed
                    OnPropertyChanged("FanGraphPreset");
                });

                FanPresetPerformanceCommand = new DelegateCommand(() =>
                {
                    UIHelper.TryInvoke(() =>
                    {
                        // update charts
                        for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                            _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[1][idx];
                    });

                    // Temporary until view dependencies could be removed
                    OnPropertyChanged("FanGraphPreset");
                });

                FanPresetTurboCommand = new DelegateCommand(() =>
                {
                    UIHelper.TryInvoke(() =>
                    {
                        // update charts
                        for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                            _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[2][idx];
                    });

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

            OnPropertyChanged(nameof(SupportsAutoTDP));
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

            GPU gpu = GPUManager.GetCurrent();
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
            /*
             * case "ConfigurableTDPOverride":
             * case "ConfigurableTDPOverrideDown":
             * case "ConfigurableTDPOverrideUp":
            */

            OnPropertyChanged("ConfigurableTDPOverride");
        }

        private void QueryPowerProfile()
        {
            // manage events
            ManagerFactory.powerProfileManager.Updated += PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted += PowerProfileManager_Deleted;

            if (IsMainPage)
            {
                UIHelper.TryInvoke(() =>
                {
                    foreach (PowerProfile powerProfile in ManagerFactory.powerProfileManager.profiles.Values)
                        PowerProfileManager_Updated(powerProfile, UpdateSource.Creation);

                    // Reset Index to Default
                    ProfilesPickerViewModel profile = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == Guid.Empty);
                    if (profile is not null)
                        SelectedPresetIndex = _profilePickerItems.IndexOf(profile);
                });
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
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
            ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
            PerformanceManager.EPPChanged += PerformanceManager_EPPChanged;
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
                _fanGraphLineSeries.ActualValues.CollectionChanged -= ActualValues_CollectionChanged;
                _fanGraph.DataClick -= ChartOnDataClick;
                _fanGraph.MouseLeave -= ChartMouseLeave;
                _fanGraph.MouseMove -= ChartMouseMove;
                _fanGraph.MouseUp -= ChartMouseUp;
                _fanGraph.TouchMove -= ChartTouchMove;
            }

            base.Dispose();
        }

        #region Events

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "ConfigurableTDPOverride":
                case "ConfigurableTDPOverrideDown":
                case "ConfigurableTDPOverrideUp":
                    OnPropertyChanged(name);
                    break;
            }
        }

        private void MultimediaManager_PrimaryScreenChanged(DesktopScreen screen)
        {
            OnPropertyChanged(nameof(AutoTDPMaximum));
        }

        private void PerformanceManager_Initialized(bool CanChangeTDP, bool CanChangeGPU)
        {
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

                OnPropertyChanged(nameof(ProfilePickerCollectionView));
                SelectedPresetIndex = index;
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
                OnPropertyChanged(nameof(ProfilePickerCollectionView));

                if (SelectedPreset?.Guid == preset.Guid)
                    SelectedPresetIndex = 1;
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
            if (_updatingFanCurveUI) return;

            for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                SelectedPreset.FanProfile.fanSpeeds[idx] = (double)_fanGraphLineSeries.ActualValues[idx];
        }

        private void ChartMovePoint(Point p)
        {
            int idx = ClampIndex(p.X);
            XPointer = idx;
            YPointer = Clamp01_100(p.Y);

            if (!_isDragging || _dragIndex < 0) return;

            double newY = Clamp01_100(p.Y);
            double currentY = (double)_fanGraphLineSeries.ActualValues[_dragIndex];
            if (Math.Abs(newY - currentY) < Epsilon) return;

            // NO _updatingFanCurveUI here — we WANT CollectionChanged to sync into SelectedPreset
            _fanGraphLineSeries.ActualValues[_dragIndex] = newY;

            // keep monotonic shape (forward)
            double carry = newY;
            for (int i = _dragIndex + 1; i < _fanGraphLineSeries.ActualValues.Count; i++)
            {
                double yi = (double)_fanGraphLineSeries.ActualValues[i];
                if (yi + Epsilon < carry) _fanGraphLineSeries.ActualValues[i] = carry;
                else carry = yi;
            }
            // backward
            carry = newY;
            for (int i = _dragIndex - 1; i >= 0; i--)
            {
                double yi = (double)_fanGraphLineSeries.ActualValues[i];
                if (yi - Epsilon > carry) _fanGraphLineSeries.ActualValues[i] = carry;
                else carry = yi;
            }
        }

        private void ChartMouseMove(object sender, MouseEventArgs e)
        {
            ChartMovePoint(_fanGraph.ConvertToChartValues(e.GetPosition(_fanGraph)));
            e.Handled = true;
        }

        private void ChartTouchMove(object? sender, TouchEventArgs e)
        {
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
            if (!_isDragging) return;

            _isDragging = false;
            _dragIndex = -1;

            if (Mouse.Captured == _fanGraph)
                _fanGraph.ReleaseMouseCapture();

            OnPropertyChanged("FanGraph");
        }

        private void ChartOnDataClick(object sender, ChartPoint chartPoint)
        {
            if (chartPoint == null) return;

            // Convert the click position; cheaper than ClosestPointTo for your gridlike series
            Point p = _fanGraph.ConvertToChartValues(Mouse.GetPosition(_fanGraph));
            _dragIndex = ClampIndex(p.X);
            _isDragging = true;
            _fanGraph.CaptureMouse();
        }
    }
}
