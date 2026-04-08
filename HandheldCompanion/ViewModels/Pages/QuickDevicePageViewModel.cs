using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views.QuickPages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;

namespace HandheldCompanion.ViewModels
{
    public class QuickDevicePageViewModel : BaseViewModel
    {
        private QuickDevicePage quickDevicePage;
        private IReadOnlyList<Radio> radios;
        private DispatcherTimer radioTimer;

        // Flag to prevent circular updates when loading display settings into UI controls
        private bool isLoadingDisplay = false;

        // Collections
        public ObservableCollection<ScreenResolution> Resolutions { get; } = new();
        public ObservableCollection<ScreenFrequencyViewModel> Frequencies { get; } = new();

        // Device Capabilities
        private bool _IsDynamicLightingSupported;
        public bool IsDynamicLightingSupported
        {
            get => _IsDynamicLightingSupported;
            set { if (value != _IsDynamicLightingSupported) { _IsDynamicLightingSupported = value; OnPropertyChanged(nameof(IsDynamicLightingSupported)); } }
        }

        private Visibility _FanOverridePanelVisibility;
        public Visibility FanOverridePanelVisibility
        {
            get => _FanOverridePanelVisibility;
            set { if (value != _FanOverridePanelVisibility) { _FanOverridePanelVisibility = value; OnPropertyChanged(nameof(FanOverridePanelVisibility)); } }
        }

        private Visibility _AYANEOFlipDSPanelVisibility;
        public Visibility AYANEOFlipDSPanelVisibility
        {
            get => _AYANEOFlipDSPanelVisibility;
            set { if (value != _AYANEOFlipDSPanelVisibility) { _AYANEOFlipDSPanelVisibility = value; OnPropertyChanged(nameof(AYANEOFlipDSPanelVisibility)); } }
        }

        // Night Light
        private bool _IsNightLightSupported;
        public bool IsNightLightSupported
        {
            get => _IsNightLightSupported;
            set { if (value != _IsNightLightSupported) { _IsNightLightSupported = value; OnPropertyChanged(nameof(IsNightLightSupported)); } }
        }

        private bool _IsNightLightEnabled;
        public bool IsNightLightEnabled
        {
            get => _IsNightLightEnabled;
            set
            {
                if (value != _IsNightLightEnabled)
                {
                    _IsNightLightEnabled = value;
                    OnPropertyChanged(nameof(IsNightLightEnabled));

                    if (!isLoadingDisplay)
                        NightLight.Enabled = value;
                }
            }
        }

        // Dynamic Lighting
        private bool _IsDynamicLightingEnabled;
        public bool IsDynamicLightingEnabled
        {
            get => _IsDynamicLightingEnabled;
            set
            {
                if (value != _IsDynamicLightingEnabled)
                {
                    _IsDynamicLightingEnabled = value;
                    OnPropertyChanged(nameof(IsDynamicLightingEnabled));

                    if (!isLoadingDisplay)
                        ManagerFactory.settingsManager.SetProperty("LEDSettingsEnabled", value);
                }
            }
        }

        // WiFi
        private bool _IsWiFiSupported;
        public bool IsWiFiSupported
        {
            get => _IsWiFiSupported;
            set { if (value != _IsWiFiSupported) { _IsWiFiSupported = value; OnPropertyChanged(nameof(IsWiFiSupported)); } }
        }

        private bool _IsWiFiEnabled;
        public bool IsWiFiEnabled
        {
            get => _IsWiFiEnabled;
            set
            {
                if (value != _IsWiFiEnabled)
                {
                    _IsWiFiEnabled = value;
                    OnPropertyChanged(nameof(IsWiFiEnabled));

                    if (!isLoadingDisplay && radios != null)
                    {
                        foreach (Radio radio in radios.Where(r => r.Kind == RadioKind.WiFi))
                            _ = radio.SetStateAsync(value ? RadioState.On : RadioState.Off);
                    }
                }
            }
        }

        // Bluetooth
        private bool _IsBluetoothSupported;
        public bool IsBluetoothSupported
        {
            get => _IsBluetoothSupported;
            set { if (value != _IsBluetoothSupported) { _IsBluetoothSupported = value; OnPropertyChanged(nameof(IsBluetoothSupported)); } }
        }

