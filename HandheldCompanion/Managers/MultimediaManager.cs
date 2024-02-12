using HandheldCompanion.Managers.Desktop;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HandheldCompanion.Managers;

public static class MultimediaManager
{
    private static DesktopScreen desktopScreen;
    private static ScreenRotation screenOrientation;

    private static readonly MMDeviceEnumerator DevEnum;
    private static MMDevice multimediaDevice;
    private static readonly MMDeviceNotificationClient notificationClient;

    private static readonly ManagementEventWatcher BrightnessWatcher;
    private static readonly ManagementScope Scope;

    private static bool VolumeSupport;
    private static readonly bool BrightnessSupport;

    public static bool IsInitialized;

    static MultimediaManager()
    {
        // setup the multimedia device and get current volume value
        notificationClient = new MMDeviceNotificationClient();
        DevEnum = new MMDeviceEnumerator();
        DevEnum.RegisterEndpointNotificationCallback(notificationClient);
        SetDefaultAudioEndPoint();

        // get current brightness value
        Scope = new ManagementScope(@"\\.\root\wmi");
        Scope.Connect();

        // creating the watcher
        BrightnessWatcher = new ManagementEventWatcher(Scope, new EventQuery("Select * From WmiMonitorBrightnessEvent"));
        BrightnessWatcher.EventArrived += onWMIEvent;

        // check if we have control over brightness
        BrightnessSupport = GetBrightness() != -1;

        // manage events
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;
    }

    private static void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
    {
        VolumeNotification?.Invoke(data.MasterVolume * 100.0f);
    }

    private static void SetDefaultAudioEndPoint()
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

