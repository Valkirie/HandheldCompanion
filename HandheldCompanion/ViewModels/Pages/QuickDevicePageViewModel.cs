using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views.QuickPages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;

namespace HandheldCompanion.ViewModels
{
    public class QuickDevicePageViewModel : BaseViewModel
    {
        private QuickDevicePage? quickDevicePage;
        private IReadOnlyList<Radio>? radios;
        private Radio? wifiRadio;
        private Radio? bluetoothRadio;

        // Flag to prevent circular updates when loading display settings into UI controls
        private bool isLoadingDisplay = false;

        // Collections
        public ObservableCollection<ScreenResolution> Resolutions { get; } = new();
        public ObservableCollection<ScreenFrequencyViewModel> Frequencies { get; } = new();

        // Monitor picker, UI state only: a monitor picked by the user is kept until the page is shown again
        private Guid _selectedMonitorId = Guid.Empty;
        private bool _monitorPickedByUser = false;
        private bool isLoadingMonitors = false;
        private int _displayRefreshQueued = 0;

        // monitor the resolutions list was built for
        private Guid _listedMonitorId = Guid.Empty;

        // serializes the profile saves of display mode changes
        private readonly object _profileSaveLock = new();

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

        private IReadOnlyList<DisplayMonitorItem> _DisplayMonitorItems = [];
        public IReadOnlyList<DisplayMonitorItem> DisplayMonitorItems
        {
            get => _DisplayMonitorItems;
            private set
            {
                _DisplayMonitorItems = value;
                OnPropertyChanged(nameof(DisplayMonitorItems));
                OnPropertyChanged(nameof(HasMultipleDisplays));
                OnPropertyChanged(nameof(HasDisplayMonitors));
                OnPropertyChanged(nameof(IsSingleDisplay));
                OnPropertyChanged(nameof(SingleDisplayMonitor));
            }
        }

        // several monitors get a picker, a single one is shown as read-only text
        public bool HasMultipleDisplays => _DisplayMonitorItems.Count > 1;
        public bool HasDisplayMonitors => _DisplayMonitorItems.Count > 0;
        public bool IsSingleDisplay => _DisplayMonitorItems.Count == 1;
        public DisplayMonitorItem? SingleDisplayMonitor => IsSingleDisplay ? _DisplayMonitorItems[0] : null;

        public Guid SelectedDisplayMonitor
        {
            get => _selectedMonitorId;
            set
            {
                if (isLoadingMonitors || value == Guid.Empty || value == _selectedMonitorId)
                    return;

                if (!ManagerFactory.multimediaManager.GetConnectedMonitors().Any(m => m.KnownMonitorId == value))
                    return;

                _selectedMonitorId = value;
                _monitorPickedByUser = true;
                OnPropertyChanged(nameof(SelectedDisplayMonitor));

                Profile? gameProfile = GetActiveGameProfile(out int slot);
                LoadSelectedMonitorModes(gameProfile, slot);
            }
        }

        // where a change is stored while a game profile is applied, empty otherwise
        private string _DisplaySaveNote = string.Empty;
        public string DisplaySaveNote
        {
            get => _DisplaySaveNote;
            private set
            {
                if (value != _DisplaySaveNote)
                {
                    _DisplaySaveNote = value;
                    OnPropertyChanged(nameof(DisplaySaveNote));
                    OnPropertyChanged(nameof(HasDisplaySaveNote));
                }
            }
        }

        public bool HasDisplaySaveNote => !string.IsNullOrEmpty(_DisplaySaveNote);

        private ScreenResolution _SelectedResolution = null!;
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
                        // list the refresh rates of the new resolution, keeping the current one selected when possible;
                        // selecting it is not a user change of the refresh rate
                        int? currentFrequency = GetSelectedScreen()?.GetCurrentFrequency();

                        isLoadingDisplay = true;
                        try
                        {
                            UpdateFrequenciesForResolution(value, currentFrequency);
                        }
                        finally
                        {
                            isLoadingDisplay = false;
                        }

