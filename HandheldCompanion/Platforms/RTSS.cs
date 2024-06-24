using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Utils;
using RTSSSharedMemoryNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Platforms;

public class RTSS : IPlatform
{
    private const uint WM_APP = 0x8000;
    private const uint WM_RTSS_UPDATESETTINGS = WM_APP + 100;
    private const uint WM_RTSS_SHOW_PROPERTIES = WM_APP + 102;

    private const uint RTSSHOOKSFLAG_OSD_VISIBLE = 1;
    private const uint RTSSHOOKSFLAG_LIMITER_DISABLED = 4;
    private const string GLOBAL_PROFILE = "";

    private int HookedProcessId = 0;
    private bool ProfileLoaded;
    private AppEntry appEntry;

    private int RequestedFramerate;

    public RTSS()
    {
        PlatformType = PlatformType.RTSS;
        ExpectedVersion = new Version(7, 3, 4);
        Url = "https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html";

        Name = "RTSS";
        ExecutableName = RunningName = "RTSS.exe";

        // store specific modules
        Modules = new List<string>
        {
            "RTSSHooks64.dll"
        };

        // check if platform is installed
        InstallPath = RegistryUtils.GetString(@"SOFTWARE\WOW6432Node\Unwinder\RTSS", "InstallDir");
        if (Path.Exists(InstallPath))
        {
            // update paths
            SettingsPath = Path.Combine(InstallPath, @"Profiles\Global");
            ExecutablePath = Path.Combine(InstallPath, ExecutableName);

            // check executable
            if (File.Exists(ExecutablePath))
            {
                if (!HasModules)
                {
                    LogManager.LogWarning(
                        "Rivatuner Statistics Server RTSSHooks64.dll is missing. Please get it from: {0}", Url);
                    return;
                }

                // check executable version
                var versionInfo = FileVersionInfo.GetVersionInfo(ExecutablePath);
                var CurrentVersion = new Version(versionInfo.ProductMajorPart, versionInfo.ProductMinorPart,
                    versionInfo.ProductBuildPart);

                if (CurrentVersion < ExpectedVersion)
                {
                    LogManager.LogWarning("Rivatuner Statistics Server is outdated. Please get it from: {0}", Url);
                    return;
                }

                IsInstalled = true;
            }
        }

        if (!IsInstalled)
        {
            LogManager.LogWarning("Rivatuner Statistics Server is missing. Please get it from: {0}",
                "https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html");
            return;
        }

        // our main watchdog to (re)apply requested settings
        PlatformWatchdog = new Timer(2000) { Enabled = false };
        PlatformWatchdog.Elapsed += Watchdog_Elapsed;
    }

    public override bool Start()
    {
        // start RTSS if not running
        if (!IsRunning)
            StartProcess();
        else
            // hook into current process
            Process.Exited += Process_Exited;

        ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        ProcessManager.ProcessStopped += ProcessManager_ProcessStopped;
        ProfileManager.Applied += ProfileManager_Applied;

        // If RTSS was started while HC was fully initialized, we need to pass both current profile and foreground process
        if (SettingsManager.IsInitialized)
        {
            ProcessEx foregroundProcess = ProcessManager.GetForegroundProcess();
            ProcessManager_ForegroundChanged(foregroundProcess, null);

            ProfileManager_Applied(ProfileManager.GetCurrent(), UpdateSource.Background);
        }

        return base.Start();
    }

    public override bool Stop(bool kill = false)
    {
        ProcessManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
        ProcessManager.ProcessStopped -= ProcessManager_ProcessStopped;
        ProfileManager.Applied -= ProfileManager_Applied;

        return base.Stop(kill);
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        int frameLimit = 0;

        DesktopScreen desktopScreen = MultimediaManager.PrimaryDesktop;

        if (desktopScreen is not null)
        {
            // Determine most approriate frame rate limit based on screen frequency
            frameLimit = desktopScreen.GetClosest(profile.FramerateValue).limit;
        }

        if (frameLimit > 0)
        {
            // Apply profile-defined framerate
            RequestFPS(frameLimit);
        }
        else if (frameLimit == 0 && RequestedFramerate > 0)
        {
            // Reset to 0 only when a cap was set previously and the current profile has no limit 
            // These conditions prevent 0 from being set on every profile change 
            RequestFPS(frameLimit);
        }
    }

