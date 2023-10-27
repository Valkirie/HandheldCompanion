using HandheldCompanion.Platforms;
using System;
using System.Diagnostics;
using System.Timers;
using System.Windows;

namespace HandheldCompanion.Managers;

public static class PlatformManager
{
    private const int UpdateInterval = 1000;

    // gaming platforms
    public static readonly SteamPlatform steam = new();
    public static readonly GOGGalaxy gOGGalaxy = new();
    public static readonly UbisoftConnect ubisoftConnect = new();

    // misc platforms
    public static RTSS rTSS = new();
    public static HWiNFO hWiNFO = new();
    public static OpenHardwareMonitor openHardwareMonitor = new();

    private static Timer UpdateTimer;

    private static bool IsInitialized;

    private static PlatformNeeds CurrentNeeds = PlatformNeeds.None;
    private static PlatformNeeds PreviousNeeds = PlatformNeeds.None;

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    public static void Start()
    {
        if (steam.IsInstalled)
            steam.Start();

        if (gOGGalaxy.IsInstalled)
        {
            // do something
        }

        if (ubisoftConnect.IsInstalled)
        {
            // do something
        }

        if (rTSS.IsInstalled)
        {
            // do something
        }

        if (hWiNFO.IsInstalled)
        {
            // do something
        }

        if (openHardwareMonitor.IsInstalled)
            openHardwareMonitor.Start();

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ProfileManager.Applied += ProfileManager_Applied;
        PowerProfileManager.Applied += PowerProfileManager_Applied;

        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => MonitorPlatforms();

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "PlatformManager");
    }

    private static void PowerProfileManager_Applied(Misc.PowerProfile profile, UpdateSource source)
    {
        // AutoTDP
        if (profile.AutoTDPEnabled)
            CurrentNeeds |= PlatformNeeds.AutoTDP;
        else
            CurrentNeeds &= ~PlatformNeeds.AutoTDP;

        UpdateTimer.Stop();
        UpdateTimer.Start();
    }

    private static void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // Framerate limiter
        if (profile.FramerateEnabled)
            CurrentNeeds |= PlatformNeeds.FramerateLimiter;
        else
            CurrentNeeds &= ~PlatformNeeds.FramerateLimiter;

        UpdateTimer.Stop();
        UpdateTimer.Start();
    }

    private static void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
                case "OnScreenDisplayLevel":
                    {
                        var level = Convert.ToInt16(value);

                        switch (level)
                        {
                            case 0: // Disabled
                                CurrentNeeds &= ~PlatformNeeds.OnScreenDisplay;
                                CurrentNeeds &= ~PlatformNeeds.OnScreenDisplayComplex;
                                break;
                            default:
                            case 1: // Minimal
                                CurrentNeeds |= PlatformNeeds.OnScreenDisplay;
                                CurrentNeeds &= ~PlatformNeeds.OnScreenDisplayComplex;
                                break;
                            case 2: // Extended
                            case 3: // Full
                            case 4: // External
                                CurrentNeeds |= PlatformNeeds.OnScreenDisplay;
                                CurrentNeeds |= PlatformNeeds.OnScreenDisplayComplex;
                                break;
                        }

                        UpdateTimer.Stop();
                        UpdateTimer.Start();
                    }
                    break;
            }
        });
    }

    private static void MonitorPlatforms()
    {
        /*
         * Dependencies:
         * HWInfo: OSD
         * RTSS: AutoTDP, framerate limiter, OSD
         */

        // Check if the current needs are the same as the previous needs
        if (CurrentNeeds == PreviousNeeds) return;

        // Start or stop HWiNFO and RTSS based on the current and previous needs
        if (CurrentNeeds.HasFlag(PlatformNeeds.OnScreenDisplay))
        {
            // If OSD is needed, start RTSS and start HWiNFO only if OnScreenDisplayComplex is true
            if (!PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay))
                // Only start RTSS if it was not running before and if it is installed
                if (rTSS.IsInstalled)
                {
                    // Start RTSS
                    rTSS.Start();
                }
            if (CurrentNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
            {
                // This condition checks if OnScreenDisplayComplex is true
                // OnScreenDisplayComplex is a new flag that indicates if the OSD needs more information from HWiNFO
                if (!PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) ||
                    !PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
                    // Only start HWiNFO if it was not running before or if OnScreenDisplayComplex was false and if it is installed
                    if (hWiNFO.IsInstalled)
                        hWiNFO.Start();
            }
            else
            {
                // If OnScreenDisplayComplex is false, stop HWiNFO if it was running before
                if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) &&
                    PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
                    // Only stop HWiNFO if it is installed
                    if (hWiNFO.IsInstalled)
                        hWiNFO.Stop(true);
            }
        }
        else if (CurrentNeeds.HasFlag(PlatformNeeds.AutoTDP) || CurrentNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
        {
            // If AutoTDP or framerate limiter is needed, start only RTSS and stop HWiNFO
            if (!PreviousNeeds.HasFlag(PlatformNeeds.AutoTDP) && !PreviousNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
                // Only start RTSS if it was not running before and if it is installed
                if (rTSS.IsInstalled)
                    rTSS.Start();

            // Only stop HWiNFO if it was running before
            // Only stop HWiNFO if it is installed
            if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay))
                if (hWiNFO.IsInstalled)
                    hWiNFO.Stop(true);
        }
        else
        {
            // If none of the needs are present, stop both HWiNFO and RTSS
            if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) || PreviousNeeds.HasFlag(PlatformNeeds.AutoTDP) ||
                PreviousNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
            {
                // Only stop HWiNFO and RTSS if they were running before and if they are installed
                if (hWiNFO.IsInstalled) hWiNFO.Stop(true);
                if (rTSS.IsInstalled)
                {
                    // Stop RTSS
                    rTSS.Stop();
                }
            }
        }

        // Store the current needs in the previous needs variable
        PreviousNeeds = CurrentNeeds;
    }

    public static void Stop()
    {
        if (steam.IsInstalled)
            steam.Stop();

        if (gOGGalaxy.IsInstalled)
            gOGGalaxy.Dispose();

        if (ubisoftConnect.IsInstalled)
            ubisoftConnect.Dispose();

        if (rTSS.IsInstalled)
        {
            var killRTSS = SettingsManager.GetBoolean("PlatformRTSSEnabled");
            rTSS.Stop(killRTSS);
            rTSS.Dispose();
        }

        if (hWiNFO.IsInstalled)
        {
            var killHWiNFO = SettingsManager.GetBoolean("PlatformHWiNFOEnabled");
            hWiNFO.Stop(killHWiNFO);
            hWiNFO.Dispose();
        }

        if (openHardwareMonitor.IsInstalled)
            openHardwareMonitor.Stop();

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "PlatformManager");
    }

    public static PlatformType GetPlatform(Process proc)
    {
        if (!IsInitialized)
            return PlatformType.Windows;

        // is this process part of a specific platform
        if (steam.IsRelated(proc))
            return steam.PlatformType;
        if (gOGGalaxy.IsRelated(proc))
            return gOGGalaxy.PlatformType;
        if (ubisoftConnect.IsRelated(proc))
            return ubisoftConnect.PlatformType;
        return PlatformType.Windows;
    }

    [Flags]
    private enum PlatformNeeds
    {
        None = 0,
        AutoTDP = 1,
        FramerateLimiter = 2,
        OnScreenDisplay = 4,
        OnScreenDisplayComplex = 8
    }
}