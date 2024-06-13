using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using LiveCharts;
using LiveCharts.Definitions.Series;
using LiveCharts.Helpers;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Resources = HandheldCompanion.Properties.Resources;

namespace HandheldCompanion.ViewModels
{
    // ViewModel for Profiles Picker
    public class ProfilesPickerViewModel : BaseViewModel
    {
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
        public bool IsHeader { get; set; } = false;
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
                _selectedPreset = value;
                if (!IsQuickTools)
                {
                    _selectedPresetIndex = ProfilePickerItems.IndexOf(ProfilePickerItems.First(p => p.LinkedPresetId == _selectedPreset.Guid));
                }
                OnPropertyChanged(string.Empty); // Refresh all properties
            }
        }

        public bool IsQuickTools { get; }

        #region Binding Properties

        public double GPUFreqMinimum => IDevice.GetCurrent().GfxClock[0];
        public double GPUFreqMaximum => IDevice.GetCurrent().GfxClock[1];

        public double CPUFreqMinimum => MotherboardInfo.ProcessorMaxTurboSpeed / 4.0d;
        public double CPUFreqMaximum => MotherboardInfo.ProcessorMaxTurboSpeed;

        public double CPUCoreMaximum => MotherboardInfo.NumberOfCores;

        public bool SupportsSoftwareFanMode => IDevice.GetCurrent().Capabilities.HasFlag(Devices.DeviceCapabilities.FanControl);

        public bool SupportsAutoTDP
        {
            get
            {
                if (!PlatformManager.RTSS.IsInstalled)
                    return false;

                return PerformanceManager.GetProcessor()?.CanChangeTDP ?? false;
            }
        }

        public bool SupportsTDP => PerformanceManager.GetProcessor()?.CanChangeTDP ?? false;

        public bool SupportsGPUFreq => PerformanceManager.GetProcessor()?.CanChangeGPU ?? false;

        public bool CanChangePreset => !SelectedPreset.DeviceDefault;
        public bool CanDeletePreset => !SelectedPreset.Default;

        public bool HasWarning => !string.IsNullOrEmpty(Warning);

        public string Warning
        {
            get
            {
                if (SelectedPreset.DeviceDefault) return Resources.ProfilesPage_DefaultDeviceProfile;
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

        public double TDPMinimum
        {
            get => SettingsManager.GetDouble(Settings.ConfigurableTDPOverrideDown);
            set
            {
                if (value != TDPMinimum)
                {
                    SettingsManager.SetProperty(Settings.ConfigurableTDPOverrideDown, value);
                    OnPropertyChanged(nameof(TDPMinimum));
                }
            }
        }

        public double TDPMaximum
        {
            get => SettingsManager.GetDouble(Settings.ConfigurableTDPOverrideUp);
            set
            {
                if (value != TDPMaximum)
                {
                    SettingsManager.SetProperty(Settings.ConfigurableTDPOverrideUp, value);
                    OnPropertyChanged(nameof(TDPMaximum));
                }
            }
        }

        public double AutoTDPMaximum => MultimediaManager.GetDesktopScreen().devMode.dmDisplayFrequency;

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

        public double TDPOverrideValue
        {
            get
            {
                var tdpValues = SelectedPreset?.TDPOverrideValues ?? IDevice.GetCurrent().nTDP;
                return tdpValues[(int)PowerType.Slow];
            }
            set
            {
                if (value != TDPOverrideValue)
                {
                    SelectedPreset.TDPOverrideValues = [value, value, value];
                    OnPropertyChanged(nameof(TDPOverrideValue));
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

        public ICommand DeletePresetCommand { get; private set; }

        #endregion

        #region Main Window specific Bindings

        public Func<double, string> Formatter { get; private set; }

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
            get => _xPointer;
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
                if (value != _selectedPresetIndex && value >= 0 && value < ProfilePickerItems.Count)
                {
                    _selectedPresetIndex = value;
                    _selectedPreset = PowerProfileManager.GetProfile(ProfilePickerItems[_selectedPresetIndex].LinkedPresetId.Value);

                    OnPropertyChanged(string.Empty);
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

        public ObservableCollection<ProfilesPickerViewModel> ProfilePickerItems { get; } = new ObservableCollection<ProfilesPickerViewModel>();
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

        private bool _updatingProfile;
        private bool _updatingFanCurveUI;

        // Use these to easily rebuild 
        private ProfilesPickerViewModel _devicePresetsPickerVM;
        private ProfilesPickerViewModel _userPresetsPickerVM;

        private static HashSet<string> _skipPropertyChangedUpdate =
        [
            nameof(XPointer),
            nameof(YPointer),
            nameof(SelectedPresetIndex),
            nameof(ProfilePickerItems)
        ];

        public PerformancePageViewModel(bool isQuickTools)
        {
            _selectedPreset = PowerProfileManager.GetProfile(Guid.Empty);
            _selectedPresetIndex = 1;

            IsQuickTools = isQuickTools;

            Formatter = x => x.ToString("N2");

            #region General Setup

            SettingsManager.SettingValueChanged += SettingsManager_SettingsValueChanged;
            MultimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;
            PerformanceManager.ProcessorStatusChanged += PerformanceManager_ProcessorStatusChanged;
            PerformanceManager.EPPChanged += PerformanceManager_EPPChanged;
            PowerProfileManager.Updated += PowerProfileManager_Updated;
            PowerProfileManager.Deleted += PowerProfileManager_Deleted;

            PropertyChanged +=
                (sender, e) =>
                {
                    if (SelectedPreset is null)
                        return;

                    // TODO: Get rid of UI update here of fan graph UI dependency
                    if (!IsQuickTools)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _updatingFanCurveUI = true;
                            // update charts
                            for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                                _fanGraphLineSeries.ActualValues[idx] = SelectedPreset.FanProfile.fanSpeeds[idx];

                            _updatingFanCurveUI = false;
                        });

                        // No need to update 
                        if (_skipPropertyChangedUpdate.Contains(e.PropertyName))
                            return;
                    }

                    _updatingProfile = true;
                    PowerProfileManager.UpdateOrCreateProfile(SelectedPreset, UpdateSource.ProfilesPage);
                    _updatingProfile = false;
                };

            CreatePresetCommand = new DelegateCommand(() =>
            {
                // Get the count of profiles that are not default, then start with +1 of that
                int count = PowerProfileManager.profiles.Values.Count(p => !p.IsDefault());
                int idx = count + 1;

                // Create a base name for the new profile
                string baseName = Resources.PowerProfileManualName;

                // Check for duplicates and increment the index
                while (PowerProfileManager.profiles.Values.Any(p => p.Name == string.Format(baseName, idx)))
                    idx++;

                // Format the name with the updated index
                string name = string.Format(baseName, idx);

                // Create the new power profile
                PowerProfile powerProfile = new PowerProfile(name, Resources.PowerProfileManualDescription)
                {
                    TDPOverrideValues = IDevice.GetCurrent().nTDP
                };

                // Update or create the profile
                PowerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);
            });

            DeletePresetCommand = new DelegateCommand(async () =>
            {
                Task<ContentDialogResult> dialogTask = new Dialog(isQuickTools ? OverlayQuickTools.GetCurrent() : MainWindow.GetCurrent())
                {
                    Title = $"{Resources.ProfilesPage_AreYouSureDelete1} \"{SelectedPreset.Name}\"?",
                    Content = Resources.ProfilesPage_AreYouSureDelete2,
                    CloseButtonText = Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Resources.ProfilesPage_Delete
                }.ShowAsync();

                await dialogTask; // sync call

                switch (dialogTask.Result)
                {
                    case ContentDialogResult.Primary:
                        PowerProfileManager.DeleteProfile(SelectedPreset);
                        break;
                }
            });

            #endregion

            #region Main Window Setup

            if (!IsQuickTools)
            {
                _devicePresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_DevicePresets };
                _userPresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_UserPresets };

                ProfilePickerItems.Add(_devicePresetsPickerVM);
                ProfilePickerItems.Add(_userPresetsPickerVM);

                // Fill initial data
                foreach (var preset in PowerProfileManager.profiles.Values)
                {
                    var index = ProfilePickerItems.IndexOf(preset.IsDefault() ? _devicePresetsPickerVM : _userPresetsPickerVM) + 1;
                    ProfilePickerItems.Insert(index, new ProfilesPickerViewModel { Text = preset.Name, LinkedPresetId = preset.Guid });
                }
                
                // Reset Index to Default, 1 item before _userPresetsPickerVM
                _selectedPresetIndex = ProfilePickerItems.IndexOf(_userPresetsPickerVM) - 1;

                OpenModifyDialogCommand = new DelegateCommand(() =>
                {
                    ModifyPresetName = PresetName;
                    ModifyPresetDescription = PresetDescription;
                    _modifyDialog.ShowAsync();
                });

                ConfirmModifyCommand = new DelegateCommand(() =>
                {
                    // Update the name of the selected preset
                    SelectedPreset.Name = ModifyPresetName;

                    // Update the corresponding item in ProfilePickerItems
                    var selectedItem = ProfilePickerItems.FirstOrDefault(item => item.LinkedPresetId == SelectedPreset.Guid);
                    if (selectedItem != null)
                    {
                        PresetName = ModifyPresetName;
                        PresetDescription = ModifyPresetDescription;

                        selectedItem.Text = ModifyPresetName;
                    }
                });

                FanPresetSilentCommand = new DelegateCommand(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // update charts
                        for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                            _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[0][idx];

                        // Temporary until view dependencies could be removed
                        OnPropertyChanged("FanGraph");
                    });
                });

                FanPresetPerformanceCommand = new DelegateCommand(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // update charts
                        for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                            _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[1][idx];

                        // Temporary until view dependencies could be removed
                        OnPropertyChanged("FanGraph");
                    });
                });

                FanPresetTurboCommand = new DelegateCommand(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // update charts
                        for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                            _fanGraphLineSeries.ActualValues[idx] = IDevice.GetCurrent().fanPresets[2][idx];

                        // Temporary until view dependencies could be removed
                        OnPropertyChanged("FanGraph");
                    });
                });
            }

            #endregion
        }


        public override void Dispose()
        {
            SettingsManager.SettingValueChanged -= SettingsManager_SettingsValueChanged;
            MultimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
            PerformanceManager.ProcessorStatusChanged -= PerformanceManager_ProcessorStatusChanged;
            PerformanceManager.EPPChanged += PerformanceManager_EPPChanged;
            PowerProfileManager.Updated -= PowerProfileManager_Updated;
            PowerProfileManager.Deleted -= PowerProfileManager_Deleted;

            if (!IsQuickTools)
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

        private void SettingsManager_SettingsValueChanged(string name, object value)
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

        private void PerformanceManager_ProcessorStatusChanged(bool CanChangeTDP, bool CanChangeGPU)
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
            // Updated is triggered from PropertyChanged
            if (_updatingProfile) return;

            // Update all properties
            if (SelectedPreset?.Guid == preset.Guid)
                OnPropertyChanged(string.Empty);

            // Main Window only
            if (!IsQuickTools)
            {
                int index;
                var foundPreset = ProfilePickerItems.FirstOrDefault(p => p.LinkedPresetId == preset.Guid);
                if (foundPreset is not null)
                {
                    index = ProfilePickerItems.IndexOf(foundPreset);
                    foundPreset.Text = preset.Name;
                }
                else
                {
                    index = ProfilePickerItems.IndexOf(preset.IsDefault() ? _devicePresetsPickerVM : _userPresetsPickerVM) + 1;
                    ProfilePickerItems.Insert(index, new() { LinkedPresetId = preset.Guid, Text = preset.Name });
                }

                OnPropertyChanged(nameof(ProfilePickerItems));
                SelectedPresetIndex = index;
            }
        }

        private void PowerProfileManager_Deleted(PowerProfile preset)
        {
            if (IsQuickTools)
            {
                if (SelectedPreset?.Guid == preset.Guid && MainWindow.overlayquickTools.ContentFrame.CanGoBack)
                    MainWindow.overlayquickTools.ContentFrame.GoBack();
            }
            else
            {
                var foundVm = ProfilePickerItems.First(p => p.LinkedPresetId == preset.Guid);
                ProfilePickerItems.Remove(foundVm);
                OnPropertyChanged(nameof(ProfilePickerItems));
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
        }

        private void ActualValues_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_updatingFanCurveUI) return;

            for (int idx = 0; idx < _fanGraphLineSeries.ActualValues.Count; idx++)
                SelectedPreset.FanProfile.fanSpeeds[idx] = (double)_fanGraphLineSeries.ActualValues[idx];
        }

        private void ChartMovePoint(Point point)
        {
            if (_storedChartPoint is not null)
            {
                double pointY = Math.Max(0, Math.Min(100, point.Y));

                // update current poing value
                _fanGraphLineSeries.ActualValues[_storedChartPoint.Key] = pointY;

                // prevent higher values from having lower fan speed
                for (int i = _storedChartPoint.Key; i < _fanGraphLineSeries.ActualValues.Count; i++)
                {
                    if ((double)_fanGraphLineSeries.ActualValues[i] < pointY)
                        _fanGraphLineSeries.ActualValues[i] = pointY;
                }

                // prevent lower values from having higher fan speed
                for (int i = _storedChartPoint.Key; i >= 0; i--)
                {
                    if ((double)_fanGraphLineSeries.ActualValues[i] > pointY)
                        _fanGraphLineSeries.ActualValues[i] = pointY;
                }
            }

            ISeriesView series = _fanGraph.Series.First();
            ChartPoint closestPoint = series.ClosestPointTo(point.X, AxisOrientation.X);

            YPointer = closestPoint.Y;
            XPointer = closestPoint.X;
        }

        private void ChartMouseMove(object sender, MouseEventArgs e)
        {
            Point point = _fanGraph.ConvertToChartValues(e.GetPosition(_fanGraph));
            ChartMovePoint(point);
        }

        private void ChartTouchMove(object? sender, TouchEventArgs e)
        {
            Point point = _fanGraph.ConvertToChartValues(e.GetTouchPoint(_fanGraph).Position);
            ChartMovePoint(point);
            e.Handled = true;
        }

        private void ChartMouseUp(object sender, MouseButtonEventArgs e)
        {
            _storedChartPoint = null;
            // Temporary until view dependencies could be removed
            OnPropertyChanged("FanGraph");
        }

        private void ChartMouseLeave(object sender, MouseEventArgs e)
        {
            _storedChartPoint = null;
            // Temporary until view dependencies could be removed
            OnPropertyChanged("FanGraph");
        }

        private void ChartOnDataClick(object sender, ChartPoint p)
        {
            if (p is null)
                return;

            // store current point
            _storedChartPoint = p;
        }
    }
}
