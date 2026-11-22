using HandheldCompanion;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Helpers;

/// <summary>
/// Freezes the selected game when the device sleeps and thaws it on wake (refocus only if it was in front).
/// Primary trigger is a display-off that leads to sleep; SystemPending is the fallback if the game is not already frozen.
/// On Modern Standby, Quick Tools Sleep turns the display off instead of SetSuspendState (which becomes S4 there).
/// </summary>
public static class QuickSleep
{
    // the game frozen by the current request, null when nothing is frozen
    private static ProcessEx? frozen;
    // a game left running because of its anti-cheat, still brought back in front after wake
    private static ProcessEx? refocusOnly;
    // the game window to bring back after wake, zero when the game was not in front
    private static nint gameWindow;
    // 1 when the frozen game was in front, or the first window behind Quick Tools
    private static int bringToFront;
    private static int busy;

    // display state watcher, Modern Standby only
    // watcherLock guards the watcher state used by the callback, updateLock serializes register and unregister.
    // Register and unregister run outside watcherLock, so they never wait on a callback that waits on them.
    private static readonly object watcherLock = new();
    private static readonly object updateLock = new();
    private static readonly bool modernStandby = !PowerActionsHelper.IsSleepAvailable();
    private static IntPtr registration = IntPtr.Zero;
    private static bool watching;
    private static int lastDisplayState = -1;
    // tick of the last display off requested by Run, so that request always counts as a sleep
    private static long requestedTick = long.MinValue;
    private static bool started;

    static QuickSleep()
    {
        SystemManager.SystemStatusChanged += SystemManager_SystemStatusChanged;
    }

    /// <summary>
    /// Starts following the SuspendOnSleep / SleepHibernate settings. Restores any Windows
    /// power settings left over from a previous sleep before thawing a game.
    /// </summary>
    public static void Start()
    {
        if (started)
            return;
        started = true;

        SleepPowerPolicy.RestoreIfNeeded();
        if (ManagerFactory.settingsManager.GetBoolean("SleepHibernate"))
            SleepPowerPolicy.SetHibernateToggle(true);

        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        ManagerFactory.processManager.ProcessStopped += ProcessManager_ProcessStopped;

        // Modern Standby: the watcher is the primary freeze path, including profile On when global is Off.
        UpdateWatcher(modernStandby);

        // a game already in front when HC starts raises no foreground change
        Task.Run(WarmForeground);
    }

    public static void Stop()
    {
        if (!started)
            return;
        started = false;

        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
        ManagerFactory.processManager.ProcessStopped -= ProcessManager_ProcessStopped;
        UpdateWatcher(false);
    }

    private static bool HasPending()
    {
        return Volatile.Read(ref frozen) is not null || Volatile.Read(ref refocusOnly) is not null;
    }

    private static void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
    {
        if (name is not ("SuspendOnSleep" or "SleepHibernate"))
            return;

        if (name == "SleepHibernate")
        {
            bool enabled = Convert.ToBoolean(value);
            SleepPowerPolicy.SetHibernateToggle(enabled);
            if (!enabled)
                SleepPowerPolicy.RestoreIfNeeded();
        }

        UpdateWatcher(modernStandby);
        Task.Run(WarmForeground);
    }

