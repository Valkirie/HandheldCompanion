using System;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;

namespace HandheldCompanion.Managers;

public static class PlatformManager
{
    private const int UpdateInterval = 1000;

    // gaming platforms
    private static readonly SteamPlatform Steam = new();
    private static readonly GOGGalaxy GOGGalaxy = new();
    private static readonly UbisoftConnect UbisoftConnect = new();

    // misc platforms
    public static RTSS RTSS = new();
    public static HWiNFO HWiNFO = new();
    private static Timer UpdateTimer;

    private static bool IsInitialized;

    private static PlatformNeeds CurrentNeeds = PlatformNeeds.None;
    private static PlatformNeeds PreviousNeeds = PlatformNeeds.None;

    public static void Start()
    {
        if (Steam.IsInstalled)
        {
            Steam.Start();
        }

        if (GOGGalaxy.IsInstalled)
        {
            // do something
        }

        if (UbisoftConnect.IsInstalled)
        {
            // do something
        }

        if (RTSS.IsInstalled)
        {
            // do something
        }

        if (HWiNFO.IsInstalled)
        {
            // do something
        }

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ProfileManager.Applied += ProfileManager_Applied;

        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => MonitorPlatforms();

        IsInitialized = true;

        LogManager.LogInformation("{0} has started", "PlatformManager");
    }

    private static void ProfileManager_Applied(Profile profile, ProfileUpdateSource source)
    {
        // AutoTDP
        if (profile.AutoTDPEnabled)
            CurrentNeeds |= PlatformNeeds.AutoTDP;
        else
            CurrentNeeds &= ~PlatformNeeds.AutoTDP;

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
                if (RTSS.IsInstalled)
                {
                    // Start and enable OSD
                    RTSS.Start();
                    RTSS.SetEnableOSD(true);
                }
            if (CurrentNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
            {
                // This condition checks if OnScreenDisplayComplex is true
                // OnScreenDisplayComplex is a new flag that indicates if the OSD needs more information from HWiNFO
                if (!PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) ||
                    !PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
                    // Only start HWiNFO if it was not running before or if OnScreenDisplayComplex was false and if it is installed
                    if (HWiNFO.IsInstalled)
                        HWiNFO.Start();
            }
            else
            {
                // If OnScreenDisplayComplex is false, stop HWiNFO if it was running before
                if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) &&
                    PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
                    // Only stop HWiNFO if it is installed
                    if (HWiNFO.IsInstalled)
                        HWiNFO.Stop(true);
            }
        }
        else if (CurrentNeeds.HasFlag(PlatformNeeds.AutoTDP) || CurrentNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
        {
            // If AutoTDP or framerate limiter is needed, start only RTSS and stop HWiNFO
            if (!PreviousNeeds.HasFlag(PlatformNeeds.AutoTDP) && !PreviousNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
                // Only start RTSS if it was not running before and if it is installed
                if (RTSS.IsInstalled)
                    RTSS.Start();

            // Only stop HWiNFO if it was running before
            // Only stop HWiNFO if it is installed
            if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay))
                if (HWiNFO.IsInstalled)
                    HWiNFO.Stop(true);

            // Disable OSD
            RTSS.SetEnableOSD(false);
        }
        else
        {
            // If none of the needs are present, stop both HWiNFO and RTSS
            if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) || PreviousNeeds.HasFlag(PlatformNeeds.AutoTDP) ||
                PreviousNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
            {
                // Only stop HWiNFO and RTSS if they were running before and if they are installed
                if (HWiNFO.IsInstalled) HWiNFO.Stop(true);
                if (RTSS.IsInstalled)
                {
                    // Disable OSD
                    RTSS.SetEnableOSD(false);
                    RTSS.Stop();
                }
            }
        }

        // Store the current needs in the previous needs variable
        PreviousNeeds = CurrentNeeds;
    }

    public static void Stop()
    {
        if (Steam.IsInstalled)
            Steam.Stop();

        if (GOGGalaxy.IsInstalled)
            GOGGalaxy.Dispose();

        if (UbisoftConnect.IsInstalled)
            UbisoftConnect.Dispose();

        if (RTSS.IsInstalled)
        {
            var killRTSS = SettingsManager.GetBoolean("PlatformRTSSEnabled");
            RTSS.Stop(killRTSS);
            RTSS.Dispose();
        }

        if (HWiNFO.IsInstalled)
        {
            var killHWiNFO = SettingsManager.GetBoolean("PlatformHWiNFOEnabled");
            HWiNFO.Stop(killHWiNFO);
            HWiNFO.Dispose();
        }

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "PlatformManager");
    }

    public static PlatformType GetPlatform(Process proc)
    {
        if (!IsInitialized)
            return PlatformType.Windows;

        // is this process part of a specific platform
        if (Steam.IsRelated(proc))
            return Steam.PlatformType;
        if (GOGGalaxy.IsRelated(proc))
            return GOGGalaxy.PlatformType;
        if (UbisoftConnect.IsRelated(proc))
            return UbisoftConnect.PlatformType;
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