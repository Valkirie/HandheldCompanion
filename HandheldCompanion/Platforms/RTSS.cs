using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using HandheldCompanion;
using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using RTSSSharedMemoryNET;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Platforms;

public class RTSS : IPlatform
{
    public enum AppFlagsEx
    {
        None = 0x0,
        OpenGL = 0x1,
        DDraw = 0x2,
        D3D8 = 0x3,
        D3D9 = 0x4,
        D3D9Ex = 0x5,
        D3D10 = 0x6,
        D3D11 = 0x7,
        D3D12 = 0x8,
        D3D12AFR = 0x9,
        Vulkan = 0xA,
        ProfileUpdateRequested = 0x10000000,
        MASK = 0xF
    }

    private const uint WM_APP = 0x8000;
    private const uint WM_RTSS_UPDATESETTINGS = WM_APP + 100;
    private const uint WM_RTSS_SHOW_PROPERTIES = WM_APP + 102;

    private const uint RTSSHOOKSFLAG_OSD_VISIBLE = 1;
    private const uint RTSSHOOKSFLAG_LIMITER_DISABLED = 4;
    private const string GLOBAL_PROFILE = "";

    private readonly ConcurrentList<int> HookedProcessIds = new();
    private bool ProfileLoaded;

    private int RequestedFramerate;

    public RTSS()
    {
        PlatformType = PlatformType.RTSS;
        ExpectedVersion = new Version(7, 3, 4);
        Url = "https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html";

        Name = "RTSS";
        ExecutableName = "RTSS.exe";

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
        if (!IsRunning())
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
            var foregroundProcess = ProcessManager.GetForegroundProcess();
            if (foregroundProcess is not null)
                ProcessManager_ForegroundChanged(foregroundProcess, null);

            ProfileManager_Applied(ProfileManager.GetCurrent(), ProfileUpdateSource.Background);
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

    private void ProfileManager_Applied(Profile profile, ProfileUpdateSource source)
    {
        // apply profile defined framerate
        if (profile.FramerateEnabled)
        {
            var frequency = (int)Math.Floor(SystemManager.GetDesktopScreen().GetFrequency()
                .GetValue((Frequency)profile.FramerateValue));
            RequestFPS(frequency);
        }
        else
        {
            // restore default framerate
            RequestFPS(0);
        }
    }

    private async void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
    {
        // hook new process
        AppEntry appEntry = null;

        var ProcessId = processEx.GetProcessId();
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

            try
            {
                appEntry = OSD.GetAppEntries().Where(x => (x.Flags & AppFlags.MASK) != AppFlags.None && x.ProcessId == ProcessId).FirstOrDefault();
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch {}

            await Task.Delay(1000);
        } while (appEntry is null && ProcessManager.HasProcess(ProcessId) && KeepAlive);

        if (appEntry is null)
            return;

        // raise event
        Hooked?.Invoke(appEntry);

        // we're already hooked into this process
        if (HookedProcessIds.Contains(ProcessId))
            return;

        // store into array
        HookedProcessIds.Add(ProcessId);
    }

    private void ProcessManager_ProcessStopped(ProcessEx processEx)
    {
        var ProcessId = processEx.GetProcessId();
        if (ProcessId == 0)
            return;

        // we're not hooked into this process
        if (!HookedProcessIds.Contains(ProcessId))
            return;

        // remove from array
        HookedProcessIds.Remove(ProcessId);

        // raise event
        Unhooked?.Invoke(ProcessId);
    }

    private void Watchdog_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (Monitor.TryEnter(updateLock))
        {
            if (GetTargetFPS() != RequestedFramerate)
                SetTargetFPS(RequestedFramerate);

            Monitor.Exit(updateLock);
        }
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        if (KeepAlive)
            StartProcess();
    }

    public double GetFramerate(int processId)
    {
        try
        {
            var appE = OSD.GetAppEntries()
                .Where(x => (x.Flags & AppFlags.MASK) != AppFlags.None).FirstOrDefault(a => a.ProcessId == processId);
            if (appE is null)
                return 0.0d;

            return (double)appE.StatFrameTimeBufFramerate / 10;
        }
        catch (InvalidDataException)
        {
        }
        catch (FileNotFoundException)
        {
        }

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

    private bool SetTargetFPS(int Limit)
    {
        if (!IsRunning())
            return false;

        try
        {
            if (SetProfileProperty("FramerateLimit", Limit))
            {
                SaveProfile();
                UpdateSettings();
                UpdateProfiles();

                return true;
            }
        }
        catch
        {
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
        if (!IsRunning())
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

        if (IsRunning())
            KillProcess();

        return base.StartProcess();
    }

    public override bool StopProcess()
    {
        if (IsStarting)
            return false;
        if (!IsInstalled)
            return false;
        if (!IsRunning())
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