    private static void SettingsManager_SettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "NativeDisplayOrientation":
                {
                    var nativeOrientation = (ScreenRotation.Rotations)Convert.ToInt32(value);

                    if (!IsInitialized)
                        return;

                    var oldOrientation = screenOrientation.rotation;
                    screenOrientation = new ScreenRotation(screenOrientation.rotationUnnormalized, nativeOrientation);

                    if (oldOrientation != screenOrientation.rotation)
                        // Though the real orientation didn't change, raise event because the interpretation of it changed
                        DisplayOrientationChanged?.Invoke(screenOrientation);
                }
                break;
        }
    }

    private static void HotkeysManager_CommandExecuted(string listener)
    {
        switch (listener)
        {
            case "increaseBrightness":
                {
                    var stepRoundDn = (int)Math.Floor(GetBrightness() / 5.0d);
                    var brightness = stepRoundDn * 5 + 5;
                    SetBrightness(brightness);
                }
                break;
            case "decreaseBrightness":
                {
                    var stepRoundUp = (int)Math.Ceiling(GetBrightness() / 5.0d);
                    var brightness = stepRoundUp * 5 - 5;
                    SetBrightness(brightness);
                }
                break;
            case "increaseVolume":
                {
                    var stepRoundDn = (int)Math.Floor(Math.Round(GetVolume() / 5.0d, 2));
                    var volume = stepRoundDn * 5 + 5;
                    SetVolume(volume);
                }
                break;
            case "decreaseVolume":
                {
                    var stepRoundUp = (int)Math.Ceiling(Math.Round(GetVolume() / 5.0d, 2));
                    var volume = stepRoundUp * 5 - 5;
                    SetVolume(volume);
                }
                break;
        }
    }

    private static void onWMIEvent(object sender, EventArrivedEventArgs e)
    {
        var brightness = Convert.ToInt32(e.NewEvent.Properties["Brightness"].Value);
        BrightnessNotification?.Invoke(brightness);
    }

    private static void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        Screen PrimaryScreen = Screen.PrimaryScreen;

        if (desktopScreen is null || desktopScreen.PrimaryScreen.DeviceName != PrimaryScreen.DeviceName)
        {
            // update current desktop screen
            desktopScreen = new DesktopScreen(PrimaryScreen);
            desktopScreen.devMode = GetDisplay(desktopScreen.PrimaryScreen.DeviceName);

            // pull resolutions details
            List<Display> resolutions = GetResolutions(desktopScreen.PrimaryScreen.DeviceName);
            foreach (Display mode in resolutions)
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

            // sort resolutions
            desktopScreen.SortResolutions();

            // raise event
            PrimaryScreenChanged?.Invoke(desktopScreen);
        }
        else
        {
            // update current desktop resolution
            desktopScreen.devMode = GetDisplay(desktopScreen.PrimaryScreen.DeviceName);
        }

        ScreenRotation.Rotations oldOrientation = screenOrientation.rotation;

        if (!IsInitialized)
        {
            var nativeScreenRotation = (ScreenRotation.Rotations)SettingsManager.GetInt("NativeDisplayOrientation");
            screenOrientation = new ScreenRotation((ScreenRotation.Rotations)desktopScreen.devMode.dmDisplayOrientation,
                nativeScreenRotation);
            oldOrientation = ScreenRotation.Rotations.UNSET;

            if (nativeScreenRotation == ScreenRotation.Rotations.UNSET)
                SettingsManager.SetProperty("NativeDisplayOrientation", (int)screenOrientation.rotationNativeBase,
                    true);
        }
        else
        {
            screenOrientation = new ScreenRotation((ScreenRotation.Rotations)desktopScreen.devMode.dmDisplayOrientation,
                screenOrientation.rotationNativeBase);
        }

        // raise event
        ScreenResolution screenResolution = desktopScreen.GetResolution(desktopScreen.devMode.dmPelsWidth, desktopScreen.devMode.dmPelsHeight);
        if (screenResolution is not null)
            DisplaySettingsChanged?.Invoke(desktopScreen, screenResolution);

        // raise event
        if (oldOrientation != screenOrientation.rotation)
            DisplayOrientationChanged?.Invoke(screenOrientation);
    }

    public static DesktopScreen GetDesktopScreen()
    {
        return desktopScreen;
    }

    public static ScreenRotation GetScreenOrientation()
    {
        return screenOrientation;
    }

    public static void Start()
    {
        // start brightness watcher
        BrightnessWatcher.Start();

        // force trigger events
        SystemEvents_DisplaySettingsChanged(null, null);

        // get native resolution
        ScreenResolution nativeResolution = desktopScreen.screenResolutions.First();

        // get integer scaling dividers
        int idx = 1;

        while (true)
        {
            int height = nativeResolution.Height / idx;
            var dividedRes = desktopScreen.screenResolutions.FirstOrDefault(r => r.Height == height);
            if (dividedRes is null)
                break;

            desktopScreen.screenDividers.Add(new(idx, dividedRes));
            idx++;
        }

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "SystemManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        // stop brightness watcher
        BrightnessWatcher.Stop();

        DevEnum.UnregisterEndpointNotificationCallback(notificationClient);

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "SystemManager");
    }

    public static bool SetResolution(int width, int height, int displayFrequency)
    {
        if (!IsInitialized)
            return false;

        var ret = false;
        long RetVal = 0;
        var dm = new Display();
        dm.dmSize = (short)Marshal.SizeOf(typeof(Display));
        dm.dmPelsWidth = width;
        dm.dmPelsHeight = height;
        dm.dmDisplayFrequency = displayFrequency;
        dm.dmFields = Display.DM_PELSWIDTH | Display.DM_PELSHEIGHT | Display.DM_DISPLAYFREQUENCY;
        RetVal = ChangeDisplaySettings(ref dm, CDS_TEST);
        if (RetVal == 0)
        {
            RetVal = ChangeDisplaySettings(ref dm, 0);
            ret = true;
        }

        return ret;
    }

    public static bool SetResolution(int width, int height, int displayFrequency, int bitsPerPel)
    {
        if (!IsInitialized)
            return false;

        var ret = false;
        long RetVal = 0;
        var dm = new Display();
        dm.dmSize = (short)Marshal.SizeOf(typeof(Display));
        dm.dmPelsWidth = width;
        dm.dmPelsHeight = height;
        dm.dmDisplayFrequency = displayFrequency;
        dm.dmBitsPerPel = bitsPerPel;
        dm.dmFields = Display.DM_PELSWIDTH | Display.DM_PELSHEIGHT | Display.DM_DISPLAYFREQUENCY;
        RetVal = ChangeDisplaySettings(ref dm, CDS_TEST);
        if (RetVal == 0)
        {
            RetVal = ChangeDisplaySettings(ref dm, 0);
            ret = true;
        }

        return ret;
    }

    public static Display GetDisplay(string DeviceName)
    {
        var dm = new Display();
        dm.dmSize = (short)Marshal.SizeOf(typeof(Display));
        bool mybool;
        mybool = EnumDisplaySettings(DeviceName, -1, ref dm);
        return dm;
    }

    public static List<Display> GetResolutions(string DeviceName)
    {
        var allMode = new List<Display>();
        var dm = new Display();
        dm.dmSize = (short)Marshal.SizeOf(typeof(Display));
        var index = 0;
        while (EnumDisplaySettings(DeviceName, index, ref dm))
        {
            allMode.Add(dm);
            index++;
        }

        return allMode;
    }

    public static void PlayWindowsMedia(string file)
    {
        var path = Path.Combine(@"c:\Windows\Media\", file);
        if (File.Exists(path))
            new SoundPlayer(path).Play();
    }

    public static bool HasVolumeSupport()
    {
        return VolumeSupport;
    }

    public static void SetVolume(double volume)
    {
        if (!VolumeSupport)
            return;

        multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(volume / 100.0d);
    }

    public static double GetVolume()
    {
        if (!VolumeSupport)
            return 0.0d;

        return multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0d;
    }

    public static bool HasBrightnessSupport()
    {
        return BrightnessSupport;
    }

    public static void SetBrightness(double brightness)
    {
        if (!BrightnessSupport)
            return;

        try
        {
            using (var mclass = new ManagementClass("WmiMonitorBrightnessMethods"))
            {
                mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                using (var instances = mclass.GetInstances())
                {
                    foreach (ManagementObject instance in instances)
                    {
                        object[] args = { 1, brightness };
                        instance.InvokeMethod("WmiSetBrightness", args);
                    }
                }
            }
        }
        catch
        {
        }
    }

    public static short GetBrightness()
    {
        try
        {
            using (var mclass = new ManagementClass("WmiMonitorBrightness"))
            {
                mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                using (var instances = mclass.GetInstances())
                {
                    foreach (ManagementObject instance in instances)
                        return (byte)instance.GetPropertyValue("CurrentBrightness");
                }
            }

            return 0;
        }
        catch
        {
        }

        return -1;
    }

    private class MMDeviceNotificationClient : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            SetDefaultAudioEndPoint();
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct Display
    {
        public const int DM_DISPLAYFREQUENCY = 0x400000;
        public const int DM_PELSWIDTH = 0x80000;
        public const int DM_PELSHEIGHT = 0x100000;
        private const int CCHDEVICENAME = 32;
        private const int CCHFORMNAME = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string dmDeviceName;

        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;

        public int dmPositionX;
        public int dmPositionY;
        public DMDO dmDisplayOrientation;
        public int dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;

        public override string ToString()
        {
            return $"{dmPelsWidth}x{dmPelsHeight}, {dmDisplayFrequency}, {dmBitsPerPel}";
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int ChangeDisplaySettings([In] ref Display lpDevMode, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref Display lpDevMode);

    [Flags]
    public enum DisplayDeviceStateFlags
    {
        /// <summary>The device is part of the desktop.</summary>
        AttachedToDesktop = 0x1,
        MultiDriver = 0x2,

        /// <summary>The device is part of the desktop.</summary>
        PrimaryDevice = 0x4,

        /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
        MirroringDriver = 0x8,

        /// <summary>The device is VGA compatible.</summary>
        VGACompatible = 0x16,

        /// <summary>The device is removable; it cannot be the primary display.</summary>
        Removable = 0x20,

        /// <summary>The device has more display modes than its output devices support.</summary>
        ModesPruned = 0x8000000,
        Remote = 0x4000000,
        Disconnect = 0x2000000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DisplayDevice
    {
        [MarshalAs(UnmanagedType.U4)] public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        [MarshalAs(UnmanagedType.U4)] public DisplayDeviceStateFlags StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("User32.dll")]
    private static extern int EnumDisplayDevices(string lpDevice, int iDevNum, ref DisplayDevice lpDisplayDevice,
        int dwFlags);

    #endregion

    #region events

    public static event DisplaySettingsChangedEventHandler DisplaySettingsChanged;
    public delegate void DisplaySettingsChangedEventHandler(DesktopScreen screen, ScreenResolution resolution);

    public static event PrimaryScreenChangedEventHandler PrimaryScreenChanged;
    public delegate void PrimaryScreenChangedEventHandler(DesktopScreen screen);

    public static event DisplayOrientationChangedEventHandler DisplayOrientationChanged;
    public delegate void DisplayOrientationChangedEventHandler(ScreenRotation rotation);

    public static event VolumeNotificationEventHandler VolumeNotification;
    public delegate void VolumeNotificationEventHandler(float volume);

    public static event BrightnessNotificationEventHandler BrightnessNotification;
    public delegate void BrightnessNotificationEventHandler(int brightness);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}