                        ApplyDisplayChange(true);
                    }
                }
            }
        }

        private ScreenFrequencyViewModel _SelectedFrequency = null!;
        public ScreenFrequencyViewModel SelectedFrequency
        {
            get => _SelectedFrequency;
            set
            {
                if (value != _SelectedFrequency)
                {
                    _SelectedFrequency = value;
                    OnPropertyChanged(nameof(SelectedFrequency));
                    OnPropertyChanged(nameof(SelectedFrequencyValue));

                    if (!isLoadingDisplay)
                        ApplyDisplayChange(false);
                }
            }
        }

        public int? SelectedFrequencyValue
        {
            get => _SelectedFrequency?.Frequency;
            set
            {
                if (!value.HasValue)
                    return;

                if (_SelectedFrequency?.Frequency == value.Value)
                    return;

                ScreenFrequencyViewModel? matchingFrequency;
                if (!Monitor.TryEnter(_collectionLock2, TimeSpan.FromSeconds(2)))
                    return;

                try
                {
                    matchingFrequency = Frequencies.FirstOrDefault(f => f.Frequency == value.Value);
                }
                finally
                {
                    Monitor.Exit(_collectionLock2);
                }

                if (matchingFrequency is not null)
                    SelectedFrequency = matchingFrequency;
            }
        }

        // Event for requesting dialog from View
        public event EventHandler<TaskCompletionSource<bool>>? RequestAYANEOFlipScreenConfirmation;

        public QuickDevicePageViewModel(QuickDevicePage quickDevicePage)
        {
            this.quickDevicePage = quickDevicePage;

            // Enable collection synchronization for cross-thread access
            BindingOperations.EnableCollectionSynchronization(Resolutions, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(Frequencies, _collectionLock2);

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

            // Query radio state on initialization and hook into StateChanged events
            _ = InitializeRadioStateAsync();
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
            ManagerFactory.profileManager.LastMonitorChanged += ProfileManager_LastMonitorChanged;

            // the power state picks the profile slot a change is stored in
            ManagerFactory.powerProfileManager.OverridesApplied += PowerProfileManager_OverridesApplied;

            ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
        }

        private async Task InitializeRadioStateAsync()
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

                    wifiRadio = radios?.FirstOrDefault(r => r.Kind == RadioKind.WiFi);
                    bluetoothRadio = radios?.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);

                    UpdateRadioStates();

                    // Hook into StateChanged events for event-driven updates
                    wifiRadio?.StateChanged += WiFiRadio_StateChanged;

                    bluetoothRadio?.StateChanged += BluetoothRadio_StateChanged;
                }
                finally
                {
                    isLoadingDisplay = false;
                }
            }
            catch { }
        }

        private void UpdateRadioStates()
        {
            IsWiFiEnabled = wifiRadio?.State == RadioState.On;
            IsBluetoothEnabled = bluetoothRadio?.State == RadioState.On;
        }

        private void WiFiRadio_StateChanged(Radio sender, object args)
        {
            UIHelper.TryBeginInvoke(() =>
            {
                IsWiFiEnabled = sender.State == RadioState.On;
            });
        }

        private void BluetoothRadio_StateChanged(Radio sender, object args)
        {
            UIHelper.TryBeginInvoke(() =>
            {
                IsBluetoothEnabled = sender.State == RadioState.On;
            });
        }

        private void QueryMedia()
        {
            ManagerFactory.multimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;
            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;

            QueueDisplayRefresh();
        }

        private void MultimediaManager_Initialized()
        {
            QueryMedia();
        }

        private void MultimediaManager_PrimaryScreenChanged(DesktopScreen screen)
        {
            // raised while the display list is updated, DisplaySettingsChanged follows with the new list
            QueueDisplayRefresh();
        }

        private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution? resolution)
        {
            // raised for any display change with the primary display as argument, re-read the selected monitor
            UIHelper.TryBeginInvoke(() =>
            {
                QueueDisplayRefresh();
            });
        }

        /// <summary>
        /// Coalesces monitor list refreshes onto the UI thread.
        /// </summary>
        private void QueueDisplayRefresh()
        {
            if (Interlocked.Exchange(ref _displayRefreshQueued, 1) == 1)
                return;

            UIHelper.TryBeginInvoke(() =>
            {
                Interlocked.Exchange(ref _displayRefreshQueued, 0);
                RefreshDisplayMonitors();
            });
        }

        /// <summary>
        /// Called when the page is shown: the monitor is picked again by the default rule.
        /// </summary>
        public void OnPageShown()
        {
            _monitorPickedByUser = false;
            QueueDisplayRefresh();
        }

        /// <summary>
        /// Rebuilds the monitor picker and re-reads the selected monitor modes.
        /// Default selection: the monitor the game of the applied game profile runs on, otherwise the main monitor.
        /// A monitor picked by the user is kept while it stays connected.
        /// </summary>
        private void RefreshDisplayMonitors()
        {
            List<DesktopScreen> monitors = ManagerFactory.multimediaManager.GetConnectedMonitors();
            Profile? gameProfile = GetActiveGameProfile(out int slot);

            Guid primaryId = (monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault())?.KnownMonitorId ?? Guid.Empty;
            Guid gameMonitorId = GetGameMonitorId(gameProfile, monitors);

            if (!_monitorPickedByUser || !monitors.Any(m => m.KnownMonitorId == _selectedMonitorId))
            {
                _monitorPickedByUser = false;
                _selectedMonitorId = gameMonitorId != Guid.Empty ? gameMonitorId : primaryId;
            }

            // same badges as the profile Display picker, the source badge only while a game profile is applied
            FramerateSourceBadge sourceBadge = FramerateSourceBadge.None;
            Guid sourceId = primaryId;
            if (gameProfile is not null)
            {
                if (gameMonitorId == Guid.Empty)
                    sourceBadge = FramerateSourceBadge.Default;
                else
                {
                    sourceId = gameMonitorId;
                    sourceBadge = DisplayModeHelper.IsGameRunning(gameProfile) ? FramerateSourceBadge.RunningHere : FramerateSourceBadge.LastUsed;
                }
            }

            List<DisplayMonitorItem> items = monitors
                .Select((m, index) => new DisplayMonitorItem(m.KnownMonitorId, GetMonitorLabel(m, index + 1), m.IsPrimary,
                    m.KnownMonitorId == sourceId ? sourceBadge : FramerateSourceBadge.None) { IsInternal = m.IsInternal })
                .ToList();

            isLoadingMonitors = true;
            try
            {
                DisplayMonitorItems = items;
                OnPropertyChanged(nameof(SelectedDisplayMonitor));
            }
            finally
            {
                isLoadingMonitors = false;
            }

            UpdateDisplaySaveNote(gameProfile, slot);
            LoadSelectedMonitorModes(gameProfile, slot);
        }

        private static string GetMonitorLabel(DesktopScreen screen, int position)
        {
            KnownMonitor? known = ManagerFactory.multimediaManager.GetKnownMonitor(screen.KnownMonitorId);
            // QuickTools is narrow: name only, without the "Display N: " prefix; internal and main are badges
            return MonitorIdentity.GetDisplayName(screen, known, position);
        }

        // connected monitor the game window was last active on, sub profiles fall back to their parent
        private static Guid GetGameMonitorId(Profile? gameProfile, List<DesktopScreen> monitors)
        {
            if (gameProfile is null)
                return Guid.Empty;

            Guid monitorId = DisplayModeHelper.GetLastMonitorId(gameProfile);
            return monitors.Any(m => m.KnownMonitorId == monitorId) ? monitorId : Guid.Empty;
        }


        /// <summary>
        /// The applied profile when it is a game profile (sub profiles included), with the power slot the apply path uses.
        /// </summary>
        private static Profile? GetActiveGameProfile(out int slot)
        {
            slot = 0;

            if (!ManagerFactory.profileManager.IsReady)
                return null;

            Profile? profile = ManagerFactory.profileManager.GetCurrent();
            if (profile is null || profile.Default)
                return null;

            if (!ManagerFactory.powerProfileManager.GetCurrentGameProfile(out _, out int powerLineStatus))
                powerLineStatus = (int)System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus;

            // profile slots only exist for battery and plugged in
            if (powerLineStatus != (int)System.Windows.Forms.PowerLineStatus.Offline && powerLineStatus != (int)System.Windows.Forms.PowerLineStatus.Online)
                return null;

            slot = powerLineStatus;
            return profile;
        }

        private DesktopScreen? GetSelectedScreen()
        {
            List<DesktopScreen> monitors = ManagerFactory.multimediaManager.GetConnectedMonitors();
            return monitors.FirstOrDefault(m => m.KnownMonitorId == _selectedMonitorId)
                ?? monitors.FirstOrDefault(m => m.IsPrimary)
                ?? monitors.FirstOrDefault()
                ?? ManagerFactory.multimediaManager.PrimaryDesktop;
        }

        private bool IsSelectedMonitorPrimary()
        {
            return GetSelectedScreen()?.IsPrimary ?? true;
        }

        private void UpdateDisplaySaveNote(Profile? gameProfile, int slot)
        {
            if (gameProfile is null)
            {
                DisplaySaveNote = string.Empty;
                return;
            }

            string name = gameProfile.Name;
            if (gameProfile.IsSubProfile)
            {
                Profile? parent = ManagerFactory.profileManager.GetParent(gameProfile);
                if (parent is not null)
                    name = $"{parent.Name} / {gameProfile.Name}";
            }

            string powerState = slot == (int)System.Windows.Forms.PowerLineStatus.Online
                ? Properties.Resources.PowerState_PluggedIn
                : Properties.Resources.PowerState_OnBattery;
            DisplaySaveNote = string.Format(Properties.Resources.QuickDevicePage_DisplaySaveNote, name, powerState);
        }

        private static ScreenResolution? FindResolution(DesktopScreen screen, int width, int height)
        {
            return DisplayModeHelper.FindResolution(screen.screenResolutions, width, height, r => r.Width, r => r.Height);
        }

        // same rule as the apply path: closest available, higher on a tie
        private static int SnapFrequency(ScreenResolution resolution, int frequency)
        {
            return DisplayModeHelper.SnapRefreshRate(resolution.Frequencies.Keys, frequency);
        }

        /// <summary>
        /// Loads the resolutions and refresh rates of the selected monitor without applying anything.
        /// While a game profile is applied, the selection shows the mode the profile resolves to for this monitor and slot.
        /// </summary>
        private void LoadSelectedMonitorModes(Profile? gameProfile, int slot)
        {
            DesktopScreen? screen = GetSelectedScreen();
            if (screen is null)
                return;

            UpdateDisplayOverrideUI();

            isLoadingDisplay = true;
            try
            {
                List<ScreenResolution> resolutions = screen.screenResolutions.ToList();
                bool sameList = _listedMonitorId == screen.KnownMonitorId
                    && Resolutions.Count == resolutions.Count
                    && Resolutions.Zip(resolutions).All(pair => pair.First.Equals(pair.Second));

                if (!sameList)
                {
                    if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                        return;

                    try
                    {
                        Resolutions.Clear();
                        foreach (ScreenResolution resolution in resolutions)
                            Resolutions.Add(resolution);
                    }
                    finally
                    {
                        Monitor.Exit(_collectionLock);
                    }

                    _listedMonitorId = screen.KnownMonitorId;
                }

                // integer scaling drives the primary display resolution, leave the selection alone
                if (IsIntegerScalingActive() && screen.IsPrimary)
                    return;

                ScreenResolution? currentResolution = screen.GetResolution();
                ScreenResolution? selected = currentResolution;
                int frequency = screen.GetCurrentFrequency();

                if (gameProfile is not null && screen.KnownMonitorId != Guid.Empty)
                {
                    ResolvedDisplayMode mode = ManagerFactory.powerProfileManager.ResolveDisplayMode(gameProfile, slot, screen.KnownMonitorId);
                    if (mode.HasResolution)
                        selected = FindResolution(screen, mode.Width, mode.Height) ?? selected;
                    if (mode.HasRefreshRate)
                        frequency = mode.RefreshRate;
                }

                if (selected is null)
                    return;

                // a refresh rate that isn't the current one must exist for the shown resolution
                if (!ReferenceEquals(selected, currentResolution) || frequency != screen.GetCurrentFrequency())
                    frequency = SnapFrequency(selected, frequency);

                SelectedResolution = Resolutions.FirstOrDefault(r => r.Equals(selected)) ?? selected;
                UpdateFrequenciesForResolution(SelectedResolution, frequency);
            }
            finally
            {
                isLoadingDisplay = false;
            }
        }

        private void UpdateFrequenciesForResolution(ScreenResolution resolution, int? currentFrequency = null)
        {
            // Store current selection before clearing
            int? currentSelectedFrequency = currentFrequency ?? SelectedFrequency?.Frequency;
            IEnumerable<int> frequencies = resolution.Frequencies.Keys;

            if (currentFrequency.HasValue && currentFrequency.Value > 1)
                frequencies = frequencies.Append(currentFrequency.Value);

            if (!Monitor.TryEnter(_collectionLock2, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                Frequencies.Clear();
                foreach (int frequency in frequencies.Distinct().OrderByDescending(f => f))
                {
                    Frequencies.Add(new ScreenFrequencyViewModel(frequency));
                }

                // Always select a frequency if available
                if (Frequencies.Any())
                {
                    ScreenFrequencyViewModel? selectedFreq = null;

                    // First, try to select matching frequency if we have a current one
                    if (currentSelectedFrequency.HasValue)
                    {
                        selectedFreq = Frequencies.FirstOrDefault(f => f.Frequency == currentSelectedFrequency.Value);
                    }

                    // If no matching frequency found, select the first (highest) one
                    if (selectedFreq == null)
                    {
                        selectedFreq = Frequencies.FirstOrDefault();
                    }

                    if (selectedFreq != null)
                        SelectedFrequency = selectedFreq;
                }
            }
            finally
            {
                Monitor.Exit(_collectionLock2);
            }
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            // Update UI
            UpdateDisplayOverrideUI();

            // default monitor, save note and shown mode depend on the applied profile
            QueueDisplayRefresh();
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // the default profile is an inherit layer of the shown game profile mode
            if (profile.Default && !isCurrent)
            {
                QueueDisplayRefresh();
                return;
            }

            // Only update UI if this is the currently applied profile
            if (!isCurrent)
                return;

            // Update UI based on the updated profile's integer scaling state
            UpdateDisplayOverrideUI();
            QueueDisplayRefresh();
        }

        private void ProfileManager_LastMonitorChanged(Profile profile)
        {
            // the game window moved: badges, and the default monitor unless the user picked one
            QueueDisplayRefresh();
        }

        private void PowerProfileManager_OverridesApplied(Profile gameProfile, int powerLineStatus, UpdateSource source)
        {
            // power state changes switch the profile slot
            QueueDisplayRefresh();
        }

        // integer scaling of the applied profile and power state drives the primary display resolution
        private static bool IsIntegerScalingActive()
        {
            return ManagerFactory.powerProfileManager.IsReady
                && ManagerFactory.powerProfileManager.GetGraphics().IntegerScaling.IsEnabled;
        }

        private void UpdateDisplayOverrideUI()
        {
            isLoadingDisplay = true;
            try
            {
                // integer scaling only drives the primary display
                var canChangeDisplay = !(IsIntegerScalingActive() && IsSelectedMonitorPrimary());
                IsDisplayStackEnabled = canChangeDisplay;
                ResolutionOverrideStackVisibility = canChangeDisplay ? Visibility.Collapsed : Visibility.Visible;
            }
            finally
            {
                isLoadingDisplay = false;
            }
        }

        private void NightLight_Toggled(bool enabled)
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
        }

        private void SettingsManager_SettingValueChanged(string? name, object? value, bool temporary, bool initializing)
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

        /// <summary>
        /// Applies a resolution or refresh rate picked by the user on the selected monitor.
        /// With a game profile applied, the changed field is stored as an override of that profile for the current
        /// power slot and monitor, and the profile apply path changes the mode. Otherwise the change is temporary.
        /// </summary>
        private void ApplyDisplayChange(bool resolutionChanged)
        {
            ScreenResolution? resolution = SelectedResolution;
            ScreenFrequencyViewModel? frequency = SelectedFrequency;
            DesktopScreen? screen = GetSelectedScreen();
            if (resolution is null || frequency is null || screen is null)
                return;

            Guid monitorId = screen.KnownMonitorId;
            int width = resolution.Width;
            int height = resolution.Height;
            int refreshRate = frequency.Frequency;

            try
            {
                Profile? gameProfile = GetActiveGameProfile(out int slot);
                if (gameProfile is not null && monitorId != Guid.Empty)
                {
                    // integer scaling drives the primary display resolution
                    if (resolutionChanged && screen.IsPrimary && ManagerFactory.powerProfileManager.ResolveGraphics(gameProfile, slot).IntegerScaling.IsEnabled)
                        return;

                    bool changed = resolutionChanged
                        ? WriteDisplayModeOverride(gameProfile, slot, monitorId, width, height, null)
                        : WriteDisplayModeOverride(gameProfile, slot, monitorId, 0, 0, refreshRate);

                    if (changed)
                        QueueProfileSave(gameProfile);
                    return;
                }

                // Don't apply if already at this mode
                if (screen.devMode.dmPelsWidth == width &&
                    screen.devMode.dmPelsHeight == height &&
                    screen.devMode.dmDisplayFrequency == refreshRate)
                    return;

                if (monitorId == Guid.Empty)
                {
                    // monitor without a known id, only the primary display can be changed
                    if (screen.IsPrimary)
                        ManagerFactory.multimediaManager.SetResolution(width, height, refreshRate, resolution.BitsPerPel);
                    return;
                }

                // the per-monitor path takes the display lock, keep it off the UI thread
                Task.Run(() => ManagerFactory.multimediaManager.SetMonitorDisplayMode(monitorId, width, height, refreshRate));
            }
            catch { }
        }

        /// <summary>
        /// Stores a resolution (width and height) or a refresh rate override in a profile slot. Returns true when it changed.
        /// </summary>
        private static bool WriteDisplayModeOverride(Profile profile, int slot, Guid monitorId, int width, int height, int? refreshRate)
        {
            lock (profile.SyncRoot)
            {
                profile.PerformanceOverrides ??= new();
                if (!profile.PerformanceOverrides.TryGetValue(slot, out PerformanceOverrides? overrides) || overrides is null)
                {
                    overrides = new();
                    profile.PerformanceOverrides[slot] = overrides;
                }

                overrides.DisplayModes ??= new();
                if (!overrides.DisplayModes.TryGetValue(monitorId, out DisplayModeOverride? displayMode) || displayMode is null)
                {
                    displayMode = new();
                    overrides.DisplayModes[monitorId] = displayMode;
                }

                if (refreshRate is int frequency)
                {
                    if (frequency <= 0 || (displayMode.RefreshMode == DisplayOverrideMode.Override && displayMode.RefreshRate == frequency))
                        return false;

                    displayMode.RefreshMode = DisplayOverrideMode.Override;
                    displayMode.RefreshRate = frequency;
                    return true;
                }

                if (width <= 0 || height <= 0
                    || (displayMode.ResolutionMode == DisplayOverrideMode.Override && displayMode.Width == width && displayMode.Height == height))
                    return false;

                displayMode.ResolutionMode = DisplayOverrideMode.Override;
                displayMode.Width = width;
                displayMode.Height = height;
                return true;
            }
        }

        /// <summary>
        /// Saves the profile through the normal update path, which applies it when it is current.
        /// </summary>
        private void QueueProfileSave(Profile profile)
        {
            Task.Run(() =>
            {
                lock (_profileSaveLock)
                {
                    try
                    {
                        ManagerFactory.profileManager.UpdateOrCreateProfile(profile, UpdateSource.Background);
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError("Failed to save display mode of profile {0}: {1}", profile.Name, ex.Message);
                    }
                }
            });
        }

        public void SetResolution(ScreenResolution? resolution)
        {
            DesktopScreen? desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;
            if (desktopScreen == null || resolution == null)
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
                ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
                ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
                ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
                ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
                ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
                ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
                ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
                ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
                ManagerFactory.profileManager.LastMonitorChanged -= ProfileManager_LastMonitorChanged;
                ManagerFactory.powerProfileManager.OverridesApplied -= PowerProfileManager_OverridesApplied;
                NightLight.Toggled -= NightLight_Toggled;

                // Unhook from Radio StateChanged events
                wifiRadio?.StateChanged -= WiFiRadio_StateChanged;
                wifiRadio = null;
                bluetoothRadio?.StateChanged -= BluetoothRadio_StateChanged;
                bluetoothRadio = null;

                radios = null;
                quickDevicePage = null;
            }

            base.Dispose(disposing);
        }
    }
}