    public static async Task Run()
    {
        if (Interlocked.Exchange(ref busy, 1) == 1)
        {
            LogManager.LogInformation("[QuickSleep] Ignored, a sleep request is already running.");
            return;
        }

        try
        {
            SleepPowerPolicy.ApplyForSleep();

            if (modernStandby)
            {
                EnterStandby();
                return;
            }

            FreezeNow(PrepareFreeze());

            bool slept = false;
            try
            {
                LogManager.LogInformation("[QuickSleep] Requesting sleep.");
                slept = await Task.Run(() => PowerActionsHelper.Sleep(force: false)).ConfigureAwait(false);
                LogManager.LogInformation("[QuickSleep] Sleep request returned {0}.", slept);
            }
            catch (Exception ex)
            {
                LogManager.LogError("[QuickSleep] Sleep request failed: {0}", ex.Message);
            }

            // Sleep() returns after wake (or if sleep never started). Always restore and thaw:
            // a missed PowerModes.Resume must not leave Windows settings changed or the game frozen.
            SleepPowerPolicy.RestoreIfNeeded();
            await Thaw(refocus: slept).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref busy, 0);
        }
    }

    /// <summary>
    /// Modern Standby path: turn the display off and let Windows enter standby. The display watcher freezes the game
    /// once the display is off and thaws it when the display is back on.
    /// </summary>
    private static void EnterStandby()
    {
        // the setting may have just been turned on, make sure the watcher runs
        UpdateWatcher(true);

        lock (watcherLock)
        {
            if (!watching)
                LogManager.LogWarning("[QuickSleep] Display watcher is not running, the game will not be frozen.");
        }

        Interlocked.Exchange(ref requestedTick, Environment.TickCount64);

        LogManager.LogInformation("[QuickSleep] No S3 sleep on this device, turning the display off for Modern Standby.");
        if (!PowerActionsHelper.TurnDisplayOff())
        {
            Interlocked.Exchange(ref requestedTick, long.MinValue);
            SleepPowerPolicy.RestoreIfNeeded();
            LogManager.LogWarning("[QuickSleep] Could not turn the display off.");
        }
    }

    // ---------- display state watcher ----------
    private static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    private const uint DEVICE_NOTIFY_CALLBACK = 2;
    private const uint PBT_POWERSETTINGCHANGE = 0x8013;

    // a display off within this time after Run asked for it is the Quick Sleep request
    private static readonly TimeSpan RequestWindow = TimeSpan.FromSeconds(10);
    // idle time this close to the display timeout counts as the timeout
    private static readonly TimeSpan IdleMargin = TimeSpan.FromSeconds(5);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint DisplayCallback(IntPtr context, uint type, IntPtr setting);

    // keeps the callback alive while native code holds it
    private static readonly DisplayCallback displayCallback = OnDisplayCallback;

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
    {
        public DisplayCallback Callback;
        public IntPtr Context;
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerSettingRegisterNotification(ref Guid settingGuid, uint flags, ref DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient, out IntPtr registrationHandle);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSettingUnregisterNotification(IntPtr registrationHandle);

    private static void UpdateWatcher(bool enabled)
    {
        // S3 capture is SystemPending only
        if (!modernStandby)
        {
            if (!enabled)
            {
                SleepPowerPolicy.RestoreIfNeeded();
                if (HasPending())
                    _ = Thaw(refocus: false);
            }
            return;
        }

        lock (updateLock)
        {
            if (enabled && registration == IntPtr.Zero)
            {
                lock (watcherLock)
                {
                    lastDisplayState = -1;
                    watching = true;
                }

                Guid guid = GUID_CONSOLE_DISPLAY_STATE;
                DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient = new() { Callback = displayCallback, Context = IntPtr.Zero };
                uint result = PowerSettingRegisterNotification(ref guid, DEVICE_NOTIFY_CALLBACK, ref recipient, out IntPtr handle);
                if (result != 0)
                {
                    lock (watcherLock)
                        watching = false;
                    LogManager.LogWarning("[QuickSleep] Could not watch the display state, error {0}.", result);
                    return;
                }

                registration = handle;
                LogManager.LogInformation("[QuickSleep] Display watcher started.");
            }
            else if (!enabled && registration != IntPtr.Zero)
            {
                lock (watcherLock)
                    watching = false;

                PowerSettingUnregisterNotification(registration);
                registration = IntPtr.Zero;
                LogManager.LogInformation("[QuickSleep] Display watcher stopped.");
            }
        }

        // a game frozen just before the setting was turned off must not stay frozen
        if (!enabled)
        {
            SleepPowerPolicy.RestoreIfNeeded();
            if (HasPending())
                _ = Thaw(refocus: false);
        }
    }

    private static uint OnDisplayCallback(IntPtr context, uint type, IntPtr setting)
    {
        if (type != PBT_POWERSETTINGCHANGE || setting == IntPtr.Zero)
            return 0;

        try
        {
            // POWERBROADCAST_SETTING: Guid (16), DataLength (4), Data
            int state = Marshal.ReadInt32(setting, 20);

            // dimmed is not an off or on transition
            if (state == 2)
                return 0;

            lock (watcherLock)
            {
                // Windows reports the current state right after registering, an on there finds nothing to thaw
                if (!watching || state == lastDisplayState)
                    return 0;

                lastDisplayState = state;

                if (state == 0)
                {
                    // the work stays on this thread and is fast: standby may follow within a second
                    OnDisplayOff();
                    return 0;
                }
            }

            if (state == 1)
            {
                SleepPowerPolicy.RestoreIfNeeded();
                if (HasPending())
                {
                    LogManager.LogInformation("[QuickSleep] Display on, thawing.");
                    _ = Thaw(refocus: true);
                }
            }
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[QuickSleep] Display state handling failed: {0}", ex.Message);
        }

        return 0;
    }

    private static void OnDisplayOff()
    {
        // long.MinValue means no request: subtracting it would overflow into a negative and always count as requested
        long tick = Interlocked.Exchange(ref requestedTick, long.MinValue);
        bool requested = tick != long.MinValue && Environment.TickCount64 - tick <= (long)RequestWindow.TotalMilliseconds;
        if (requested)
            LogManager.LogInformation("[QuickSleep] Display off: requested by Quick Sleep {0:0.0}s ago, freeze True.", (Environment.TickCount64 - tick) / 1000.0);
        else if (!ShouldFreezeOnDisplayOff())
            return;

        SleepPowerPolicy.ApplyForSleep();

        if (HasPending())
            return;

        // freeze only once the display is off: a game frozen while it still owns the screen
        // keeps the desktop compositor busy for the whole standby
        FreezeTarget? target = PrepareFreeze();
        if (target is null)
        {
            LogManager.LogInformation("[QuickSleep] Display off, no game to freeze.");
            return;
        }

        FreezeNow(target);
    }

    /// <summary>
    /// Tells a display off that goes with a sleep (power button, Start menu sleep, or an idle timeout that also
    /// sleeps) apart from a display timeout that leaves the device awake.
    /// </summary>
    private static bool ShouldFreezeOnDisplayOff()
    {
        bool ac = PowerActionsHelper.IsOnAC();
        PowerIndexType index = ac ? PowerIndexType.AC : PowerIndexType.DC;

        uint displayTimeout = 0, sleepTimeout = 0, buttonAction = uint.MaxValue;
        if (PowerScheme.GetActiveScheme(out Guid scheme))
        {
            PowerScheme.GetValue(index, scheme, SUB_VIDEO, VIDEOIDLE, out displayTimeout);
            PowerScheme.GetValue(index, scheme, PowerSubGroup.SUB_SLEEP, STANDBYIDLE, out sleepTimeout);
            if (!PowerScheme.GetValue(index, scheme, SUB_BUTTONS, PBUTTONACTION, out buttonAction))
                buttonAction = uint.MaxValue;
        }

        TimeSpan idle = GetIdleTime();
        bool displayRequired = IsDisplayRequired();

        // the display cannot time out before the timeout, or while something holds it on
        bool manual = displayTimeout == 0
            || idle < TimeSpan.FromSeconds(displayTimeout) - IdleMargin
            || displayRequired;

        bool freeze;
        string reason;
        if (manual)
        {
            // a power button set to only turn the display off leaves the device awake
            freeze = buttonAction != BUTTON_ACTION_DISPLAY_OFF;
            reason = freeze ? "manual display off" : "manual display off, power button only turns the display off";
        }
        else
        {
            freeze = sleepTimeout != 0 && sleepTimeout <= displayTimeout;
            reason = freeze ? "display timeout, sleep follows" : "display timeout, no sleep follows";
        }

        LogManager.LogInformation("[QuickSleep] Display off: {0}, freeze {1}. Source {2}, idle {3:0.0}s, display timeout {4}s, sleep timeout {5}s, display required {6}, power button action {7}.",
            reason, freeze, ac ? "AC" : "DC", idle.TotalSeconds, displayTimeout, sleepTimeout, displayRequired, buttonAction);

        return freeze;
    }

    // ---------- power settings (used to tell a sleep display-off from a plain display timeout) ----------
    private static readonly Guid SUB_VIDEO = new("7516B95F-F776-4464-8C53-06167F40CC99");
    private static readonly Guid VIDEOIDLE = new("3C0BC021-C8A8-4E07-A973-6B14CBCB2B7E");
    private static readonly Guid STANDBYIDLE = new("29F6C1DB-86DA-48C5-9FDB-F2B67B1F44DA");
    private static readonly Guid SUB_BUTTONS = new("4F971E89-EEBD-4455-A8DE-9E59040E7347");
    private static readonly Guid PBUTTONACTION = new("7648EFA3-DD9C-4E3E-B566-50F929386280");
    private const uint BUTTON_ACTION_DISPLAY_OFF = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    private static TimeSpan GetIdleTime()
    {
        LASTINPUTINFO info = new() { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        // both are 32-bit tick counts, the unsigned difference survives the wrap
        return TimeSpan.FromMilliseconds(unchecked(GetTickCount() - info.dwTime));
    }

    private const int SystemExecutionState = 16;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(int informationLevel, IntPtr inputBuffer, uint inputBufferLength, out uint outputBuffer, uint outputBufferLength);

    private static bool IsDisplayRequired()
    {
        return CallNtPowerInformation(SystemExecutionState, IntPtr.Zero, 0, out uint state, sizeof(uint)) == 0
            && (state & ES_DISPLAY_REQUIRED) != 0;
    }

    private static async void SystemManager_SystemStatusChanged(SystemManager.SystemStatus status, SystemManager.SystemStatus prevStatus)
    {
        if (status == SystemManager.SystemStatus.SystemPending && prevStatus == SystemManager.SystemStatus.SystemReady)
        {
            SleepPowerPolicy.ApplyForSleep();
            if (!HasPending())
                FreezeNow(PrepareFreeze());
            return;
        }

        if (status == SystemManager.SystemStatus.SystemReady && prevStatus == SystemManager.SystemStatus.SystemPending)
        {
            SleepPowerPolicy.RestoreIfNeeded();
            await Thaw(refocus: true).ConfigureAwait(false);
        }
    }

    private sealed record FreezeTarget(ProcessEx Game, nint Window, List<Process> Children, string? AntiCheat, bool WasInFront, string Reason);

    /// <summary>
    /// Finds the game to freeze and its child processes, without freezing anything yet.
    /// A game already in front, or the first window behind Quick Tools, is taken as-is.
    /// Otherwise the front-most strongest window wins: a non-default profile, then a known platform, then one that covers its monitor.
    /// </summary>
    private static FreezeTarget? PrepareFreeze()
    {
        try
        {
            // look at the windows before the freeze, the game cannot answer once frozen
            (nint window, string source, bool wasInFront) = FindTargetWindow();
            if (window == 0)
            {
                LogManager.LogInformation("[QuickSleep] No window to freeze ({0}).", source);
                return null;
            }

            ProcessEx? game = ResolveProcess(window, log: true);
            if (game is null)
                return null;

            if (game.Process is null || game.Process.HasExited || game.IsSuspended)
                return null;

            string? notGame = GetNotGameReason(game, window);
            if (notGame is not null)
            {
                LogManager.LogInformation("[QuickSleep] {0} (pid {1}, {2}) is not a game: {3}, not freezing.", game.Executable, game.ProcessId, source, notGame);
                return null;
            }

            LogManager.LogInformation("[QuickSleep] {0} (pid {1}) selected by {2}, wasInFront {3}.", game.Executable, game.ProcessId, source, wasInFront);

            List<SnapshotEntry> snapshot = TakeProcessSnapshot();
            string? antiCheat = IsAntiCheatProtected(game, snapshot);
            List<Process> children = GetChildProcesses(game.ProcessId, snapshot);
            return new FreezeTarget(game, window, children, antiCheat, wasInFront, source);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[QuickSleep] Could not find a game to freeze: {0}", ex.Message);
            return null;
        }
    }

    // platform helpers missing from HC's launcher list (ProcessEx.IsLauncher)
    private static readonly HashSet<string> PlatformHelperExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "gameoverlayui.exe", "gameoverlayui64.exe", "steamerrorreporter.exe",
        "GalaxyClient Helper.exe",
        "RiotClientServices.exe", "RiotClientUx.exe", "RiotClientUxRender.exe", "Riot Client.exe",
        "EOSOverlayRenderer-Win64-Shipping.exe",
    };

    /// <summary>
    /// Why the process in front is never a game, or null. A platform's own client and helpers get that platform
    /// from PlatformManager (Steam lists steamwebhelper.exe among its modules), so the platform alone does not
    /// make a game.
    /// </summary>
    private static string? GetNotGameReason(ProcessEx process, nint window)
    {
        if (process.ProcessId == Environment.ProcessId)
            return "Handheld Companion";

        string exe = process.Executable ?? string.Empty;

        switch (ProcessManager.GetFilter(exe, process.Path ?? string.Empty, string.Empty, GetWindowClass(window)))
        {
            case ProcessEx.ProcessFilter.HandheldCompanion:
                return "Handheld Companion";
            case ProcessEx.ProcessFilter.Restricted:
                return "system or tool process";
            case ProcessEx.ProcessFilter.Desktop:
                return "desktop";
        }

        if (process.IsLauncher)
            return "game platform client";

        if (PlatformHelperExecutables.Contains(exe))
            return "game platform helper";

        // the platform's own executable, as GameLib reports it
        List<IPlatform>? platforms = PlatformManager.GamingPlatforms;
        if (platforms is not null)
        {
            foreach (IPlatform platform in platforms)
            {
                try
                {
                    string own = platform.ExecutableName;
                    if (!string.IsNullOrEmpty(own) && exe.Equals(own, StringComparison.OrdinalIgnoreCase))
                        return $"{platform.Name} client";
                }
                catch
                {
                    // platform not installed
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The ProcessEx that owns the window: the one HC already tracks, or a local one when HC does not track it.
    /// </summary>
    private static ProcessEx? ResolveProcess(nint window, bool log)
    {
        int processId = GetWindowProcessId(window);

        // a UWP frame belongs to ApplicationFrameHost, the game is the hosted process
        if (GetWindowClass(window) == "ApplicationFrameWindow")
        {
            var hosted = new ProcessUtils.FindHostedProcess(window)._realProcess;
            if (hosted is not null)
                processId = (int)hosted.ProcessId;
        }

        if (processId == 0 || processId == Environment.ProcessId)
            return null;

        ProcessEx? tracked = ProcessManager.GetProcess(processId);
        if (tracked is not null)
            return tracked;

        try
        {
            Process process = Process.GetProcessById(processId);
            if (process.HasExited)
                return null;

            string? path = ProcessUtils.GetPathToApp(processId);
            if (string.IsNullOrEmpty(path))
            {
                if (log)
                    LogManager.LogInformation("[QuickSleep] Could not read the path of pid {0}, not freezing.", processId);
                return null;
            }

            string exe = Path.GetFileName(path);
            ProcessEx local = new(process, path, exe, ProcessManager.GetFilter(exe, path));
            local.Platform = PlatformManager.GetPlatform(local);

            if (log)
                LogManager.LogInformation("[QuickSleep] {0} (pid {1}) is not tracked by HC, looked it up directly.", exe, processId);
            return local;
        }
        catch (Exception ex)
        {
            if (log)
                LogManager.LogInformation("[QuickSleep] Could not look up pid {0}: {1}", processId, ex.Message);
            return null;
        }
    }

    private static void FreezeNow(FreezeTarget? target)
    {
        if (target is null)
            return;

        ProcessEx game = target.Game;
        List<Process> suspendedChildren = new();
        try
        {
            if (game.Process is null || game.Process.HasExited)
                return;

            if (!WantsSuspend(game))
            {
                LogManager.LogInformation("[QuickSleep] {0} is not set to suspend on sleep, not freezing.", game.Executable);
                return;
            }

            bool explicitOn = HasExplicitSuspendOnSleep(game);
            if (target.AntiCheat is not null && !explicitOn)
            {
                // frozen threads can look like tampering to an anti-cheat and risk a ban, so Windows handles the sleep
                LogManager.LogInformation("[QuickSleep] Anti-cheat detected ({0}), not freezing {1}.", target.AntiCheat, game.Executable);
                if (target.WasInFront && target.Window != 0)
                {
                    Interlocked.Exchange(ref bringToFront, 1);
                    Interlocked.Exchange(ref gameWindow, target.Window);
                    Interlocked.Exchange(ref refocusOnly, game);
                }
                return;
            }

            if (target.AntiCheat is not null)
                LogManager.LogWarning("[QuickSleep] Anti-cheat detected ({0}), freezing {1} because the profile enables suspend. This can risk a ban.", target.AntiCheat, game.Executable);

            if (game.IsSuspended)
                return;

            foreach (Process child in target.Children.Where(p => !p.HasExited))
            {
                if (ProcessUtils.NtSuspendProcess(child.Handle) == 0)
                    suspendedChildren.Add(child);
            }

            if (ProcessUtils.NtSuspendProcess(game.Handle) != 0)
            {
                foreach (Process child in suspendedChildren.Where(p => !p.HasExited))
                    ProcessUtils.NtResumeProcess(child.Handle);
                LogManager.LogWarning("[QuickSleep] Could not freeze {0}. Sleep continues.", game.Executable);
                return;
            }

            // the window first, Thaw takes the game and then the window
            Interlocked.Exchange(ref bringToFront, target.WasInFront ? 1 : 0);
            Interlocked.Exchange(ref gameWindow, target.Window);
            Interlocked.Exchange(ref frozen, game);
            LogManager.LogInformation("[QuickSleep] Froze {0} and {1} child process(es).", game.Executable, suspendedChildren.Count);
        }
        catch (Exception ex)
        {
            foreach (Process child in suspendedChildren.Where(p => !p.HasExited))
            {
                try { ProcessUtils.NtResumeProcess(child.Handle); }
                catch { }
            }
            LogManager.LogWarning("[QuickSleep] Could not freeze {0}: {1}", game.Executable, ex.Message);
        }
    }

    private static async Task Thaw(bool refocus)
    {
        // a game behind another window stays where it was
        bool focus = refocus && Interlocked.Exchange(ref bringToFront, 0) == 1;

        // take ownership once, so the wake handlers never thaw the same game twice
        ProcessEx? game = Interlocked.Exchange(ref frozen, null);
        if (game is null)
        {
            // a game that was left running only needs the refocus
            game = Interlocked.Exchange(ref refocusOnly, null);
            if (game is null)
                return;

            nint window = Interlocked.Exchange(ref gameWindow, 0);
            if (focus)
                await Refocus(game, window).ConfigureAwait(false);
            return;
        }

        nint hWnd = Interlocked.Exchange(ref gameWindow, 0);

        try
        {
            if (await ProcessManager.ResumeProcess(game, false).ConfigureAwait(false))
                LogManager.LogInformation("[QuickSleep] Thawed {0}.", game.Executable);
            else
                LogManager.LogWarning("[QuickSleep] Could not thaw {0}.", game.Executable);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[QuickSleep] Could not thaw {0}: {1}", game.Executable, ex.Message);
        }

        if (focus)
            await Refocus(game, hWnd).ConfigureAwait(false);
    }

    // ---------- child processes ----------
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private readonly record struct SnapshotEntry(int ProcessId, int ParentId, string Executable);

    /// <summary>
    /// All running processes from a kernel process snapshot. Unlike a WMI query it takes milliseconds,
    /// so it can run after the display is off without running into standby.
    /// </summary>
    private static List<SnapshotEntry> TakeProcessSnapshot()
    {
        List<SnapshotEntry> entries = new();

        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
            return entries;

        try
        {
            PROCESSENTRY32 entry = new() { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            for (bool ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
                entries.Add(new SnapshotEntry((int)entry.th32ProcessID, (int)entry.th32ParentProcessID, entry.szExeFile ?? string.Empty));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return entries;
    }

    /// <summary>
    /// Direct child processes of the given process, from a snapshot.
    /// </summary>
    private static List<Process> GetChildProcesses(int parentId, List<SnapshotEntry> snapshot)
    {
        List<Process> children = new();
        foreach (SnapshotEntry entry in snapshot)
        {
            if (entry.ParentId != parentId || entry.ProcessId == parentId)
                continue;

            try
            {
                children.Add(Process.GetProcessById(entry.ProcessId));
            }
            catch
            {
                // exited in the meantime
            }
        }

        return children;
    }

    // ---------- anti-cheat ----------
    // Freezing a protected game can look like tampering and risk a ban, so those games are left to Windows.

    // files and folders shipped in the game install, checked by exact path, never by listing a folder
    private static readonly (string Path, string Name)[] AntiCheatMarkers =
    [
        ("EasyAntiCheat", "Easy Anti-Cheat"),
        ("start_protected_game.exe", "Easy Anti-Cheat"),
        ("EasyAntiCheat_x64.dll", "Easy Anti-Cheat"),
        ("BattlEye", "BattlEye"),
        ("EAAntiCheat", "EA Javelin"),
        (Path.Combine("__Installer", "EAAntiCheat"), "EA Javelin"),
        ("GameGuard", "nProtect GameGuard"),
        (Path.Combine("pb", "pbcl.dll"), "PunkBuster"),
    ];

    // processes that only run while a protected game runs, so seeing one anywhere means the game in front is protected
    private static readonly Dictionary<string, string> SessionAntiCheatProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EasyAntiCheat.exe"] = "Easy Anti-Cheat",
        ["EasyAntiCheat_EOS.exe"] = "Easy Anti-Cheat",
        ["BEService.exe"] = "BattlEye",
        ["BEService_x64.exe"] = "BattlEye",
        ["EAAntiCheat.GameService.exe"] = "EA Javelin",
        ["SGuard64.exe"] = "Anti-Cheat Expert",
        ["SGuardSvc64.exe"] = "Anti-Cheat Expert",
        ["GameMon.des"] = "nProtect GameGuard",
        ["GameMon64.des"] = "nProtect GameGuard",
    };

    // folder names that end the walk up from the game: a library root is not part of one game
    private static readonly HashSet<string> LibraryRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "common", "steamapps", "SteamLibrary", "Steam", "Program Files", "Program Files (x86)", "Epic Games",
        "GOG Games", "GOG Galaxy", "XboxGames", "EA Games", "Ubisoft Game Launcher", "games", "Riot Games", "HoYoPlay",
    };

    // the exe folder and up to three parents, Unreal games keep the exe in Binaries\Win64 below the game root
    private const int MarkerDepth = 4;

    // per process id: the anti-cheat named by files in the game install, or null
    private static readonly ConcurrentDictionary<int, (string Path, string? AntiCheat)> markerCache = new();

    private static void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessEx.ProcessFilter filter)
    {
        // read the game install while the game is in front, so the display off only reads the cache
        if (processEx is null || processEx.ProcessId == 0 || processEx.ProcessId == Environment.ProcessId)
            return;

        if (IsMarkerCached(processEx))
            return;

        Task.Run(() => GetMarkerAntiCheat(processEx));
    }

    /// <summary>
    /// Reads the install of the game in front ahead of time, the same way the display off picks it.
    /// </summary>
    private static void WarmForeground()
    {
        try
        {
            (nint window, _, _) = FindTargetWindow();
            if (window == 0)
                return;

            ProcessEx? game = ResolveProcess(window, log: false);
            if (game is not null && GetNotGameReason(game, window) is null)
                GetMarkerAntiCheat(game);
        }
        catch
        {
            // the display off computes it if needed
        }
    }

    private static bool IsMarkerCached(ProcessEx game)
    {
        return markerCache.TryGetValue(game.ProcessId, out var cached)
            && string.Equals(cached.Path, game.Path ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static void ProcessManager_ProcessStopped(ProcessEx processEx)
    {
        markerCache.TryRemove(processEx.ProcessId, out _);
    }

    private static string? GetMarkerAntiCheat(ProcessEx game)
    {
        string path = game.Path ?? string.Empty;
        if (markerCache.TryGetValue(game.ProcessId, out var cached) && string.Equals(cached.Path, path, StringComparison.OrdinalIgnoreCase))
            return cached.AntiCheat;

        string? antiCheat = FindAntiCheatMarker(path);
        markerCache[game.ProcessId] = (path, antiCheat);
        return antiCheat;
    }

    private static string? FindAntiCheatMarker(string exePath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(exePath);
            for (int depth = 0; depth < MarkerDepth && !string.IsNullOrEmpty(dir); depth++)
            {
                DirectoryInfo info = new(dir);
                // stop at a drive root or a library folder
                if (info.Parent is null || LibraryRoots.Contains(info.Name))
                    break;

                foreach ((string marker, string name) in AntiCheatMarkers)
                {
                    string path = Path.Combine(dir, marker);
                    if (Directory.Exists(path) || File.Exists(path))
                        return $"{name}, {path}";
                }

                // the game root sits right under a library folder, nothing above it belongs to the game
                if (LibraryRoots.Contains(info.Parent.Name))
                    break;

                dir = info.Parent.FullName;
            }
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[QuickSleep] Could not look for anti-cheat files of {0}: {1}", exePath, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Returns the anti-cheat protecting the game, or null. Cheap enough for the display off: the install files
    /// are read once per process and cached, processes come from the snapshot, and a service is only queried
    /// when the game itself points at that anti-cheat.
    /// </summary>
    private static string? IsAntiCheatProtected(ProcessEx game, List<SnapshotEntry> snapshot)
    {
        // (a) files and folders in the game install
        string? marker = GetMarkerAntiCheat(game);
        if (marker is not null)
            return marker;

        // (b) anti-cheat processes that only run with a protected game
        foreach (SnapshotEntry entry in snapshot)
        {
            if (SessionAntiCheatProcesses.TryGetValue(entry.Executable, out string? name))
                return $"{name}, {entry.Executable} running";
        }

        // (c) always-on anti-cheat services, only when the game belongs to them
        string path = game.Path ?? string.Empty;
        string exe = game.Executable ?? string.Empty;

        if (game.Platform == GamePlatform.RiotGames && IsServiceRunning("vgc"))
            return "Riot Vanguard, vgc service";

        if (path.Contains("Call of Duty", StringComparison.OrdinalIgnoreCase) && IsServiceRunning("atvi-brynhildr"))
            return "Ricochet, atvi-brynhildr service";

        if ((exe.Equals("cs2.exe", StringComparison.OrdinalIgnoreCase) || exe.Equals("csgo.exe", StringComparison.OrdinalIgnoreCase)) && IsServiceRunning("FACEIT"))
            return "FACEIT, FACEIT service";

        if ((path.Contains("HoYoPlay", StringComparison.OrdinalIgnoreCase) || path.Contains("miHoYo", StringComparison.OrdinalIgnoreCase) || path.Contains("Genshin Impact", StringComparison.OrdinalIgnoreCase))
            && (IsServiceRunning("mhyprot2") || IsServiceRunning("mhyprot3")))
            return "HoYoverse mhyprot, mhyprot service";

        return null;
    }

    private static bool IsServiceRunning(string name)
    {
        try
        {
            using ServiceController service = new(name);
            return service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            // not installed
            return false;
        }
    }

    // ---------- game window ----------
    private const uint MONITOR_DEFAULTTONULL = 0;
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    /// <summary>
    /// True when the window covers the whole monitor it is on: exclusive or borderless fullscreen.
    /// A maximized window with a title bar is not fullscreen, even when the taskbar hides.
    /// </summary>
    private static bool CoversMonitor(nint hWnd)
    {
        if (hWnd == 0 || hWnd == GetShellWindow() || hWnd == WinAPI.GetDesktopWindow())
            return false;

        string name = GetWindowClass(hWnd);
        if (name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return false;

        if (IsZoomed(hWnd) && (WinAPI.GetWindowLong(hWnd, GWL_STYLE) & WS_CAPTION) == WS_CAPTION)
            return false;

        IntPtr monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONULL);
        if (monitor == IntPtr.Zero)
            return false;

        MONITORINFO info = new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(hWnd, out RECT rect))
            return false;

        return rect.Left <= info.rcMonitor.Left && rect.Top <= info.rcMonitor.Top
            && rect.Right >= info.rcMonitor.Right && rect.Bottom >= info.rcMonitor.Bottom;
    }

    private const int DWMWA_CLOAKED = 14;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);

    private static bool IsCloaked(nint hWnd)
    {
        return DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
    }

    private static string GetWindowClass(nint hWnd)
    {
        StringBuilder className = new(256);
        GetClassName(hWnd, className, className.Capacity);
        return className.ToString();
    }

    /// <summary>
    /// The game to freeze. A game already in front, or the first eligible window behind Quick Tools, is taken directly.
    /// Otherwise the front-most strongest window: a non-default profile, then a known platform, then one that covers its monitor.
    /// </summary>
    private static (nint Window, string Reason, bool WasInFront) FindTargetWindow()
    {
        List<nint> windows = EnumerateTopLevel();
        nint foreground = WinAPI.GetForegroundWindow();
        nint quickTools = GetQuickToolsHandle();
        bool behindQuickTools = foreground != 0 && foreground == quickTools;

        nint direct = 0;
        if (!behindQuickTools && IsEligibleWindow(foreground))
            direct = foreground;
        else if (behindQuickTools)
        {
            foreach (nint hWnd in windows)
            {
                if (!IsEligibleWindow(hWnd))
                    continue;

                direct = hWnd;
                break;
            }
        }

        if (direct != 0 && TryRank(direct, out _, out string directReason))
            return (direct, directReason, true);

        nint best = 0;
        int bestRank = 0;
        string bestReason = "";
        foreach (nint hWnd in windows)
        {
            if (!IsEligibleWindow(hWnd) || !TryRank(hWnd, out int rank, out string reason) || rank <= bestRank)
                continue;

            best = hWnd;
            bestRank = rank;
            bestReason = reason;
        }

        if (best == 0)
            return (0, behindQuickTools ? "nothing behind Quick Tools" : "no game window", false);

        return (best, bestReason, best == direct);
    }

    private static List<nint> EnumerateTopLevel()
    {
        List<nint> windows = new();
        WinAPI.EnumWindows((hWnd, _) =>
        {
            windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    // Visible top-level windows, including minimized ones. Cloaked, tool, and HC windows are not candidates.
    private static bool IsEligibleWindow(nint hWnd)
    {
        if (hWnd == 0 || hWnd == GetShellWindow() || hWnd == WinAPI.GetDesktopWindow())
            return false;

        if (IsCloaked(hWnd) || GetWindowProcessId(hWnd) == Environment.ProcessId)
            return false;

        if ((WinAPI.GetWindowLong(hWnd, WinAPI.GWL_EXSTYLE) & WinAPI.WS_EX_TOOLWINDOW) != 0)
            return false;

        if (!ProcessUtils.IsWindowVisible(hWnd) && !ProcessUtils.IsIconic(hWnd))
            return false;

        string name = GetWindowClass(hWnd);
        return name is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
    }

    // 3 profile, 2 platform, 1 covers the monitor. Zero when the window is not a game.
    private static bool TryRank(nint window, out int rank, out string reason)
    {
        rank = 0;
        reason = "";

        ProcessEx? game = ResolveProcess(window, log: false);
        if (game is null || game.Process is null || game.Process.HasExited || game.IsSuspended)
            return false;

        if (GetNotGameReason(game, window) is not null)
            return false;

        if (HasNonDefaultProfile(game))
        {
            rank = 3;
            reason = "profile";
            return true;
        }

        if (game.Platform != GamePlatform.Generic)
        {
            rank = 2;
            reason = "platform";
            return true;
        }

        if (CoversMonitor(window))
        {
            rank = 1;
            reason = "covers-monitor";
            return true;
        }

        return false;
    }

    private static bool HasNonDefaultProfile(ProcessEx game)
    {
        if (string.IsNullOrEmpty(game.Path))
            return false;

        try
        {
            return !ManagerFactory.profileManager.GetProfileFromPath(game.Path, true).Default;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Per-profile SuspendOnSleep: Inherit uses the Device setting; On/Off override it.
    /// </summary>
    public static bool WantsSuspend(ProcessEx game)
    {
        bool global = ManagerFactory.settingsManager.GetBoolean("SuspendOnSleep");
        if (string.IsNullOrEmpty(game.Path))
            return global;

        try
        {
            return ManagerFactory.profileManager.GetProfileFromPath(game.Path, true).SuspendOnSleep switch
            {
                SuspendOnSleepMode.On => true,
                SuspendOnSleepMode.Off => false,
                _ => global,
            };
        }
        catch
        {
            return global;
        }
    }

    /// <summary>
    /// True when the profile explicitly enables suspend (not inherit). Used for the anti-cheat override.
    /// </summary>
    public static bool HasExplicitSuspendOnSleep(ProcessEx game)
    {
        if (string.IsNullOrEmpty(game.Path))
            return false;

        try
        {
            return ManagerFactory.profileManager.GetProfileFromPath(game.Path, true).SuspendOnSleep == SuspendOnSleepMode.On;
        }
        catch
        {
            return false;
        }
    }

    private static async Task Refocus(ProcessEx game, nint hWnd)
    {
        if (hWnd == 0)
            return;

        if (game.Process is null || game.Process.HasExited || !WinAPI.IsWindow(hWnd))
        {
            LogManager.LogInformation("[QuickSleep] Skip refocus, {0} or its window is gone.", game.Executable);
            return;
        }

        // let the display and the game settle after wake
        await Task.Delay(500).ConfigureAwait(false);

        if (ProcessUtils.IsIconic(hWnd))
            ProcessUtils.ShowWindow(hWnd, (int)ProcessUtils.ShowWindowCommands.Restored);

        bool focused = ProcessUtils.SetForegroundWindow(hWnd);
        LogManager.LogInformation("[QuickSleep] Refocus {0} returned {1}.", game.Executable, focused);
    }

    private static int GetWindowProcessId(nint hWnd)
    {
        if (hWnd == 0)
            return 0;

        WinAPI.GetWindowThreadProcessId(hWnd, out int processId);
        return processId;
    }

    private static nint GetQuickToolsHandle()
    {
        try
        {
            return OverlayQuickTools.GetCurrent()?.GetHandle() ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
