using HandheldCompanion.Platforms;
using HandheldCompanion.Shared;
using System;
using System.Diagnostics;

namespace HandheldCompanion.Managers;

public static class PlatformManager
{
    // gaming platforms
    public static readonly Steam Steam = new();
    public static readonly GOGGalaxy GOGGalaxy = new();
    public static readonly UbisoftConnect UbisoftConnect = new();

    // misc platforms
    public static RTSS RTSS = new();
    public static Platforms.LibreHardwareMonitor LibreHardwareMonitor = new();

    public static bool IsInitialized;

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    public static void Start()
    {
        if (IsInitialized)
            return;

        if (Steam.IsInstalled)
            Steam.Start();

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
            RTSS.Start();
        }

        if (LibreHardwareMonitor.IsInstalled)
        {
            LibreHardwareMonitor.Start();
        }

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "PlatformManager");
    }

    public static void Stop()
    {
        if (Steam.IsInstalled)
            Steam.Stop();

        if (GOGGalaxy.IsInstalled)
            GOGGalaxy.Stop();

        if (UbisoftConnect.IsInstalled)
            UbisoftConnect.Stop();

        if (RTSS.IsInstalled)
        {
            bool killRTSS = ManagerFactory.settingsManager.GetBoolean("PlatformRTSSEnabled");
            RTSS.Stop(killRTSS);
        }

        if (LibreHardwareMonitor.IsInstalled)
        {
            LibreHardwareMonitor.Stop();
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