        private bool _IsBluetoothEnabled;
        public bool IsBluetoothEnabled
        {
            get => _IsBluetoothEnabled;
            set
            {
                if (value != _IsBluetoothEnabled)
                {
                    _IsBluetoothEnabled = value;
                    OnPropertyChanged(nameof(IsBluetoothEnabled));

                    if (!isLoadingDisplay && radios != null)
                    {
                        foreach (Radio radio in radios.Where(r => r.Kind == RadioKind.Bluetooth))
                            _ = radio.SetStateAsync(value ? RadioState.On : RadioState.Off);
                    }
                }
            }
        }

        // AYANEO Flip DS
        private bool _AYANEOFlipScreenEnabled;
        public bool AYANEOFlipScreenEnabled
        {
            get => _AYANEOFlipScreenEnabled;
            set
            {
                if (value != _AYANEOFlipScreenEnabled)
                {
                    _AYANEOFlipScreenEnabled = value;
                    OnPropertyChanged(nameof(AYANEOFlipScreenEnabled));

                    if (!isLoadingDisplay)
                        HandleAYANEOFlipScreenToggle(value);
                }
            }
        }

        private double _AYANEOFlipScreenBrightness;
        public double AYANEOFlipScreenBrightness
        {
            get => _AYANEOFlipScreenBrightness;
            set
            {
                if (value != _AYANEOFlipScreenBrightness && !double.IsNaN(value))
                {
                    _AYANEOFlipScreenBrightness = value;
                    OnPropertyChanged(nameof(AYANEOFlipScreenBrightness));

                    if (!isLoadingDisplay)
                        ManagerFactory.settingsManager.SetProperty("AYANEOFlipScreenBrightness", value);
                }
            }
        }

        // Fan Override
        private bool _FanOverrideEnabled;
        public bool FanOverrideEnabled
        {
            get => _FanOverrideEnabled;
            set
            {
                if (value != _FanOverrideEnabled)
                {
                    _FanOverrideEnabled = value;
                    OnPropertyChanged(nameof(FanOverrideEnabled));

                    if (!isLoadingDisplay)
                    {
                        if (IDevice.GetCurrent() is LegionGo device)
                            device.SetFanFullSpeed(value);
                        else if (IDevice.GetCurrent() is ClawA2VM claw8)
                            claw8.SetFanFullSpeed(value);
                    }
                }
            }
        }

        // Display Settings
        private bool _IsDisplayStackEnabled;
        public bool IsDisplayStackEnabled
        {
            get => _IsDisplayStackEnabled;
            set { if (value != _IsDisplayStackEnabled) { _IsDisplayStackEnabled = value; OnPropertyChanged(nameof(IsDisplayStackEnabled)); } }
        }

        private Visibility _ResolutionOverrideStackVisibility;
        public Visibility ResolutionOverrideStackVisibility
        {
            get => _ResolutionOverrideStackVisibility;
            set { if (value != _ResolutionOverrideStackVisibility) { _ResolutionOverrideStackVisibility = value; OnPropertyChanged(nameof(ResolutionOverrideStackVisibility)); } }
        }

        private ScreenResolution _SelectedResolution;
        public ScreenResolution SelectedResolution
        {
            get => _SelectedResolution;
            set
            {
                if (value != _SelectedResolution)
                {
                    _SelectedResolution = value;
                    OnPropertyChanged(nameof(SelectedResolution));

                    if (!isLoadingDisplay && value != null)
                    {
                        UpdateFrequenciesForResolution(value);
                        ApplyResolution();
                    }
                }
            }
        }

        private ScreenFrequencyViewModel _SelectedFrequency;
        public ScreenFrequencyViewModel SelectedFrequency
        {
            get => _SelectedFrequency;
            set
            {
                if (value != _SelectedFrequency)
                {
                    _SelectedFrequency = value;
                    OnPropertyChanged(nameof(SelectedFrequency));

                    if (!isLoadingDisplay && value != null)
                    {
                        ApplyResolution();
                    }
                }
            }
        }

        // Event for requesting dialog from View
        public event EventHandler<TaskCompletionSource<bool>> RequestAYANEOFlipScreenConfirmation;

