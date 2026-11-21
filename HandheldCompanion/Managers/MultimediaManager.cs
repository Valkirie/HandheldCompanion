using HandheldCompanion.Helpers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
using DisplayDevice = HandheldCompanion.Misc.DisplayDevice;
using ScreenOrientation = HandheldCompanion.Managers.Desktop.ScreenOrientation;

namespace HandheldCompanion.Managers;

public enum DockedDisplayBehavior
{
    DoNothing = 0,
    SwitchToExternal = 1,
    SwitchToExternalWhenCharging = 2,
}

public class MultimediaManager : IManager
{
    // Screen management
    public ConcurrentDictionary<string, DesktopScreen> AllScreens = [];
    public DesktopScreen? PrimaryDesktop;
    private readonly object _displayLock = new();

    // Known monitors, kept even when not connected
    private readonly List<KnownMonitor> _knownMonitors = [];
    private readonly object _knownMonitorsLock = new();
    private static string KnownMonitorsPath => Path.Combine(App.SettingsPath, "monitors.json");

    // System display modes: the mode each monitor had before HC applied a profile override,
    // also saved to settings so the next start can put it back after a crash
    private const string LastSystemDisplayModesSettingsName = "LastSystemDisplayModes";
    private readonly ConcurrentDictionary<Guid, SystemDisplayMode> _systemDisplayModes = new();
    private readonly object _profileDisplayLock = new();

    // Last display mode HC enforced on each monitor: an apply that resolves the same one leaves the monitor alone,
    // so a mode picked since then (outside HC or from the quick settings) stays until the profile, power state or mode changes
    private readonly ConcurrentDictionary<Guid, EnforcedDisplayMode> _enforcedDisplayModes = new();

    // Audio management
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private MMDevice? _multimediaDevice;
    private readonly MMDeviceNotificationClient _notificationClient;
    private bool _volumeSupport;

    // Brightness management
    private readonly ManagementEventWatcher _brightnessWatcher;
    private readonly ManagementScope _scope;
    private readonly bool _brightnessSupport;
    private short _streamingBrightness = -1;

    public MultimediaManager()
    {
        // Setup audio endpoint
        _deviceEnumerator = new MMDeviceEnumerator();
        _notificationClient = _deviceEnumerator.CreateNotificationClient();
        _notificationClient.DefaultDeviceChanged += NotificationClient_DefaultDeviceChanged;
        SetDefaultAudioEndPoint();

        // Setup brightness monitoring
        _scope = new ManagementScope(@"\\.\root\wmi");
        _scope.Connect();

        _brightnessWatcher = new ManagementEventWatcher(_scope, new EventQuery("Select * From WmiMonitorBrightnessEvent"));

        // Check if we have control over brightness
        _brightnessSupport = GetBrightness() != -1;
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // Start brightness monitoring
        _brightnessWatcher.EventArrived += OnWMIEvent;
        _brightnessWatcher.Start();

        // Subscribe to our own screen events for docked display behavior
        ScreenConnected += MultimediaManager_ScreenConnected;
        ScreenDisconnected += MultimediaManager_ScreenDisconnected;

        // Load known monitors before the first display query
        LoadKnownMonitors();

        // Subscribe to display settings changes
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

        // Initial display query
        SystemEvents_DisplaySettingsChanged(null, new EventArgs());

        // System display modes left by a session that didn't stop, checked against the connected monitors
        LoadSystemDisplayModes();

        // Profile display modes follow the applied overrides (profile activation, power state and default profile changes)
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

        // Remember the monitor the game window is on, it drives the framerate presets of the profile
        ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        ManagerFactory.profileManager.Applied += ProfileManager_Applied;

        base.Start();
    }

    private void PowerProfileManager_Initialized()
    {
        QueryPowerProfile();
    }

    private void QueryPowerProfile()
    {
        ManagerFactory.powerProfileManager.OverridesApplied += PowerProfileManager_OverridesApplied;
        QueueProfileDisplayModes();
    }

    private void PowerProfileManager_OverridesApplied(Profile gameProfile, int powerLineStatus, UpdateSource source)
    {
        QueueProfileDisplayModes();
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // Unregister audio callbacks
        _notificationClient.Dispose();

        // Stop brightness monitoring
        _brightnessWatcher.EventArrived -= OnWMIEvent;
        _brightnessWatcher.Stop();

        // Unsubscribe from our own screen events
        ScreenConnected -= MultimediaManager_ScreenConnected;
        ScreenDisconnected -= MultimediaManager_ScreenDisconnected;

        // Unsubscribe from events
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
        ManagerFactory.powerProfileManager.OverridesApplied -= PowerProfileManager_OverridesApplied;
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
        ManagerFactory.profileManager.Applied -= ProfileManager_Applied;

        // Put back the system display modes
        RestoreProfileDisplayModes();

        base.Stop();
    }

    private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
    {
        VolumeNotification?.Invoke(data.MasterVolume * 100.0f);
    }

