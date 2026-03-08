using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Shared;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
using DisplayDevice = HandheldCompanion.Misc.DisplayDevice;
using ScreenOrientation = HandheldCompanion.Managers.Desktop.ScreenOrientation;

namespace HandheldCompanion.Managers;

public class MultimediaManager : IManager
{
    // Screen management
    public ConcurrentDictionary<string, DesktopScreen> AllScreens = [];
    public DesktopScreen PrimaryDesktop;
    private readonly object _displayLock = new();

    // Audio management
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private MMDevice _multimediaDevice;
    private readonly MMDeviceNotificationClient _notificationClient;
    private bool _volumeSupport;

    // Brightness management
    private readonly ManagementEventWatcher _brightnessWatcher;
    private readonly ManagementScope _scope;
    private readonly bool _brightnessSupport;

    public MultimediaManager()
    {
        // Setup audio endpoint
        _notificationClient = new MMDeviceNotificationClient(this);
        _deviceEnumerator = new MMDeviceEnumerator();
        _deviceEnumerator.RegisterEndpointNotificationCallback(_notificationClient);
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

        // Subscribe to display settings changes
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

        // Subscribe to settings manager
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

        // Initial display query
        SystemEvents_DisplaySettingsChanged(null, null);

        base.Start();
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // Unregister audio callbacks
        _deviceEnumerator.UnregisterEndpointNotificationCallback(_notificationClient);

        // Stop brightness monitoring
        _brightnessWatcher.EventArrived -= OnWMIEvent;
        _brightnessWatcher.Stop();

        // Unsubscribe from events
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

        base.Stop();
    }

    private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
    {
        VolumeNotification?.Invoke(data.MasterVolume * 100.0f);
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

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        // do something
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
        monitorName = null;

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
                List<DisplayDevice> resolutions = GetResolutions(desktopScreen.screen.DeviceName);
                foreach (DisplayDevice mode in resolutions)
                {
                    ScreenResolution res = new ScreenResolution(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel);

                    // Get all frequencies for this resolution
                    List<int> frequencies = resolutions
                        .Where(a => a.dmPelsWidth == mode.dmPelsWidth &&
                                    a.dmPelsHeight == mode.dmPelsHeight &&
                                    a.dmBitsPerPel == mode.dmBitsPerPel)
                        .Select(b => b.dmDisplayFrequency)
                        .Distinct()
                        .OrderBy(f => f)
                        .ToList();

                    foreach (int frequency in frequencies)
                        res.Frequencies.Add(frequency, frequency);

                    if (!desktopScreen.HasResolution(res))
                        desktopScreen.screenResolutions.Add(res);
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
                nativeWidth = desktopScreen.nativeResolution.Width = Math.Min(maxRes.Width, nativeWidth);
                nativeHeight = desktopScreen.nativeResolution.Height = Math.Min(maxRes.Height, nativeHeight);

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

            // Update primary screen
            DesktopScreen newPrimary = desktopScreens.Values.FirstOrDefault(a => a.IsPrimary);
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
            foreach (DesktopScreen desktop in desktopScreens.Values.Where(a => !AllScreens.ContainsKey(a.DevicePath)))
            {
                LogManager.LogInformation("Screen {0} connected", desktop.ToString());
                ScreenConnected?.Invoke(desktop);
            }

            // Raise events for disconnected screens
            foreach (DesktopScreen desktop in AllScreens.Values.Where(a => !desktopScreens.ContainsKey(a.DevicePath)))
            {
                LogManager.LogInformation("Screen {0} disconnected", desktop.ToString());
                ScreenDisconnected?.Invoke(desktop);
            }

            // Update AllScreens collection
            foreach (var screen in AllScreens.Values)
                screen.Dispose();
            AllScreens.Clear();

            foreach (DesktopScreen desktop in desktopScreens.Values)
                AllScreens.TryAdd(desktop.DevicePath, desktop);

            // Notify subscribers of display settings change
            ScreenResolution screenResolution = PrimaryDesktop.GetResolution();
            if (screenResolution is not null)
                DisplaySettingsChanged?.Invoke(PrimaryDesktop, screenResolution);

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
        using (RegistryKey displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY"))
        {
            if (displayKey == null)
                return;

            // Each subkey here represents a monitor model.
            foreach (string monitorKeyName in displayKey.GetSubKeyNames())
            {
                using (RegistryKey monitorKey = displayKey.OpenSubKey(monitorKeyName))
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
                        using (RegistryKey deviceKey = monitorKey.OpenSubKey(deviceParamsPath))
                        {
                            if (deviceKey == null)
                                continue;

                            // Retrieve the EDID data.
                            byte[] edidData = deviceKey.GetValue("EDID") as byte[];
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

    #region Display Resolution Management

    public bool SetResolution(int width, int height, int displayFrequency)
    {
        return SetResolution(width, height, displayFrequency, 0);
    }

    public bool SetResolution(int width, int height, int displayFrequency, int bitsPerPel)
    {
        if (Status != ManagerStatus.Initialized)
            return false;

        DisplayDevice dm = new DisplayDevice
        {
            dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice)),
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
        _multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(volume / 100.0d);
    }

    public double GetVolume()
    {
        if (!_volumeSupport)
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

        _multimediaDevice.AudioEndpointVolume.Mute = true;
    }

    public void Unmute()
    {
        if (!_volumeSupport)
            return;

        _multimediaDevice.AudioEndpointVolume.Mute = false;
    }

    public void ToggleMute()
    {
        if (!_volumeSupport)
            return;

        _multimediaDevice.AudioEndpointVolume.Mute = !_multimediaDevice.AudioEndpointVolume.Mute;
    }

    public bool IsMuted()
    {
        if (!_volumeSupport)
            return true;

        return _multimediaDevice.AudioEndpointVolume.Mute;
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

    #region Nested Classes

    private class MMDeviceNotificationClient : IMMNotificationClient
    {
        private readonly MultimediaManager _multimediaManager;

        public MMDeviceNotificationClient(MultimediaManager multimediaManager)
        {
            _multimediaManager = multimediaManager;
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            _multimediaManager?.SetDefaultAudioEndPoint();
        }

        public void OnDeviceAdded(string deviceId) { }

        public void OnDeviceRemoved(string deviceId) { }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }

        public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }
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
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DisplayDevice lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DisplayDevice lpDevMode, int dwFlags);
    #endregion

    #region Events

    public event DisplaySettingsChangedEventHandler DisplaySettingsChanged;
    public delegate void DisplaySettingsChangedEventHandler(DesktopScreen screen, ScreenResolution resolution);

    public event PrimaryScreenChangedEventHandler PrimaryScreenChanged;
    public delegate void PrimaryScreenChangedEventHandler(DesktopScreen screen);

    public event ScreenConnectedEventHandler ScreenConnected;
    public delegate void ScreenConnectedEventHandler(DesktopScreen screen);

    public event ScreenDisconnectedEventHandler ScreenDisconnected;
    public delegate void ScreenDisconnectedEventHandler(DesktopScreen screen);

    public event VolumeNotificationEventHandler VolumeNotification;
    public delegate void VolumeNotificationEventHandler(float volume);

    public event BrightnessNotificationEventHandler BrightnessNotification;
    public delegate void BrightnessNotificationEventHandler(int brightness);

    #endregion
}