        public QuickDevicePageViewModel(QuickDevicePage quickDevicePage)
        {
            this.quickDevicePage = quickDevicePage;

            // Enable collection synchronization for cross-thread access
            BindingOperations.EnableCollectionSynchronization(Resolutions, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(Frequencies, _collectionLock);

            // Initialize device capabilities
            IDevice currentDevice = IDevice.GetCurrent();
            AYANEOFlipDSPanelVisibility = currentDevice is AYANEOFlipDS ? Visibility.Visible : Visibility.Collapsed;
            IsDynamicLightingSupported = currentDevice.Capabilities.HasFlag(DeviceCapabilities.DynamicLighting);
            FanOverridePanelVisibility = currentDevice.Capabilities.HasFlag(DeviceCapabilities.FanOverride) ? Visibility.Visible : Visibility.Collapsed;

            // Initialize Night Light
            IsNightLightSupported = NightLight.Supported;
            IsNightLightEnabled = NightLight.Enabled;

            // Setup manager events
            SetupManagerEvents();

            // Start radio timer
            InitializeRadioTimer();
        }

        private void SetupManagerEvents()
        {
            NightLight.Toggled += NightLight_Toggled;

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

            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfiles();
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
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // Load initial settings
            isLoadingDisplay = true;
            try
            {
                IsDynamicLightingEnabled = ManagerFactory.settingsManager.GetBoolean("LEDSettingsEnabled");

                IDevice currentDevice = IDevice.GetCurrent();
                if (currentDevice is AYANEOFlipDS)
                {
                    AYANEOFlipScreenEnabled = ManagerFactory.settingsManager.GetBoolean("AYANEOFlipScreenEnabled");
                    AYANEOFlipScreenBrightness = ManagerFactory.settingsManager.GetDouble("AYANEOFlipScreenBrightness");
                }
            }
            finally
            {
                isLoadingDisplay = false;
            }
        }

        private void ProfileManager_Initialized()
        {
            QueryProfiles();
        }

        private void QueryProfiles()
        {
            // manage events
            ManagerFactory.profileManager.Applied += ProfileManager_Applied;
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Discarded += ProfileManager_Discarded;

            ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
        }

        private void InitializeRadioTimer()
        {
            radioTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(5) // Reduced frequency to improve performance
            };
            radioTimer.Tick += RadioTimer_Tick;
            radioTimer.Start();
        }

        private async void RadioTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var adapter = await BluetoothAdapter.GetDefaultAsync();
                radios = await Radio.GetRadiosAsync();

                isLoadingDisplay = true;
                try
                {
                    IsWiFiSupported = radios?.Any(r => r.Kind == RadioKind.WiFi) == true;
                    IsBluetoothSupported = radios?.Any(r => r.Kind == RadioKind.Bluetooth) == true;

                    var wifi = radios?.FirstOrDefault(r => r.Kind == RadioKind.WiFi);
                    var bt = radios?.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);