    private async void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx)
    {
        if (processEx is null || processEx == ProcessManager.Empty)
            return;

        // hook new process
        appEntry = null;

        var ProcessId = processEx.ProcessId;
        var foregroundId = ProcessId;
        if (ProcessId == 0)
            return;

        do
        {
            /*
             * loop until we either:
             * - got an RTSS entry
             * - process no longer exists
             * - RTSS was closed
             */

            ProcessEx foreground = ProcessManager.GetForegroundProcess();
            foregroundId = foreground is not null ? foreground.ProcessId : 0;

            try
            {
                appEntry = OSD.GetAppEntries().Where(x => (x.Flags & AppFlags.MASK) != AppFlags.None && x.ProcessId == ProcessId).FirstOrDefault();
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch { }

            await Task.Delay(1000);
        } while (appEntry is null && foregroundId == ProcessId && KeepAlive);

        if (appEntry is null)
            return;

        // set HookedProcessId
        HookedProcessId = appEntry.ProcessId;

        // raise event
        Hooked?.Invoke(appEntry);
    }

    private void ProcessManager_ProcessStopped(ProcessEx processEx)
    {
        var ProcessId = processEx.ProcessId;
        if (ProcessId != HookedProcessId)
            return;

        // clear HookedProcessId
        HookedProcessId = 0;

        // raise event
        Unhooked?.Invoke(ProcessId);
    }

    private void Watchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        lock (updateLock)
        {
            if (GetTargetFPS() != RequestedFramerate)
                SetTargetFPS(RequestedFramerate);

            try
            {
                // force "Show On-Screen Display" to On
                SetFlags(~RTSSHOOKSFLAG_OSD_VISIBLE, RTSSHOOKSFLAG_OSD_VISIBLE);
            }
            catch (DllNotFoundException)
            { }

            // force "On-Screen Display Support" to On
            if (GetEnableOSD() != true)
                SetEnableOSD(true);
        }
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        if (KeepAlive)
            StartProcess();
    }

    public bool HasHook()
    {
        return HookedProcessId != 0;
    }

    public void RefreshAppEntry()
    {
        // refresh appEntry
        int processId = appEntry is not null ? appEntry.ProcessId : 0;
        appEntry = OSD.GetAppEntries().Where(x => (x.Flags & AppFlags.MASK) != AppFlags.None).FirstOrDefault(a => a.ProcessId == processId);
    }

    public double GetFramerate(bool refresh = false)
    {
        try
        {
            if (refresh)
                RefreshAppEntry();

            if (appEntry is null)
                return 0.0d;

            return (double)appEntry.StatFrameTimeBufFramerate / 10;
        }
        catch (InvalidDataException)
        { }
        catch (FileNotFoundException)
        { }

        return 0.0d;
    }

    public double GetFrametime(bool refresh = false)
    {
        try
        {
            if (refresh)
                RefreshAppEntry();

            if (appEntry is null)
                return 0.0d;

            return (double)appEntry.InstantaneousFrameTime / 1000;
        }
        catch (InvalidDataException)
        { }
        catch (FileNotFoundException)
        { }

        return 0.0d;
    }

    public bool GetProfileProperty<T>(string propertyName, out T value)
    {
        var bytes = new byte[Marshal.SizeOf<T>()];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        value = default;
        try
        {
            if (!GetProfileProperty(propertyName, handle.AddrOfPinnedObject(), (uint)bytes.Length))
                return false;

            value = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            handle.Free();
        }
    }

    public bool SetProfileProperty<T>(string propertyName, T value)
    {
        var bytes = new byte[Marshal.SizeOf<T>()];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
            return SetProfileProperty(propertyName, handle.AddrOfPinnedObject(), (uint)bytes.Length);
        }
        catch
        {
            return false;
        }
        finally
        {
            handle.Free();
        }
    }

    public void UpdateSettings()
    {
        PostMessage(WM_RTSS_UPDATESETTINGS, IntPtr.Zero, IntPtr.Zero);
    }

    private void PostMessage(uint Msg, IntPtr wParam, IntPtr lParam)
    {
        var hWnd = FindWindow(null, "RTSS");
        if (hWnd == IntPtr.Zero)
            hWnd = FindWindow(null, "RivaTuner Statistics Server");

        if (hWnd != IntPtr.Zero)
            PostMessage(hWnd, Msg, wParam, lParam);
    }

    public uint EnableFlag(uint flag, bool status)
    {
        var current = SetFlags(~flag, status ? flag : 0);
        UpdateSettings();
        return current;
    }

    public bool GetEnableOSD()
    {
        if (!IsRunning)
            return false;

        try
        {
            // load default profile
            if (!ProfileLoaded)
            {
                LoadProfile();
                ProfileLoaded = true;
            }

            if (GetProfileProperty("EnableOSD", out int enabled))
                return Convert.ToBoolean(enabled);
        }
        catch
        {
        }

        return false;
    }

    public bool SetEnableOSD(bool enable)
    {
        if (!IsRunning)
            return false;

        try
        {
            // Ensure Global profile is loaded
            LoadProfile();

            // Set EnableOSD as requested
            if (SetProfileProperty("EnableOSD", enable ? 1 : 0))
            {
                // Save and reload profile
                SaveProfile();
                UpdateProfiles();

                return true;
            }
        }
        catch
        {
            LogManager.LogWarning("Failed to set OSD visibility settings in RTSS");
        }

        return false;
    }

    private bool SetTargetFPS(int Limit)
    {
        if (!IsRunning)
            return false;

        try
        {
            // Ensure Global profile is loaded
            LoadProfile();

            // Set Framerate Limit as requested
            if (SetProfileProperty("FramerateLimit", Limit))
            {
                // Save and reload profile
                SaveProfile();
                UpdateProfiles();

                return true;
            }
        }
        catch
        {
            LogManager.LogWarning("Failed to set Framerate Limit in RTSS");
        }

        /*
        if (File.Exists(SettingsPath))
        {
            IniFile iniFile = new(SettingsPath);
            if (iniFile.Write("Limit", Limit.ToString(), "Framerate"))
            {
                UpdateProfiles();
                return true;
            }
        }
        */

        return false;
    }

    private int GetTargetFPS()
    {
        if (!IsRunning)
            return 0;

        try
        {
            // load default profile
            if (!ProfileLoaded)
            {
                LoadProfile();
                ProfileLoaded = true;
            }

            if (GetProfileProperty("FramerateLimit", out int fpsLimit))
                return fpsLimit;
        }
        catch
        {
        }

        return 0;

        /*
        if (File.Exists(SettingsPath))
        {
            IniFile iniFile = new(SettingsPath);
            return Convert.ToInt32(iniFile.Read("Limit", "Framerate"));
        }
        */
    }

    public void RequestFPS(int framerate)
    {
        if (RequestedFramerate == framerate)
            return;

        RequestedFramerate = framerate;
        SetTargetFPS(framerate);
    }

    public override bool StartProcess()
    {
        if (!IsInstalled)
            return false;

        if (IsRunning)
            KillProcess();

        return base.StartProcess();
    }

    public override bool StopProcess()
    {
        if (IsStarting)
            return false;
        if (!IsInstalled)
            return false;
        if (!IsRunning)
            return false;

        KillProcess();

        return true;
    }

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }

    #region struct

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("RTSSHooks64.dll")]
    public static extern uint SetFlags(uint dwAND, uint dwXOR);

    [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
    public static extern void LoadProfile(string profile = GLOBAL_PROFILE);

    [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
    public static extern void SaveProfile(string profile = GLOBAL_PROFILE);

    [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
    public static extern void DeleteProfile(string profile = GLOBAL_PROFILE);

    [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
    public static extern bool GetProfileProperty(string propertyName, IntPtr value, uint size);

    [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
    public static extern bool SetProfileProperty(string propertyName, IntPtr value, uint size);

    [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
    public static extern void ResetProfile(string profile = GLOBAL_PROFILE);

    [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
    public static extern void UpdateProfiles();

    #endregion

    #region events

    public event HookedEventHandler Hooked;

    public delegate void HookedEventHandler(AppEntry appEntry);

    public event UnhookedEventHandler Unhooked;

    public delegate void UnhookedEventHandler(int processId);

    #endregion
}