    private void NotificationClient_DefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        SetDefaultAudioEndPoint();
    }

    private void SetDefaultAudioEndPoint()
    {
        try
        {
            if (_multimediaDevice?.AudioEndpointVolume is not null)
            {
                _volumeSupport = false;
                _multimediaDevice.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
            }

            _multimediaDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            if (_multimediaDevice?.AudioEndpointVolume is not null)
            {
                _volumeSupport = true;
                _multimediaDevice.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
            }

            VolumeNotification?.Invoke((float)GetVolume());
        }
        catch (Exception)
        {
            LogManager.LogError("No AudioEndpoint available");
        }
    }

    private void MultimediaManager_ScreenConnected(DesktopScreen screen)
    {
        // AllScreens still contains the previous list when this event fires.
        // Only act when the new screen is itself external.
        if (screen.IsInternal)
            return;

        // If the current primary is already an external screen, the OS has already handled
        // the topology (e.g. re-enabled internal, plugged a third monitor). Don't interfere.
        if (PrimaryDesktop?.IsExternal == true)
            return;

        DockedDisplayBehavior behavior = (DockedDisplayBehavior)ManagerFactory.settingsManager.GetInt("DockedDisplayBehavior");

        switch (behavior)
        {
            case DockedDisplayBehavior.SwitchToExternal:
                SetDisplayTopology(true);
                break;

            case DockedDisplayBehavior.SwitchToExternalWhenCharging:
                if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online)
                    SetDisplayTopology(true);
                break;

            case DockedDisplayBehavior.DoNothing:
            default:
                break;
        }
    }

    private void MultimediaManager_ScreenDisconnected(DesktopScreen screen)
    {
        // When an external screen is unplugged restore internal display
        DockedDisplayBehavior behavior = (DockedDisplayBehavior)ManagerFactory.settingsManager.GetInt("DockedDisplayBehavior");
        if (behavior == DockedDisplayBehavior.DoNothing)
            return;

        if (screen.IsInternal)
            return;

        // AllScreens still contains the previous list when this event fires, so exclude
        // the screen being disconnected when checking whether any external displays remain.
        bool hasExternal = AllScreens.Values.Any(s => s.IsExternal && s.DevicePath != screen.DevicePath);
        if (!hasExternal)
            SetDisplayTopology(false);
    }

    /// <summary>
    /// Turns off the displays either through the Windows monitor power command or by changing display topology.
    /// <paramref name="disable"/> = true disables the internal display in the display topology, preventing input from waking it.
    /// </summary>
    public static void TurnOffScreen(bool disable)
    {
        if (disable)
        {
            SetDisplayTopology(true);
            return;
        }

        WinAPI.SendMessage((IntPtr)0xffff, WinAPI.WM_SYSCOMMAND, (IntPtr)0xf170, (IntPtr)2);
    }

    /// <summary>
    /// Turns the displays back on either through the Windows monitor power command or by changing display topology.
    /// <paramref name="disable"/> = true restores the internal display in the display topology.
    /// </summary>
    public static void TurnOnScreen(bool disable)
    {
        if (disable)
        {
            SetDisplayTopology(false);
            return;
        }

        WinAPI.SendMessage((IntPtr)0xffff, WinAPI.WM_SYSCOMMAND, (IntPtr)0xf170, (IntPtr)(-1));
    }

    /// <summary>
    /// Switches display topology.
    /// <paramref name="externalOnly"/> = true  → external display only (handheld screen off).
    /// <paramref name="externalOnly"/> = false → internal display only (handheld screen back on).
    /// </summary>
    public static void SetDisplayTopology(bool externalOnly)
    {
        string argument = externalOnly ? "/external" : "/internal";
        LogManager.LogInformation("Switching display topology: DisplaySwitch.exe {0}", argument);
        try
        {
            Process.Start(new ProcessStartInfo("DisplaySwitch.exe", argument)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to switch display topology: {0}", ex.Message);
        }
    }

    private void OnWMIEvent(object sender, EventArrivedEventArgs e)
    {
        int brightness = Convert.ToInt32(e.NewEvent.Properties["Brightness"].Value);
        BrightnessNotification?.Invoke(brightness);
    }

    #region Display Helper Methods

    public static string GetDisplayName(string input)
    {
        int index = input.LastIndexOf('\\');
        if (index >= 0 && index < input.Length - 1)
        {
            return input.Substring(index + 1);
        }
        return string.Empty; // Or handle this case as needed
    }

    public static string GetDisplayFriendlyName(string deviceName)
    {
        // default fallback: \\.\DISPLAYx
        string friendlyName = GetDisplayName(deviceName);

        try
        {
            // Find the Display/Path for this screen (you already use WindowsDisplayAPI)
            var display = Display.GetDisplays()
                .FirstOrDefault(d => string.Equals(d.DisplayName, deviceName, StringComparison.OrdinalIgnoreCase));

            if (display != null)
            {
                // 1) EDID monitor name (preferred)
                // Extract the instance id from DevicePath: \\?\DISPLAY#<hwid>#<instance>#...
                var m = Regex.Match(display.DevicePath,
                    @"^\\\\\?\\DISPLAY#[^#]+#(?<instance>[^#]+)(?=[#\{])",
                    RegexOptions.IgnoreCase);

                if (m.Success && TryGetEdidMonitorName(m.Groups["instance"].Value, out var edidName))
                    return edidName;

                // 2) DisplayConfig target friendly name (often "Generic PnP Monitor", but keep as fallback)
                var target = PathDisplayTarget.GetDisplayTargets()
                    .FirstOrDefault(t => string.Equals(t.DevicePath, display.DevicePath, StringComparison.OrdinalIgnoreCase));
                if (target != null && !string.IsNullOrWhiteSpace(target.FriendlyName))
                    return target.FriendlyName.Trim();

                // 3) WindowsDisplayAPI device name
                if (!string.IsNullOrWhiteSpace(display.DeviceName))
                    return display.DeviceName.Trim();
            }
        }
        catch
        {
            // ignore and use fallback
        }

        return friendlyName;
    }

    /// <summary>
    /// Reads the EDID and returns the Monitor Name (descriptor 0xFC), e.g. "AYANEOQHD".
    /// </summary>
    private static bool TryGetEdidMonitorName(string lookupInstanceKeyName, out string monitorName)
    {
        monitorName = string.Empty;

        using var displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY");
        if (displayKey == null) return false;

        foreach (var monitorKeyName in displayKey.GetSubKeyNames())
            using (var monitorKey = displayKey.OpenSubKey(monitorKeyName))
            {
                if (monitorKey == null) continue;

                foreach (var instanceKeyName in monitorKey.GetSubKeyNames())
                {
                    if (!string.Equals(instanceKeyName, lookupInstanceKeyName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var deviceParams = monitorKey.OpenSubKey(instanceKeyName + @"\Device Parameters");
                    if (deviceParams == null) continue;

                    if (deviceParams.GetValue("EDID") is not byte[] edid || edid.Length < 128)
                        continue;

                    // Four 18-byte descriptors starting at offset 54
                    const int dtdStart = 54;
                    for (int i = 0; i < 4; i++)
                    {
                        int off = dtdStart + i * 18;

                        // If pixel clock bytes are 00 00, this is a descriptor (not a detailed timing)
                        bool isDescriptor = edid[off] == 0x00 && edid[off + 1] == 0x00;
                        if (!isDescriptor) continue;

                        byte tag = edid[off + 3];
                        if (tag == 0xFC) // Monitor Name
                        {
                            // 13 bytes of ASCII text starting at off+5, terminated by 0x0A or padded with spaces
                            string raw = System.Text.Encoding.ASCII.GetString(edid, off + 5, 13);
                            string name = raw.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                monitorName = name;
                                return true;
                            }
                        }
                    }
                }
            }

        return false;
    }

    public string GetAdapterFriendlyName(string deviceName)
    {
        try
        {
            Display? display = Display.GetDisplays().FirstOrDefault(d => d.DisplayName.Equals(deviceName));
            if (display is null)
                return string.Empty;

            PathDisplayTarget? target = GetDisplayTarget(display.DevicePath);
            return target?.FriendlyName ?? display.DeviceName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetDisplayPath(string deviceName)
    {
        try
        {
            Display? display = Display.GetDisplays().FirstOrDefault(d => d.DisplayName.Equals(deviceName));
            return display?.DevicePath ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private PathDisplayTarget? GetDisplayTarget(string devicePath)
    {
        return PathDisplayTarget.GetDisplayTargets().FirstOrDefault(t => t.DevicePath.Equals(devicePath));
    }

    #endregion

    #region Event Handlers

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        lock (_displayLock)
        {
            AddStatus(ManagerStatus.Busy);

            Dictionary<string, DesktopScreen> desktopScreens = [];

            foreach (Screen screen in Screen.AllScreens)
            {
                if (string.IsNullOrEmpty(screen.DeviceName))
                    continue;

                DesktopScreen desktopScreen = new(screen);

                // Pull all available resolutions and frequencies
                if (desktopScreen.screen is null)
                    continue;

                List<DisplayDevice> resolutions = GetResolutions(desktopScreen.screen.DeviceName);
                foreach (DisplayDevice mode in resolutions)
                {
                    ScreenResolution? resolution = desktopScreen.screenResolutions
                        .FirstOrDefault(a => a.Width == mode.dmPelsWidth && a.Height == mode.dmPelsHeight);

                    if (resolution is null)
                    {
                        resolution = new ScreenResolution(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel);
                        desktopScreen.screenResolutions.Add(resolution);
                    }
                    else
                    {
                        if (mode.dmBitsPerPel > resolution.BitsPerPel)
                        {
                            resolution.BitsPerPel = mode.dmBitsPerPel;
                        }
                    }

                    if (mode.dmDisplayFrequency > 1)
                        resolution.Frequencies.TryAdd(mode.dmDisplayFrequency, mode.dmDisplayFrequency);
                }

                // Get current resolution info
                ScreenResolution currentResolution = new(screen.Bounds.Width, screen.Bounds.Height, screen.BitsPerPixel);

                // Initialize native resolution
                desktopScreen.nativeResolution = new(screen.Bounds.Width, screen.Bounds.Height, screen.BitsPerPixel);

                int nativeWidth = desktopScreen.nativeResolution.Width;
                int nativeHeight = desktopScreen.nativeResolution.Height;

                // Get native resolution from EDID
                Regex regex = new Regex(@"^\\\\\?\\DISPLAY#[^#]+#(?<instance>[^#]+)(?=[#\{])", RegexOptions.IgnoreCase);
                Match match = regex.Match(desktopScreen.DevicePath);
                if (match.Success && match.Groups.ContainsKey("instance"))
                {
                    string instanceKeyName = match.Groups["instance"].Value;
                    GetNativeResolutions(instanceKeyName, ref nativeWidth, ref nativeHeight);

                    desktopScreen.nativeResolution.Width = nativeWidth;
                    desktopScreen.nativeResolution.Height = nativeHeight;
                }

                // Handle portrait-native displays (swap width/height for landscape mode)
                if (desktopScreen.nativeResolution.Orientation == ScreenOrientation.Portrait &&
                    currentResolution.Orientation == ScreenOrientation.Landscape)
                {
                    (nativeWidth, nativeHeight) = (nativeHeight, nativeWidth);
                }

                // Sort resolutions by size (largest first)
                desktopScreen.screenResolutions = currentResolution.Orientation == ScreenOrientation.Landscape
                    ? desktopScreen.screenResolutions.OrderByDescending(r => r.Width).ThenByDescending(r => r.Height).ToList()
                    : desktopScreen.screenResolutions.OrderByDescending(r => r.Height).ThenByDescending(r => r.Width).ToList();

                // Clamp native resolution to maximum available
                ScreenResolution? maxRes = desktopScreen.screenResolutions.FirstOrDefault();
                if (maxRes is not null)
                {
                    nativeWidth = desktopScreen.nativeResolution.Width = Math.Min(maxRes.Width, nativeWidth);
                    nativeHeight = desktopScreen.nativeResolution.Height = Math.Min(maxRes.Height, nativeHeight);
                }

                // Calculate integer scaling dividers
                int divider = 1;
                while (true)
                {
                    int scaledHeight = nativeHeight / divider;
                    int scaledWidth = nativeWidth;

                    ScreenResolution? dividedRes = desktopScreen.screenResolutions
                        .FirstOrDefault(res => res.Height == scaledHeight && res.Width <= scaledWidth);

                    if (dividedRes is null)
                        break;

                    desktopScreen.screenDividers.Add(new(divider, dividedRes));
                    divider++;
                }

                desktopScreens.Add(desktopScreen.DevicePath, desktopScreen);
            }

            // Match connected screens to known monitors
            AssignKnownMonitors(desktopScreens.Values.ToList());

            // Update primary screen
            DesktopScreen? newPrimary = desktopScreens.Values.FirstOrDefault(a => a.IsPrimary);
            if (newPrimary is not null)
            {
                bool isNewPrimary = PrimaryDesktop?.DevicePath != newPrimary.DevicePath;

                PrimaryDesktop?.Dispose();
                PrimaryDesktop = newPrimary;

                if (isNewPrimary)
                {
                    LogManager.LogInformation("Primary screen set to {0}", newPrimary.ToString());
                    PrimaryScreenChanged?.Invoke(newPrimary);
                }
            }

            // Raise events for newly connected screens
            bool screenConnected = false;
            foreach (DesktopScreen desktop in desktopScreens.Values.Where(a => !AllScreens.ContainsKey(a.DevicePath)))
            {
                screenConnected = true;
                LogManager.LogInformation("Screen {0} connected", desktop.ToString());
                ScreenConnected?.Invoke(desktop);
            }

            // Raise events for disconnected screens
            bool screenDisconnected = false;
            foreach (DesktopScreen desktop in AllScreens.Values.Where(a => !desktopScreens.ContainsKey(a.DevicePath)))
            {
                screenDisconnected = true;
                LogManager.LogInformation("Screen {0} disconnected", desktop.ToString());
                ScreenDisconnected?.Invoke(desktop);
            }

            // Windows puts every monitor back in its saved mode when one is connected or disconnected,
            // so the profile display modes are enforced again on all of them
            bool topologyChanged = screenConnected || screenDisconnected;
            if (topologyChanged)
                _enforcedDisplayModes.Clear();

            // Update AllScreens collection
            foreach (var screen in AllScreens.Values)
                screen.Dispose();
            AllScreens.Clear();

            foreach (DesktopScreen desktop in desktopScreens.Values)
                AllScreens.TryAdd(desktop.DevicePath, desktop);

            // Notify subscribers of display settings change
            ScreenResolution? screenResolution = PrimaryDesktop?.GetResolution();
            if (screenResolution is not null && PrimaryDesktop is not null)
                DisplaySettingsChanged?.Invoke(PrimaryDesktop, screenResolution);

            // A mode changed outside HC (e.g. Windows Settings) becomes the system display mode,
            // the saved mode Windows sets on a connect or disconnect doesn't
            if (IsReady && !topologyChanged)
                AdoptChangedSystemDisplayModes();

            // Connected and remaining monitors get the current profile display mode
            if (topologyChanged && IsReady)
                QueueProfileDisplayModes();

            // The game window may sit on another monitor after a topology change
            if (IsReady)
                QueueGameMonitorUpdate();

            RemoveStatus(ManagerStatus.Busy);
        }
    }

    /// <summary>
    /// Finds all EDID entries in the registry that match the provided lookupKeyName,
    /// and returns (via ref parameters) the maximum horizontal (width) and vertical (height)
    /// native resolutions found.
    /// </summary>
    /// <param name="lookupKeyName">
    /// The instance key name to match (for example, "5&11494a2a&0&UID265").
    /// </param>
    /// <param name="maxWidth">Returns the maximum width found (in pixels).</param>
    /// <param name="maxHeight">Returns the maximum height found (in pixels).</param>
    public static void GetNativeResolutions(string lookupKeyName, ref int maxWidth, ref int maxHeight)
    {
        // Initialize the max values.
        maxWidth = 0;
        maxHeight = 0;

        // Open the DISPLAY key in the registry.
        using (RegistryKey? displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY"))
        {
            if (displayKey == null)
                return;

            // Each subkey here represents a monitor model.
            foreach (string monitorKeyName in displayKey.GetSubKeyNames())
            {
                using (RegistryKey? monitorKey = displayKey.OpenSubKey(monitorKeyName))
                {
                    if (monitorKey == null)
                        continue;

                    // Under each monitor model there are one or more instances.
                    foreach (string instanceKeyName in monitorKey.GetSubKeyNames())
                    {
                        // Only process keys that match our lookupKeyName.
                        if (!string.Equals(instanceKeyName, lookupKeyName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Build the path to "Device Parameters".
                        string deviceParamsPath = instanceKeyName + @"\Device Parameters";
                        using (RegistryKey? deviceKey = monitorKey.OpenSubKey(deviceParamsPath))
                        {
                            if (deviceKey == null)
                                continue;

                            // Retrieve the EDID data.
                            byte[]? edidData = deviceKey.GetValue("EDID") as byte[];
                            if (edidData == null || edidData.Length < 128)
                                continue;

                            // Detailed Timing Descriptors (DTDs) start at offset 54.
                            const int dtdStart = 54;
                            // If the first DTD is empty, skip this entry.
                            if (edidData[dtdStart] == 0 && edidData[dtdStart + 1] == 0)
                                continue;

                            // According to the EDID spec:
                            //   Byte 56: Horizontal active pixels (lower 8 bits)
                            //   Byte 58: Upper 4 bits for horizontal active (bits 7–4)
                            //   Byte 59: Vertical active pixels (lower 8 bits)
                            //   Byte 61: Upper 4 bits for vertical active (bits 7–4)
                            int hActiveLow = edidData[dtdStart + 2];
                            int hActiveHigh = (edidData[dtdStart + 4] >> 4) & 0x0F;
                            int hActive = (hActiveHigh << 8) | hActiveLow;

                            int vActiveLow = edidData[dtdStart + 5];
                            int vActiveHigh = (edidData[dtdStart + 7] >> 4) & 0x0F;
                            int vActive = (vActiveHigh << 8) | vActiveLow;

                            // Update maximum width and height if necessary.
                            if (hActive > maxWidth)
                                maxWidth = hActive;
                            if (vActive > maxHeight)
                                maxHeight = vActive;
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Known Monitors

    private void LoadKnownMonitors()
    {
        lock (_knownMonitorsLock)
        {
            _knownMonitors.Clear();

            try
            {
                if (!File.Exists(KnownMonitorsPath))
                    return;

                string json = File.ReadAllText(KnownMonitorsPath);
                List<KnownMonitor>? monitors = JsonConvert.DeserializeObject<List<KnownMonitor>>(json);
                if (monitors is not null)
                    _knownMonitors.AddRange(monitors.Where(m => m is not null && m.Id != Guid.Empty));

                LogManager.LogInformation("Loaded {0} known monitor(s)", _knownMonitors.Count);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load known monitors: {0}", ex.Message);
            }
        }
    }

    private void SaveKnownMonitors()
    {
        lock (_knownMonitorsLock)
        {
            try
            {
                string json = JsonConvert.SerializeObject(_knownMonitors, Formatting.Indented);
                File.WriteAllText(KnownMonitorsPath, json);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save known monitors: {0}", ex.Message);
            }
        }
    }

    private void AssignKnownMonitors(IReadOnlyList<DesktopScreen> screens)
    {
        lock (_knownMonitorsLock)
        {
            try
            {
                MonitorIdentity.Assign(screens, _knownMonitors);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to match known monitors: {0}", ex.Message);
            }

            SaveKnownMonitors();
        }

        KnownMonitorsChanged?.Invoke();
    }

    /// <summary>
    /// Removes a monitor that isn't connected from the known monitors. Connected monitors can't be forgotten.
    /// Profile references are removed separately, see ProfileManager.RemoveMonitorReferences.
    /// </summary>
    public bool ForgetKnownMonitor(Guid monitorId)
    {
        bool removed;
        lock (_displayLock)
        lock (_knownMonitorsLock)
        {
            if (AllScreens.Values.Any(s => s.KnownMonitorId == monitorId))
            {
                LogManager.LogWarning("Monitor {0} is connected and can't be forgotten", monitorId);
                return false;
            }

            removed = _knownMonitors.RemoveAll(m => m.Id == monitorId) > 0;
            if (removed)
                SaveKnownMonitors();
        }

        if (removed)
        {
            LogManager.LogInformation("Monitor {0} removed from known monitors", monitorId);
            KnownMonitorsChanged?.Invoke();
        }

        return removed;
    }

    public List<KnownMonitor> GetKnownMonitors()
    {
        lock (_knownMonitorsLock)
            return _knownMonitors.ToList();
    }

    public KnownMonitor? GetKnownMonitor(Guid id)
    {
        lock (_knownMonitorsLock)
            return _knownMonitors.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>
    /// Connected screens with a known monitor id, primary first.
    /// Doesn't take the display lock: UI callers must not wait on a display change that is invoking the UI thread.
    /// </summary>
    public List<DesktopScreen> GetConnectedMonitors()
    {
        return AllScreens.Values
            .Where(s => s.KnownMonitorId != Guid.Empty && s.screen is not null)
            .OrderByDescending(s => s.IsPrimary)
            .ThenByDescending(s => s.IsInternal)
            .ThenBy(s => s.screen?.DeviceName)
            .ToList();
    }

    #endregion

    #region Profile Display Modes

    // the mode a monitor had before HC changed it, with the last confirmed applied mode
    // and any mode HC was in the middle of applying when it stopped
    private sealed record SystemDisplayMode(int Width, int Height, int Frequency, bool FrequencyOnly)
    {
        public int AppliedWidth { get; init; }
        public int AppliedHeight { get; init; }
        public int AppliedFrequency { get; init; }

        public int PendingWidth { get; init; }
        public int PendingHeight { get; init; }
        public int PendingFrequency { get; init; }

        // entries saved before the applied mode was recorded have none
        [JsonIgnore]
        public bool HasAppliedMode => AppliedFrequency != 0;

        [JsonIgnore]
        public bool HasPendingMode => PendingFrequency != 0;
    }

    private sealed record DisplayTarget(Guid Id, string DeviceName, string Name, bool IsPrimary, List<ScreenResolution> Resolutions, List<ScreenDivider> Dividers);

    private sealed record EnforcedDisplayMode(Guid ProfileId, int PowerLineStatus, ResolvedDisplayMode Mode);

    private static EnforcedDisplayMode ResolveEnforcedDisplayMode(DisplayTarget target, Profile? profile, int powerLineStatus)
    {
        ResolvedDisplayMode mode = ManagerFactory.powerProfileManager.ResolveDisplayMode(profile, powerLineStatus, target.Id);

        // integer scaling drives the primary display resolution through its divider, the resolution override doesn't apply
        if (target.IsPrimary)
        {
            ResolvedGraphicsGroup<IntegerScalingOverride> integerScaling = ManagerFactory.powerProfileManager.ResolveGraphics(profile, powerLineStatus).IntegerScaling;
            if (integerScaling.IsEnabled)
            {
                // an unknown divider leaves the resolution alone
                ScreenDivider? divider = target.Dividers.FirstOrDefault(d => d.divider == integerScaling.Values!.Divider);
                DisplayModeSource source = integerScaling.Source == GraphicsSource.GameProfile ? DisplayModeSource.GameProfile : DisplayModeSource.DefaultProfile;
                mode = divider is null
                    ? mode with { Width = 0, Height = 0, ResolutionSource = DisplayModeSource.None }
                    : mode with { Width = divider.resolution.Width, Height = divider.resolution.Height, ResolutionSource = source };
            }
        }

        return new(profile?.Guid ?? Guid.Empty, powerLineStatus, mode);
    }

    /// <summary>
    /// Applies the profile display modes off the caller thread: changing a mode broadcasts WM_DISPLAYCHANGE,
    /// which must not run while the power profile manager holds its lock.
    /// </summary>
    public void QueueProfileDisplayModes()
    {
        Task.Run(ApplyProfileDisplayModes);
    }

    private List<DisplayTarget> GetDisplayTargets()
    {
        lock (_displayLock)
        {
            return AllScreens.Values
                .Where(s => s.KnownMonitorId != Guid.Empty && !string.IsNullOrEmpty(s.screen?.DeviceName))
                .Select(s => new DisplayTarget(s.KnownMonitorId, s.screen!.DeviceName, s.FriendlyName, s.IsPrimary, s.screenResolutions.ToList(), s.screenDividers.ToList()))
                .ToList();
        }
    }

    /// <summary>
    /// Applies the resolved display mode of the current game profile to every connected monitor.
    /// Monitors without an override get back their system display mode.
    /// </summary>
    private void ApplyProfileDisplayModes()
    {
        lock (_profileDisplayLock)
        {
            // don't change anything once the restore on stop has started
            if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
                return;

            if (!ManagerFactory.powerProfileManager.GetCurrentGameProfile(out Profile? profile, out int powerLineStatus))
                return;

            foreach (DisplayTarget target in GetDisplayTargets())
            {
                try
                {
                    EnforcedDisplayMode enforced = ResolveEnforcedDisplayMode(target, profile, powerLineStatus);
                    if (_enforcedDisplayModes.TryGetValue(target.Id, out EnforcedDisplayMode? lastEnforced) && lastEnforced == enforced)
                        continue;

                    ResolvedDisplayMode mode = enforced.Mode;
                    bool hasResolution = mode.HasResolution;
                    bool hasRefreshRate = mode.HasRefreshRate;

                    _systemDisplayModes.TryGetValue(target.Id, out SystemDisplayMode? systemMode);

                    if (!hasResolution && !hasRefreshRate)
                    {
                        if (systemMode is not null)
                        {
                            LogManager.LogInformation("Restoring system display mode of {0}", target.Name);
                            if (!SetDisplayMode(target, systemMode.Width, systemMode.Height, systemMode.Frequency, systemMode.FrequencyOnly))
                                continue;

                            RemoveSystemDisplayMode(target.Id);
                        }

                        _enforcedDisplayModes[target.Id] = enforced;
                        continue;
                    }

                    DisplayDevice current = GetDisplay(target.DeviceName);
                    if (systemMode is null)
                    {
                        systemMode = new(current.dmPelsWidth, current.dmPelsHeight, current.dmDisplayFrequency, !hasResolution);
                        SetSystemDisplayMode(target.Id, systemMode);
                    }
                    else if (systemMode.FrequencyOnly && hasResolution)
                    {
                        // the resolution was left alone so far, remember it before changing it
                        systemMode = systemMode with { Width = current.dmPelsWidth, Height = current.dmPelsHeight, FrequencyOnly = false };
                        SetSystemDisplayMode(target.Id, systemMode);
                    }

                    bool frequencyOnly = !hasResolution && systemMode.FrequencyOnly;
                    int width = hasResolution ? mode.Width : systemMode.Width;
                    int height = hasResolution ? mode.Height : systemMode.Height;
                    if (frequencyOnly)
                    {
                        width = current.dmPelsWidth;
                        height = current.dmPelsHeight;
                    }
                    int frequency = hasRefreshRate ? mode.RefreshRate : systemMode.Frequency;

                    if (SetDisplayMode(target, width, height, frequency, frequencyOnly))
                    {
                        _enforcedDisplayModes[target.Id] = enforced;
                    }
                    else if (_systemDisplayModes.TryGetValue(target.Id, out SystemDisplayMode? recorded)
                        && !recorded.HasAppliedMode && !recorded.HasPendingMode)
                    {
                        // the baseline was saved before this apply and was never confirmed
                        RemoveSystemDisplayMode(target.Id);
                    }
                }
                catch (Exception ex)
                {
                    // the next apply tries again
                    _enforcedDisplayModes.TryRemove(target.Id, out _);
                    if (_systemDisplayModes.TryGetValue(target.Id, out SystemDisplayMode? recorded)
                        && !recorded.HasAppliedMode && !recorded.HasPendingMode)
                        RemoveSystemDisplayMode(target.Id);
                    LogManager.LogError("Failed to apply profile display mode on {0}: {1}", target.Name, ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// System display mode of a monitor (the mode it had before HC changed it for a profile), the given current mode when HC didn't change it.
    /// Doesn't take the profile display lock, so the UI can ask while a mode change is running.
    /// </summary>
    public (int Width, int Height, int Frequency) GetSystemDisplayMode(Guid monitorId, int currentWidth, int currentHeight, int currentFrequency)
    {
        if (!_systemDisplayModes.TryGetValue(monitorId, out SystemDisplayMode? systemMode))
            return (currentWidth, currentHeight, currentFrequency);

        // only the refresh rate was changed, the resolution is still the original one
        return systemMode.FrequencyOnly
            ? (currentWidth, currentHeight, systemMode.Frequency)
            : (systemMode.Width, systemMode.Height, systemMode.Frequency);
    }

    /// <summary>
    /// Puts back the system display mode of every monitor HC changed for a profile.
    /// </summary>
    private void RestoreProfileDisplayModes()
    {
        lock (_profileDisplayLock)
        {
            foreach (DisplayTarget target in GetDisplayTargets())
            {
                if (!_systemDisplayModes.TryGetValue(target.Id, out SystemDisplayMode? systemMode))
                    continue;

                try
                {
                    LogManager.LogInformation("Restoring system display mode of {0}", target.Name);
                    if (SetDisplayMode(target, systemMode.Width, systemMode.Height, systemMode.Frequency, systemMode.FrequencyOnly))
                        _systemDisplayModes.TryRemove(target.Id, out _);
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to restore system display mode on {0}: {1}", target.Name, ex.Message);
                }
            }

            _enforcedDisplayModes.Clear();
            SaveSystemDisplayModes();
        }
    }

    // saved before the mode changes, so a crash right after still leaves the system display mode on record
    private void SetSystemDisplayMode(Guid monitorId, SystemDisplayMode systemMode)
    {
        _systemDisplayModes[monitorId] = systemMode;
        SaveSystemDisplayModes();
    }

    // requested mode, saved before the change so a crash during it can still be recognized
    private void RecordPendingMode(Guid monitorId, int width, int height, int frequency)
    {
        if (_systemDisplayModes.TryGetValue(monitorId, out SystemDisplayMode? systemMode))
            SetSystemDisplayMode(monitorId, systemMode with { PendingWidth = width, PendingHeight = height, PendingFrequency = frequency });
    }

    private void ClearPendingMode(Guid monitorId)
    {
        if (_systemDisplayModes.TryGetValue(monitorId, out SystemDisplayMode? systemMode) && systemMode.HasPendingMode)
            SetSystemDisplayMode(monitorId, systemMode with { PendingWidth = 0, PendingHeight = 0, PendingFrequency = 0 });
    }

    // confirmed mode the monitor ended up in, pending cleared
    private void CommitAppliedMode(Guid monitorId, int width, int height, int frequency)
    {
        if (_systemDisplayModes.TryGetValue(monitorId, out SystemDisplayMode? systemMode))
            SetSystemDisplayMode(monitorId, systemMode with
            {
                AppliedWidth = width,
                AppliedHeight = height,
                AppliedFrequency = frequency,
                PendingWidth = 0,
                PendingHeight = 0,
                PendingFrequency = 0,
            });
    }

    private static bool IsAppliedMode(SystemDisplayMode systemMode, DisplayDevice current)
    {
        return IsSameMode(current, systemMode.AppliedWidth, systemMode.AppliedHeight, systemMode.AppliedFrequency, systemMode.FrequencyOnly);
    }

    private static bool IsPendingMode(SystemDisplayMode systemMode, DisplayDevice current)
    {
        return IsSameMode(current, systemMode.PendingWidth, systemMode.PendingHeight, systemMode.PendingFrequency, systemMode.FrequencyOnly);
    }

    // a frequency-only change leaves the resolution to others (e.g. integer scaling), only the refresh rate is compared
    private static bool IsSameMode(DisplayDevice current, int width, int height, int frequency, bool frequencyOnly)
    {
        if (frequencyOnly)
            return current.dmDisplayFrequency == frequency;

        return current.dmPelsWidth == width && current.dmPelsHeight == height && current.dmDisplayFrequency == frequency;
    }

    /// <summary>
    /// A mode changed outside HC (e.g. Windows Settings) becomes the system display mode of that monitor,
    /// so ending the override or stopping HC keeps it. Only the system display mode is updated: profiles aren't written
    /// and the current override isn't applied again until the next profile apply.
    /// Skipped while HC is changing a mode: HC records the applied mode before it releases the lock,
    /// so a notification that arrives later for its own change matches.
    /// </summary>
    private void AdoptChangedSystemDisplayModes()
    {
        if (_systemDisplayModes.IsEmpty || !System.Threading.Monitor.TryEnter(_profileDisplayLock))
            return;

        try
        {
            foreach (DisplayTarget target in GetDisplayTargets())
            {
                // an entry left by a previous session without an applied mode waits for the first apply pass
                if (!_systemDisplayModes.TryGetValue(target.Id, out SystemDisplayMode? systemMode) || !systemMode.HasAppliedMode)
                    continue;

                DisplayDevice current = GetDisplay(target.DeviceName);
                if (IsAppliedMode(systemMode, current))
                    continue;

                LogManager.LogInformation("Display mode of {0} changed outside HC ({1}x{2}@{3}Hz, HC applied {4}x{5}@{6}Hz), adopted as its system display mode",
                    target.Name, current.dmPelsWidth, current.dmPelsHeight, current.dmDisplayFrequency, systemMode.AppliedWidth, systemMode.AppliedHeight, systemMode.AppliedFrequency);

                // the current mode is both the system display mode and the last applied one, so the next change outside HC is seen too
                SetSystemDisplayMode(target.Id, systemMode with
                {
                    Width = current.dmPelsWidth,
                    Height = current.dmPelsHeight,
                    Frequency = current.dmDisplayFrequency,
                    AppliedWidth = current.dmPelsWidth,
                    AppliedHeight = current.dmPelsHeight,
                    AppliedFrequency = current.dmDisplayFrequency,
                });
            }
        }
        finally
        {
            System.Threading.Monitor.Exit(_profileDisplayLock);
        }
    }

    private void RemoveSystemDisplayMode(Guid monitorId)
    {
        if (_systemDisplayModes.TryRemove(monitorId, out _))
            SaveSystemDisplayModes();
    }

    private void SaveSystemDisplayModes()
    {
        string json = _systemDisplayModes.IsEmpty ? string.Empty : JsonConvert.SerializeObject(_systemDisplayModes);
        ManagerFactory.settingsManager.SetProperty(LastSystemDisplayModesSettingsName, json);
    }

    /// <summary>
    /// Takes back the system display modes saved by a session that didn't stop:
    /// - monitor in the last confirmed applied mode, or in a pending mode, or no applied mode recorded: kept,
    ///   the first profile display mode pass puts the system display mode back and clears it, unless an override applies
    /// - a pending mode that is now showing is promoted to the confirmed applied mode
    /// - monitor already in its system display mode: cleared
    /// - any other mode: changed while HC wasn't running, the current mode is the system display mode, so the entry is cleared
    ///   and the monitor is left alone
    /// </summary>
    private void LoadSystemDisplayModes()
    {
        string json = ManagerFactory.settingsManager.GetString(LastSystemDisplayModesSettingsName);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            Dictionary<Guid, SystemDisplayMode> systemModes = JsonConvert.DeserializeObject<Dictionary<Guid, SystemDisplayMode>>(json) ?? [];
            List<DisplayTarget> targets = GetDisplayTargets();

            foreach (KeyValuePair<Guid, SystemDisplayMode> pair in systemModes)
            {
                SystemDisplayMode systemMode = pair.Value;
                DisplayTarget? target = targets.FirstOrDefault(t => t.Id == pair.Key);
                if (target is null)
                {
                    LogManager.LogInformation("Dropped last system display mode of monitor {0}, it isn't connected", pair.Key);
                    continue;
                }

                DisplayDevice current = GetDisplay(target.DeviceName);
                bool matchesApplied = systemMode.HasAppliedMode && IsAppliedMode(systemMode, current);
                bool matchesPending = systemMode.HasPendingMode && IsPendingMode(systemMode, current);
                if (matchesApplied || matchesPending)
                {
                    if (matchesPending)
                    {
                        systemMode = systemMode with
                        {
                            AppliedWidth = current.dmPelsWidth,
                            AppliedHeight = current.dmPelsHeight,
                            AppliedFrequency = current.dmDisplayFrequency,
                            PendingWidth = 0,
                            PendingHeight = 0,
                            PendingFrequency = 0,
                        };
                    }
                    else if (systemMode.HasPendingMode)
                    {
                        systemMode = systemMode with { PendingWidth = 0, PendingHeight = 0, PendingFrequency = 0 };
                    }

                    _systemDisplayModes[pair.Key] = systemMode;
                    LogManager.LogInformation("Kept last system display mode of {0} ({1}x{2}@{3}Hz), HC stopped before putting it back",
                        target.Name, systemMode.Width, systemMode.Height, systemMode.Frequency);
                    continue;
                }

                if (systemMode.HasAppliedMode || systemMode.HasPendingMode)
                {
                    if (IsSameMode(current, systemMode.Width, systemMode.Height, systemMode.Frequency, systemMode.FrequencyOnly))
                        LogManager.LogInformation("Cleared last system display mode of {0}, the monitor is already in it", target.Name);
                    else
                        LogManager.LogInformation("Display mode of {0} changed while HC wasn't running ({1}x{2}@{3}Hz, HC applied {4}x{5}@{6}Hz), adopted as its system display mode",
                            target.Name, current.dmPelsWidth, current.dmPelsHeight, current.dmDisplayFrequency, systemMode.AppliedWidth, systemMode.AppliedHeight, systemMode.AppliedFrequency);
                    continue;
                }

                _systemDisplayModes[pair.Key] = systemMode;
                LogManager.LogInformation("Kept last system display mode of {0} ({1}x{2}@{3}Hz), HC stopped before putting it back",
                    target.Name, systemMode.Width, systemMode.Height, systemMode.Frequency);
            }
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to load last system display modes: {0}", ex.Message);
        }

        // only the entries taken back stay saved
        SaveSystemDisplayModes();
    }

    /// <summary>
    /// Changes the mode of one display (dynamic, not saved to the registry).
    /// The resolution must be listed for the display; the refresh rate snaps to the closest available one.
    /// </summary>
    private bool SetDisplayMode(DisplayTarget target, int width, int height, int frequency, bool frequencyOnly)
    {
        ScreenResolution? resolution = DisplayModeHelper.FindResolution(target.Resolutions, width, height, r => r.Width, r => r.Height);

        if (resolution is null || resolution.Frequencies.Count == 0)
        {
            LogManager.LogWarning("Display mode {0}x{1} is not available on {2}", width, height, target.Name);
            return false;
        }

        frequency = DisplayModeHelper.SnapRefreshRate(resolution.Frequencies.Keys, frequency);

        // keep the current orientation
        DisplayDevice current = GetDisplay(target.DeviceName);
        width = resolution.Width;
        height = resolution.Height;
        if ((current.dmPelsWidth > current.dmPelsHeight) != (width > height))
            (width, height) = (height, width);

        if (current.dmPelsWidth == width && current.dmPelsHeight == height && current.dmDisplayFrequency == frequency)
            return true;

        // pending is saved before the change; the confirmed mode is updated only after it succeeds
        RecordPendingMode(target.Id, width, height, frequency);
        bool applied = false;
        try
        {
            DisplayDevice dm = new DisplayDevice
            {
                dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice)),
                dmDeviceName = target.DeviceName,
                dmPelsWidth = width,
                dmPelsHeight = height,
                dmDisplayFrequency = frequency,
                dmFields = frequencyOnly
                    ? DisplayDevice.DM_DISPLAYFREQUENCY
                    : DisplayDevice.DM_PELSWIDTH | DisplayDevice.DM_PELSHEIGHT | DisplayDevice.DM_DISPLAYFREQUENCY
            };

            int testResult = ChangeDisplaySettingsEx(target.DeviceName, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
            if (testResult != DISP_CHANGE_SUCCESSFUL)
            {
                LogManager.LogWarning("Display mode {0}x{1}@{2}Hz rejected on {3}: {4}", width, height, frequency, target.Name, testResult);
                return false;
            }

            int applyResult = ChangeDisplaySettingsEx(target.DeviceName, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
            if (applyResult != DISP_CHANGE_SUCCESSFUL)
            {
                LogManager.LogWarning("Failed to set display mode {0}x{1}@{2}Hz on {3}: {4}", width, height, frequency, target.Name, applyResult);
                return false;
            }

            // Windows has already switched; keep pending if the confirmed write fails so startup can still match it
            applied = true;
            DisplayDevice committed = GetDisplay(target.DeviceName);
            CommitAppliedMode(target.Id, committed.dmPelsWidth, committed.dmPelsHeight, committed.dmDisplayFrequency);
            LogManager.LogInformation("Display mode of {0} set to {1}x{2}@{3}Hz", target.Name, width, height, frequency);
            return true;
        }
        finally
        {
            if (!applied)
                ClearPendingMode(target.Id);
        }
    }

    /// <summary>
    /// Changes the mode of one connected known monitor (dynamic, not saved to the registry or to a profile).
    /// Takes the display lock: don't call it from the UI thread.
    /// </summary>
    public bool SetMonitorDisplayMode(Guid monitorId, int width, int height, int frequency)
    {
        if (!IsReady || monitorId == Guid.Empty)
            return false;

        DisplayTarget? target = GetDisplayTargets().FirstOrDefault(t => t.Id == monitorId);
        if (target is null)
            return false;

        lock (_profileDisplayLock)
        {
            try
            {
                bool result = SetDisplayMode(target, width, height, frequency, false);

                // the current profile display mode counts as enforced, so the next apply of the same profile doesn't undo this one
                if (result && ManagerFactory.powerProfileManager.GetCurrentGameProfile(out Profile? profile, out int powerLineStatus))
                    _enforcedDisplayModes[target.Id] = ResolveEnforcedDisplayMode(target, profile, powerLineStatus);

                return result;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to set display mode on {0}: {1}", target.Name, ex.Message);
                return false;
            }
        }
    }

    #endregion

    #region Game Window Monitor

    private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessEx.ProcessFilter filter)
    {
        if (filter == ProcessEx.ProcessFilter.HandheldCompanion)
            return;

        QueueGameMonitorUpdate();
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        QueueGameMonitorUpdate();
    }

    private void QueueGameMonitorUpdate()
    {
        Task.Run(UpdateGameMonitor);
    }

    /// <summary>
    /// Stores, on the current game profile, the known monitor its foreground window is on.
    /// The window is the foreground window when it belongs to the game process, otherwise the process main window.
    /// </summary>
    private void UpdateGameMonitor()
    {
        try
        {
            if (!ManagerFactory.profileManager.IsReady)
                return;

            Profile profile = ManagerFactory.profileManager.GetCurrent();
            if (profile is null || profile.Default)
                return;

            ProcessEx? processEx = ProcessManager.GetCurrent();
            if (processEx is null || processEx.ProcessId == 0 || !IsProfileProcess(profile, processEx))
                return;

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero || GetWindowThreadProcessId(hWnd, out uint processId) == 0 || processId != processEx.ProcessId)
                hWnd = processEx.Process?.MainWindowHandle ?? IntPtr.Zero;

            // a minimized window has no meaningful monitor
            if (hWnd == IntPtr.Zero || IsIconic(hWnd))
                return;

            // Screen.FromHandle is MonitorFromWindow (nearest monitor)
            string deviceName = Screen.FromHandle(hWnd).DeviceName;
            DesktopScreen? desktopScreen = AllScreens.Values.FirstOrDefault(s => string.Equals(s.screen?.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (desktopScreen is null || desktopScreen.KnownMonitorId == Guid.Empty)
                return;

            ManagerFactory.profileManager.SetLastMonitor(profile, desktopScreen.KnownMonitorId);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to detect the game window monitor: {0}", ex.Message);
        }
    }

    private static bool IsProfileProcess(Profile profile, ProcessEx processEx)
    {
        if (string.IsNullOrEmpty(processEx.Path))
            return false;

        if (string.Equals(profile.Path, processEx.Path, StringComparison.OrdinalIgnoreCase))
            return true;

        // sub profiles share the executable of their parent
        if (profile.IsSubProfile)
        {
            Profile parent = ManagerFactory.profileManager.GetParent(profile);
            return string.Equals(parent?.Path, processEx.Path, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    #endregion

    #region Display Resolution Management

    public bool SetResolution(int width, int height, int displayFrequency)
    {
        return SetResolution(width, height, displayFrequency, 0);
    }

    public bool SetResolution(int width, int height, int displayFrequency, int bitsPerPel)
    {
        if (Status != ManagerStatus.Initialized)
            return false;

        // Get the primary screen's device name
        string? deviceName = PrimaryDesktop?.screen?.DeviceName;
        if (string.IsNullOrEmpty(deviceName))
            return false;

        // Validate that the frequency is actually available for this resolution
        ScreenResolution? targetResolution = PrimaryDesktop?.screenResolutions
            .FirstOrDefault(r => r.Width == width && r.Height == height);

        if (targetResolution != null && !targetResolution.Frequencies.ContainsKey(displayFrequency))
        {
            // Frequency not in enumerated list - try the closest available frequency
            if (targetResolution.Frequencies.Any())
            {
                displayFrequency = targetResolution.Frequencies.Keys
                    .OrderBy(f => Math.Abs(f - displayFrequency))
                    .ThenByDescending(f => f) // Prefer higher if equal distance
                    .First();
            }
            else
            {
                // No frequencies available for this resolution
                return false;
            }
        }

        DisplayDevice dm = new DisplayDevice
        {
            dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice)),
            dmDeviceName = deviceName,
            dmPelsWidth = width,
            dmPelsHeight = height,
            dmDisplayFrequency = displayFrequency,
            dmFields = DisplayDevice.DM_PELSWIDTH | DisplayDevice.DM_PELSHEIGHT | DisplayDevice.DM_DISPLAYFREQUENCY
        };

        if (bitsPerPel > 0)
        {
            dm.dmBitsPerPel = bitsPerPel;
            dm.dmFields |= DisplayDevice.DM_BITSPERPEL;
        }

        long testResult = ChangeDisplaySettings(ref dm, CDS_TEST);
        if (testResult != 0)
            return false;

        long applyResult = ChangeDisplaySettings(ref dm, 0);
        return applyResult == 0;
    }

    public static DisplayDevice GetDisplay(string deviceName)
    {
        DisplayDevice dm = new DisplayDevice
        {
            dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice))
        };
        EnumDisplaySettings(deviceName, -1, ref dm);
        return dm;
    }

    public List<DisplayDevice> GetResolutions(string deviceName)
    {
        List<DisplayDevice> allMode = [];
        int index = 0;

        while (true)
        {
            DisplayDevice dm = new DisplayDevice
            {
                dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice))
            };

            if (!EnumDisplaySettingsEx(deviceName, index, ref dm, EDS_RAWMODE))
                break;

            allMode.Add(dm);
            index++;
        }

        return allMode;
    }

    #endregion

    #region Utilities

    public void PlayWindowsMedia(string file)
    {
        string path = Path.Combine(@"c:\Windows\Media\", file);
        if (File.Exists(path))
            new SoundPlayer(path).Play();
    }

    #endregion

    #region Audio Management

    public bool HasVolumeSupport()
    {
        return _volumeSupport;
    }

    public void SetVolume(double volume)
    {
        if (!_volumeSupport)
            return;

        volume = Math.Clamp(volume, 0.0d, 100.0d);
        if (_multimediaDevice?.AudioEndpointVolume is null)
            return;

        _multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(volume / 100.0d);
    }

    public double GetVolume()
    {
        if (!_volumeSupport)
            return 0.0d;

        if (_multimediaDevice?.AudioEndpointVolume is null)
            return 0.0d;

        return _multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0d;
    }

    public void IncreaseVolume()
    {
        int stepRoundDn = (int)Math.Ceiling(Math.Round(GetVolume() / 2.0d));
        int volume = stepRoundDn * 2 + 2;
        SetVolume(volume);
    }

    public void DecreaseVolume()
    {
        int stepRoundUp = (int)Math.Ceiling(Math.Round(GetVolume() / 2.0d));
        int volume = stepRoundUp * 2 - 2;
        SetVolume(volume);
    }

    public void Mute()
    {
        if (!_volumeSupport)
            return;

        if (_multimediaDevice?.AudioEndpointVolume is null)
            return;

        _multimediaDevice.AudioEndpointVolume.Mute = true;
    }

    public void Unmute()
    {
        if (!_volumeSupport)
            return;

        if (_multimediaDevice?.AudioEndpointVolume is null)
            return;

        _multimediaDevice.AudioEndpointVolume.Mute = false;
    }

    public void ToggleMute()
    {
        if (!_volumeSupport)
            return;

        if (_multimediaDevice?.AudioEndpointVolume is null)
            return;

        _multimediaDevice.AudioEndpointVolume.Mute = !_multimediaDevice.AudioEndpointVolume.Mute;
    }

    public bool IsMuted()
    {
        if (!_volumeSupport)
            return true;

        return _multimediaDevice?.AudioEndpointVolume?.Mute ?? true;
    }

    #endregion

    #region Brightness Management

    public bool HasBrightnessSupport()
    {
        return _brightnessSupport;
    }

    public void SetBrightness(double brightness)
    {
        if (!_brightnessSupport)
            return;

        brightness = Math.Clamp(brightness, 0.0d, 100.0d);

        try
        {
            using ManagementClass mclass = new ManagementClass("WmiMonitorBrightnessMethods")
            {
                Scope = new ManagementScope(@"\\.\root\wmi")
            };

            using ManagementObjectCollection instances = mclass.GetInstances();
            foreach (ManagementObject instance in instances)
            {
                object[] args = { 1, brightness };
                instance.InvokeMethod("WmiSetBrightness", args);
            }
        }
        catch { }
    }

    public void DimForStreaming()
    {
        if (!_brightnessSupport || _streamingBrightness >= 0)
            return;

        short brightness = GetBrightness();
        if (brightness < 0)
            return;

        _streamingBrightness = brightness;
        SetBrightness(0);
    }

    public void RestoreStreamingBrightness()
    {
        if (_streamingBrightness < 0)
            return;

        short brightness = _streamingBrightness;
        _streamingBrightness = -1;
        SetBrightness(brightness);
    }

    public void IncreaseBrightness()
    {
        int stepRoundDn = (int)Math.Floor(GetBrightness() / 2.0d);
        int brightness = stepRoundDn * 2 + 2;
        SetBrightness(brightness);
    }

    public void DecreaseBrightness()
    {
        int stepRoundUp = (int)Math.Ceiling(GetBrightness() / 2.0d);
        int brightness = stepRoundUp * 2 - 2;
        SetBrightness(brightness);
    }

    public short GetBrightness()
    {
        try
        {
            using ManagementClass mclass = new ManagementClass("WmiMonitorBrightness")
            {
                Scope = new ManagementScope(@"\\.\root\wmi")
            };

            using ManagementObjectCollection instances = mclass.GetInstances();
            foreach (ManagementObject instance in instances)
                return (byte)instance.GetPropertyValue("CurrentBrightness");
        }
        catch { }

        return -1;
    }

    #endregion

    #region P/Invoke

    public enum DMDO
    {
        DEFAULT = 0,
        D90 = 1,
        D180 = 2,
        D270 = 3
    }

    public const int CDS_UPDATEREGISTRY = 0x01;
    public const int CDS_TEST = 0x02;
    public const int DISP_CHANGE_SUCCESSFUL = 0;
    public const int DISP_CHANGE_RESTART = 1;
    public const int DISP_CHANGE_FAILED = -1;

    // EnumDisplaySettingsEx flags
    public const int EDS_RAWMODE = 0x00000002; // Get all hardware-supported modes

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int ChangeDisplaySettings([In] ref DisplayDevice lpDevMode, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, [In] ref DisplayDevice lpDevMode, IntPtr hwnd, int dwFlags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DisplayDevice lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DisplayDevice lpDevMode, int dwFlags);
    #endregion

    #region Events

    public event DisplaySettingsChangedEventHandler? DisplaySettingsChanged;
    public delegate void DisplaySettingsChangedEventHandler(DesktopScreen screen, ScreenResolution resolution);

    public event PrimaryScreenChangedEventHandler? PrimaryScreenChanged;
    public delegate void PrimaryScreenChangedEventHandler(DesktopScreen screen);

    public event KnownMonitorsChangedEventHandler? KnownMonitorsChanged;
    public delegate void KnownMonitorsChangedEventHandler();

    public event ScreenConnectedEventHandler? ScreenConnected;
    public delegate void ScreenConnectedEventHandler(DesktopScreen screen);

    public event ScreenDisconnectedEventHandler? ScreenDisconnected;
    public delegate void ScreenDisconnectedEventHandler(DesktopScreen screen);

    public event VolumeNotificationEventHandler? VolumeNotification;
    public delegate void VolumeNotificationEventHandler(float volume);

    public event BrightnessNotificationEventHandler? BrightnessNotification;
    public delegate void BrightnessNotificationEventHandler(int brightness);

    #endregion
}
