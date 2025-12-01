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
    public ConcurrentDictionary<string, DesktopScreen> AllScreens = [];
    public DesktopScreen PrimaryDesktop;

    private readonly MMDeviceEnumerator DevEnum;
    private MMDevice multimediaDevice;
    private readonly MMDeviceNotificationClient notificationClient;

    private readonly ManagementEventWatcher BrightnessWatcher;
    private readonly ManagementScope Scope;

    private bool VolumeSupport;
    private readonly bool BrightnessSupport;

    public MultimediaManager()
    {
        // setup the multimedia device and get current volume value
        notificationClient = new MMDeviceNotificationClient(this);
        DevEnum = new MMDeviceEnumerator();
        DevEnum.RegisterEndpointNotificationCallback(notificationClient);
        SetDefaultAudioEndPoint();

        // get current brightness value
        Scope = new ManagementScope(@"\\.\root\wmi");
        Scope.Connect();

        // creating the watcher
        BrightnessWatcher = new ManagementEventWatcher(Scope, new EventQuery("Select * From WmiMonitorBrightnessEvent"));

        // check if we have control over brightness
        BrightnessSupport = GetBrightness() != -1;
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // manage brightness watcher events
        BrightnessWatcher.EventArrived += onWMIEvent;
        BrightnessWatcher.Start();

        // manage events
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

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

        SystemEvents_DisplaySettingsChanged(null, null);

        base.Start();
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
        // do something
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        DevEnum.UnregisterEndpointNotificationCallback(notificationClient);

        // stop brightness watcher
        BrightnessWatcher.EventArrived -= onWMIEvent;
        BrightnessWatcher.Stop();

        // manage events
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
            if (multimediaDevice is not null && multimediaDevice.AudioEndpointVolume is not null)
            {
                VolumeSupport = false;
                multimediaDevice.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
            }

            multimediaDevice = DevEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            if (multimediaDevice is not null && multimediaDevice.AudioEndpointVolume is not null)
            {
                VolumeSupport = true;
                multimediaDevice.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
            }

            // do this even when no device found, to set to 0
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

    private void onWMIEvent(object sender, EventArrivedEventArgs e)
    {
        int brightness = Convert.ToInt32(e.NewEvent.Properties["Brightness"].Value);
        BrightnessNotification?.Invoke(brightness);
    }

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

    public string GetAdapterFriendlyName(string DeviceName)
    {
        string friendlyName = string.Empty;

        try
        {
            Display? PrimaryDisplay = Display.GetDisplays().Where(display => display.DisplayName.Equals(DeviceName)).FirstOrDefault();
            if (PrimaryDisplay is not null)
            {
                if (!string.IsNullOrEmpty(PrimaryDisplay.DeviceName))
                    friendlyName = PrimaryDisplay.DeviceName;

                PathDisplayTarget? PrimaryTarget = GetDisplayTarget(PrimaryDisplay.DevicePath);
                if (PrimaryTarget is not null && !string.IsNullOrEmpty(PrimaryTarget.FriendlyName))
                    friendlyName = PrimaryTarget.FriendlyName;
            }
        }
        catch { }

        return friendlyName;
    }

    public static string GetDisplayPath(string DeviceName)
    {
        string DevicePath = string.Empty;

        try
        {
            Display? PrimaryDisplay = Display.GetDisplays().Where(display => display.DisplayName.Equals(DeviceName)).FirstOrDefault();
            if (PrimaryDisplay is not null)
                DevicePath = PrimaryDisplay.DevicePath;
        }
        catch { }

        return DevicePath;
    }

    private PathDisplayTarget? GetDisplayTarget(string DevicePath)
    {
        return PathDisplayTarget.GetDisplayTargets().Where(target => target.DevicePath.Equals(DevicePath)).FirstOrDefault();
    }

    private object displayLock = new();
    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        lock (displayLock)
        {
            // set flag
            AddStatus(ManagerStatus.Busy);

            // temporary array to store all current screens
            Dictionary<string, DesktopScreen> desktopScreens = [];

            foreach (Screen screen in Screen.AllScreens)
            {
                if (string.IsNullOrEmpty(screen.DeviceName))
                    continue;

                DesktopScreen desktopScreen = new(screen);

                // pull resolutions details
                List<DisplayDevice> resolutions = GetResolutions(desktopScreen.screen.DeviceName);
                foreach (DisplayDevice mode in resolutions)
                {
                    ScreenResolution res = new ScreenResolution(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel);

                    List<int> frequencies = resolutions
                        .Where(a => a.dmPelsWidth == mode.dmPelsWidth && a.dmPelsHeight == mode.dmPelsHeight)
                        .Select(b => b.dmDisplayFrequency).Distinct().ToList();

                    foreach (int frequency in frequencies)
                        res.Frequencies.Add(frequency, frequency);

                    if (!desktopScreen.HasResolution(res))
                        desktopScreen.screenResolutions.Add(res);
                }

                // get maximum resolution
                ScreenResolution currentResolution = new(screen.Bounds.Width, screen.Bounds.Height, screen.BitsPerPixel);

                // store maximum resolution as temporary native resolution
                desktopScreen.nativeResolution = new(screen.Bounds.Width, screen.Bounds.Height, screen.BitsPerPixel);

                int nativeWidth = desktopScreen.nativeResolution.Width;
                int nativeHeight = desktopScreen.nativeResolution.Height;

                // get native resolution
                Regex regex = new Regex(@"^\\\\\?\\DISPLAY#[^#]+#(?<instance>[^#]+)(?=[#\{])", RegexOptions.IgnoreCase);
                Match match = regex.Match(desktopScreen.DevicePath);
                if (match.Success && match.Groups.ContainsKey("instance"))
                {
                    string instanceKeyName = match.Groups["instance"].Value;
                    GetNativeResolutions(instanceKeyName, ref nativeWidth, ref nativeHeight);

                    // update native resolution
                    desktopScreen.nativeResolution.Width = nativeWidth;
                    desktopScreen.nativeResolution.Height = nativeHeight;
                }

                // some devices have portrait-native display and therefore reversed width/height
                if (desktopScreen.nativeResolution.Orientation == ScreenOrientation.Portrait)
                {
                    // swap values
                    if (currentResolution.Orientation == ScreenOrientation.Landscape)
                        (nativeWidth, nativeHeight) = (nativeHeight, nativeWidth);
                }

                // sort resolutions, based on orientation
                if (currentResolution.Orientation == ScreenOrientation.Landscape)
                {
                    desktopScreen.screenResolutions = desktopScreen.screenResolutions
                        .OrderByDescending(r => r.Width)
                        .ThenByDescending(r => r.Height)
                        .ToList();
                }
                else
                {
                    desktopScreen.screenResolutions = desktopScreen.screenResolutions
                        .OrderByDescending(r => r.Height)
                        .ThenByDescending(r => r.Width)
                        .ToList();
                }

                // update native resolution, based on orientation
                ScreenResolution? maxRes = desktopScreen.screenResolutions.FirstOrDefault();
                nativeWidth = desktopScreen.nativeResolution.Width = Math.Min(maxRes.Width, nativeWidth);
                nativeHeight = desktopScreen.nativeResolution.Height = Math.Min(maxRes.Height, nativeHeight);

                // get integer scaling dividers
                int idx = 1;
                while (true)
                {
                    int height = nativeHeight / idx;
                    int width = nativeWidth;

                    ScreenResolution? dividedRes = desktopScreen.screenResolutions.FirstOrDefault(res => res.Height == height && res.Width <= width);
                    if (dividedRes is null)
                        break;

                    desktopScreen.screenDividers.Add(new(idx, dividedRes));
                    idx++;
                }

                // add to temporary array
                desktopScreens.Add(desktopScreen.DevicePath, desktopScreen);
            }

            // get refreshed primary screen (can't be null)
            DesktopScreen newPrimary = desktopScreens.Values.FirstOrDefault(a => a.IsPrimary);
            if (newPrimary is not null)
            {
                bool IsNew = PrimaryDesktop?.DevicePath != newPrimary.DevicePath;

                // set or update current primary
                PrimaryDesktop?.Dispose();
                PrimaryDesktop = newPrimary;

                // looks like we have a new primary screen
                if (IsNew)
                {
                    LogManager.LogInformation("Primary screen set to {0}", newPrimary.ToString());

                    // raise event (New primary display)
                    PrimaryScreenChanged?.Invoke(newPrimary);
                }
            }

            // raise event (New screen detected)
            foreach (DesktopScreen desktop in desktopScreens.Values.Where(a => !AllScreens.ContainsKey(a.DevicePath)))
            {
                LogManager.LogInformation("Screen {0} connected", desktop.ToString());
                ScreenConnected?.Invoke(desktop);
            }

            // raise event (New screen detected)
            foreach (DesktopScreen desktop in AllScreens.Values.Where(a => !desktopScreens.ContainsKey(a.DevicePath)))
            {
                LogManager.LogInformation("Screen {0} disconnected", desktop.ToString());
                ScreenDisconnected?.Invoke(desktop);
            }

            // clear array and transfer screens
            foreach (var screen in AllScreens.Values)
                screen.Dispose();
            AllScreens.Clear();

            foreach (DesktopScreen desktop in desktopScreens.Values)
                AllScreens.TryAdd(desktop.DevicePath, desktop);

            // raise event (Display settings were updated)
            ScreenResolution screenResolution = PrimaryDesktop.GetResolution();
            if (screenResolution is not null)
                DisplaySettingsChanged?.Invoke(PrimaryDesktop, screenResolution);

            // set flag
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

    public bool SetResolution(int width, int height, int displayFrequency)
    {
        if (Status != ManagerStatus.Initialized)
            return false;

        bool ret = false;
        DisplayDevice dm = new DisplayDevice
        {
            dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice)),
            dmPelsWidth = width,
            dmPelsHeight = height,
            dmDisplayFrequency = displayFrequency,
            dmFields = DisplayDevice.DM_PELSWIDTH | DisplayDevice.DM_PELSHEIGHT | DisplayDevice.DM_DISPLAYFREQUENCY
        };

        long RetVal = ChangeDisplaySettings(ref dm, CDS_TEST);
        if (RetVal == 0)
        {
            RetVal = ChangeDisplaySettings(ref dm, 0);
            ret = true;
        }

        return ret;
    }

    public bool SetResolution(int width, int height, int displayFrequency, int bitsPerPel)
    {
        if (Status != ManagerStatus.Initialized)
            return false;

        bool ret = false;
        DisplayDevice dm = new DisplayDevice
        {
            dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice)),
            dmPelsWidth = width,
            dmPelsHeight = height,
            dmDisplayFrequency = displayFrequency,
            dmBitsPerPel = bitsPerPel,
            dmFields = DisplayDevice.DM_PELSWIDTH | DisplayDevice.DM_PELSHEIGHT | DisplayDevice.DM_DISPLAYFREQUENCY
        };

        long RetVal = ChangeDisplaySettings(ref dm, CDS_TEST);
        if (RetVal == 0)
        {
            RetVal = ChangeDisplaySettings(ref dm, 0);
            ret = true;
        }

        return ret;
    }

    public static DisplayDevice GetDisplay(string DeviceName)
    {
        DisplayDevice dm = new DisplayDevice
        {
            dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice))
        };
        EnumDisplaySettings(DeviceName, -1, ref dm);
        return dm;
    }

    public List<DisplayDevice> GetResolutions(string DeviceName)
    {
        List<DisplayDevice> allMode = [];
        DisplayDevice dm = new DisplayDevice
        {
            dmSize = (short)Marshal.SizeOf(typeof(DisplayDevice))
        };
        int index = 0;
        while (EnumDisplaySettings(DeviceName, index, ref dm))
        {
            allMode.Add(dm);
            index++;
        }

        return allMode;
    }

    public void PlayWindowsMedia(string file)
    {
        string path = Path.Combine(@"c:\Windows\Media\", file);
        if (File.Exists(path))
            new SoundPlayer(path).Play();
    }

    public bool HasVolumeSupport()
    {
        return VolumeSupport;
    }

    public void SetVolume(double volume)
    {
        if (!VolumeSupport)
            return;

        // clamp to [0,100]
        volume = Math.Clamp(volume, 0.0d, 100.0d);

        multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(volume / 100.0d);
    }

    public double GetVolume()
    {
        if (!VolumeSupport)
            return 0.0d;

        return multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0d;
    }

    public bool HasBrightnessSupport()
    {
        return BrightnessSupport;
    }

    public void SetBrightness(double brightness)
    {
        if (!BrightnessSupport)
            return;

        // clamp to [0,100]
        brightness = Math.Clamp(brightness, 0.0d, 100.0d);

        try
        {
            using (ManagementClass mclass = new ManagementClass("WmiMonitorBrightnessMethods"))
            {
                mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                using (ManagementObjectCollection instances = mclass.GetInstances())
                {
                    foreach (ManagementObject instance in instances)
                    {
                        object[] args = { 1, brightness };
                        instance.InvokeMethod("WmiSetBrightness", args);
                    }
                }
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
        if (!VolumeSupport)
            return;

        multimediaDevice.AudioEndpointVolume.Mute = true;
    }

    public void Unmute()
    {
        if (!VolumeSupport)
            return;

        multimediaDevice.AudioEndpointVolume.Mute = false;
    }

    public void ToggleMute()
    {
        if (!VolumeSupport)
            return;

        multimediaDevice.AudioEndpointVolume.Mute = !multimediaDevice.AudioEndpointVolume.Mute;
    }

    public bool IsMuted()
    {
        if (!VolumeSupport)
            return true;

        return multimediaDevice.AudioEndpointVolume.Mute;
    }

    public short GetBrightness()
    {
        try
        {
            using (ManagementClass mclass = new ManagementClass("WmiMonitorBrightness"))
            {
                mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                using (ManagementObjectCollection instances = mclass.GetInstances())
                {
                    foreach (ManagementObject instance in instances)
                        return (byte)instance.GetPropertyValue("CurrentBrightness");
                }
            }
        }
        catch { }

        return -1;
    }

    private class MMDeviceNotificationClient : IMMNotificationClient
    {
        private MultimediaManager MultimediaManager;
        public MMDeviceNotificationClient(MultimediaManager multimediaManager)
        {
            this.MultimediaManager = multimediaManager;
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            MultimediaManager?.SetDefaultAudioEndPoint();
        }

        public void OnDeviceAdded(string deviceId)
        {
        }

        public void OnDeviceRemoved(string deviceId)
        {
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
        }

        public void OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
        }
    }

    #region imports

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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int ChangeDisplaySettings([In] ref DisplayDevice lpDevMode, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DisplayDevice lpDevMode);
    #endregion

    #region events

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