                    IsWiFiEnabled = wifi?.State == RadioState.On;
                    IsBluetoothEnabled = bt?.State == RadioState.On;
                }
                finally
                {
                    isLoadingDisplay = false;
                }
            }
            catch { }
        }

        private void QueryMedia()
        {
            ManagerFactory.multimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;
            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;

            if (ManagerFactory.multimediaManager.PrimaryDesktop is not null)
            {
                MultimediaManager_PrimaryScreenChanged(ManagerFactory.multimediaManager.PrimaryDesktop);
                MultimediaManager_DisplaySettingsChanged(ManagerFactory.multimediaManager.PrimaryDesktop,
                    ManagerFactory.multimediaManager.PrimaryDesktop.GetResolution());
            }
        }

        private void MultimediaManager_Initialized()
        {
            QueryMedia();
        }

        private void MultimediaManager_PrimaryScreenChanged(DesktopScreen screen)
        {
            UIHelper.TryInvoke(() =>
            {
                isLoadingDisplay = true;
                try
                {
                    Resolutions.Clear();
                    foreach (ScreenResolution resolution in screen.screenResolutions)
                        Resolutions.Add(resolution);
                }
                finally
                {
                    isLoadingDisplay = false;
                }
            });
        }

        private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
        {
            UIHelper.TryInvoke(() =>
            {
                isLoadingDisplay = true;
                try
                {
                    // Don't change display settings if profile has integer scaling enabled
                    Profile? currentProfile = ManagerFactory.profileManager.GetCurrent();
                    if (currentProfile is not null && currentProfile.IntegerScalingEnabled)
                    {
                        ProfileManager_Applied(currentProfile, UpdateSource.Background);
                        return;
                    }

                    if (resolution != SelectedResolution)
                    {
                        SelectedResolution = resolution;
                        UpdateFrequenciesForResolution(resolution);
                    }

                    // Select current frequency
                    int screenFrequency = desktopScreen.GetCurrentFrequency();
                    foreach (ScreenFrequencyViewModel item in Frequencies)
                    {
                        if (item.Frequency == screenFrequency)
                        {
                            SelectedFrequency = item;
                            break;
                        }
                    }
                }
                finally
                {
                    isLoadingDisplay = false;
                }
            });
        }

        private void UpdateFrequenciesForResolution(ScreenResolution resolution)
        {
            // Store current selection before clearing
            int? currentSelectedFrequency = SelectedFrequency?.Frequency;

            Frequencies.Clear();
            foreach (int frequency in resolution.Frequencies.Keys)
            {
                Frequencies.Add(new ScreenFrequencyViewModel(frequency));
            }

            // Restore selection if it exists in the new list
            if (currentSelectedFrequency.HasValue && Frequencies.Any())
            {
                var matchingFrequency = Frequencies.FirstOrDefault(f => f.Frequency == currentSelectedFrequency.Value);
                if (matchingFrequency != null)
                    SelectedFrequency = matchingFrequency;
            }
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            // Go to profile integer scaling resolution
            if (profile.IntegerScalingEnabled)
            {
                DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                if (desktopScreen == null)
                    return;

                ScreenDivider? profileResolution = desktopScreen.screenDividers.FirstOrDefault(d => d.divider == profile.IntegerScalingDivider);
                if (profileResolution is not null)
                    SetResolution(profileResolution.resolution);
            }

            // Update UI
            UpdateDisplayOverrideUI(profile);
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // Only update UI if this is the currently applied profile
            if (!isCurrent)
                return;

            // Update UI based on the updated profile's integer scaling state
            UpdateDisplayOverrideUI(profile);
        }

        private void UpdateDisplayOverrideUI(Profile profile)
        {
            UIHelper.TryInvoke(() =>
            {
                isLoadingDisplay = true;
                try
                {
                    var canChangeDisplay = !profile.IntegerScalingEnabled;
                    IsDisplayStackEnabled = canChangeDisplay;
                    ResolutionOverrideStackVisibility = canChangeDisplay ? Visibility.Collapsed : Visibility.Visible;
                }
                finally
                {
                    isLoadingDisplay = false;
                }
            });
        }

        private void ProfileManager_Discarded(Profile profile, bool swapped, Profile nextProfile)
        {
            // Don't bother discarding settings, new one will be enforced shortly
            if (swapped && nextProfile.IntegerScalingEnabled)
                return;

            if (profile.IntegerScalingEnabled)
            {
                UIHelper.TryInvoke(() =>
                {
                    isLoadingDisplay = true;
                    try
                    {
                        IsDisplayStackEnabled = true;
                        ResolutionOverrideStackVisibility = Visibility.Collapsed;
                    }
                    finally
                    {
                        isLoadingDisplay = false;
                    }
                });

                // Restore default resolution by reapplying current selection
                if (profile.IntegerScalingDivider != 1 && SelectedResolution != null)
                {
                    DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
                    if (desktopScreen != null)
                        SetResolution(desktopScreen.GetResolution());
                }
            }
        }

        private void NightLight_Toggled(bool enabled)
        {
            UIHelper.TryInvoke(() =>
            {
                isLoadingDisplay = true;
                try
                {
                    IsNightLightEnabled = enabled;
                }
                finally
                {
                    isLoadingDisplay = false;
                }
            });
        }

        private void SettingsManager_SettingValueChanged(string? name, object value, bool temporary)
        {
            UIHelper.TryInvoke(() =>
            {
                isLoadingDisplay = true;
                try
                {
                    switch (name)
                    {
                        case "LEDSettingsEnabled":
                            IsDynamicLightingEnabled = Convert.ToBoolean(value);
                            break;
                        case "AYANEOFlipScreenEnabled":
                            AYANEOFlipScreenEnabled = Convert.ToBoolean(value);
                            break;
                        case "AYANEOFlipScreenBrightness":
                            AYANEOFlipScreenBrightness = Convert.ToDouble(value);
                            break;
                    }
                }
                finally
                {
                    isLoadingDisplay = false;
                }
            });
        }

        private async void HandleAYANEOFlipScreenToggle(bool enabled)
        {
            if (!enabled)
            {
                // Check if dialog handler is registered to avoid hanging
                if (RequestAYANEOFlipScreenConfirmation == null)
                {
                    // No dialog handler, just update the setting
                    ManagerFactory.settingsManager.SetProperty("AYANEOFlipScreenEnabled", enabled);
                    return;
                }

                // Request confirmation from View
                var tcs = new TaskCompletionSource<bool>();
                RequestAYANEOFlipScreenConfirmation.Invoke(this, tcs);

                bool confirmed = await tcs.Task;
                if (confirmed)
                {
                    ManagerFactory.settingsManager.SetProperty("AYANEOFlipScreenEnabled", enabled);
                }
                else
                {
                    // Restore previous state
                    isLoadingDisplay = true;
                    try
                    {
                        AYANEOFlipScreenEnabled = true;
                    }
                    finally
                    {
                        isLoadingDisplay = false;
                    }
                }
            }
            else
            {
                ManagerFactory.settingsManager.SetProperty("AYANEOFlipScreenEnabled", enabled);
            }
        }

        private void ApplyResolution()
        {
            if (SelectedResolution == null || SelectedFrequency == null)
                return;

            int frequency = SelectedFrequency.Frequency;

            DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
            if (desktopScreen == null)
                return;

            try
            {
                // Don't apply if already at this resolution
                if (desktopScreen.devMode.dmPelsWidth == SelectedResolution.Width &&
                    desktopScreen.devMode.dmPelsHeight == SelectedResolution.Height &&
                    desktopScreen.devMode.dmDisplayFrequency == frequency &&
                    desktopScreen.devMode.dmBitsPerPel == SelectedResolution.BitsPerPel)
                    return;

                ManagerFactory.multimediaManager.SetResolution(
                    SelectedResolution.Width,
                    SelectedResolution.Height,
                    frequency,
                    SelectedResolution.BitsPerPel);
            }
            catch { }
        }

        public void SetResolution(ScreenResolution resolution)
        {
            DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
            if (desktopScreen == null)
                return;

            try
            {
                // Get current frequency
                int currentFrequency = desktopScreen.GetCurrentFrequency();

                // Find the closest available frequency in the new resolution
                int targetFrequency = GetClosestFrequency(resolution, currentFrequency);

                ManagerFactory.multimediaManager.SetResolution(
                    resolution.Width,
                    resolution.Height,
                    targetFrequency,
                    resolution.BitsPerPel);
            }
            catch { }
        }

        /// <summary>
        /// Finds the closest available frequency in a resolution's frequency list.
        /// </summary>
        private int GetClosestFrequency(ScreenResolution resolution, int targetFrequency)
        {
            if (resolution?.Frequencies == null || !resolution.Frequencies.Any())
                return targetFrequency;

            // If target frequency exists, use it
            if (resolution.Frequencies.ContainsKey(targetFrequency))
                return targetFrequency;

            // Find closest frequency by distance, then prefer higher if equal distance
            var closestFrequency = resolution.Frequencies.Keys
                .Select(freq => new { freq, distance = Math.Abs(freq - targetFrequency) })
                .OrderBy(x => x.distance)
                .ThenByDescending(x => x.freq) // Prefer higher frequency if distances are equal
                .First();

            return closestFrequency.freq;
        }

        public void Close()
        {
            ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
            ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
            ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
            ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
            ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
            ManagerFactory.profileManager.Discarded -= ProfileManager_Discarded;
            NightLight.Toggled -= NightLight_Toggled;

            if (radioTimer != null)
            {
                radioTimer.Stop();
                radioTimer.Tick -= RadioTimer_Tick;
                radioTimer = null;
            }

            quickDevicePage = null;

            Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
