using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Platforms;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using static ControllerCommon.WinAPI;
using static HandheldCompanion.Controls.ProcessEx;
using static HandheldCompanion.Managers.EnergyManager;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ProcessManager
{
    // process vars
    private static readonly Timer MonitorTimer;

    private static readonly ConcurrentDictionary<int, ProcessEx> Processes = new();

    private static ProcessEx foregroundProcess;
    private static ProcessEx previousProcess;

    private static readonly object updateLock = new();
    private static bool IsInitialized;

    public static readonly ProcessEx Empty = new()
    {
        Path = string.Empty,
        Executable = string.Empty,
        Platform = PlatformType.Windows,
        Filter = ProcessFilter.Ignored
    };

    static ProcessManager()
    {
        MonitorTimer = new Timer(2000);
        MonitorTimer.Elapsed += MonitorHelper;
    }

    public static void Start()
    {
        // start processes monitor
        MonitorTimer.Start();

        // hook: on window opened
        Automation.AddAutomationEventHandler(
            WindowPattern.WindowOpenedEvent,
            AutomationElement.RootElement,
            TreeScope.Children,
            OnWindowOpened);

        // hook: on window foreground and minimize
        WinEventWindows = OnWindowEvent;
        HookForeground = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, WinEventWindows,
            0, 0, WINEVENT_OUTOFCONTEXT);
        HookMinimize = SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZESTART, IntPtr.Zero,
            WinEventWindows, 0, 0, WINEVENT_OUTOFCONTEXT);

        // list all current windows
        EnumWindows(OnWindowDiscovered, 0);

        // get current foreground process
        var hWnd = GetforegroundWindow();
        OnWindowEvent(0, EVENT_SYSTEM_FOREGROUND, hWnd, 0, 0, 0, 0);

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "ProcessManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;

        // stop processes monitor
        MonitorTimer.Elapsed -= MonitorHelper;
        MonitorTimer.Stop();

        Automation.RemoveAllEventHandlers();

        UnhookWinEvent(HookForeground);
        UnhookWinEvent(HookMinimize);

        LogManager.LogInformation("{0} has stopped", "ProcessManager");
    }

    public static ProcessEx GetForegroundProcess()
    {
        return foregroundProcess;
    }

    public static ProcessEx GetLastSuspendedProcess()
    {
        return Processes.Values.LastOrDefault(item => item.IsSuspended());
    }

    public static ProcessEx GetProcess(int processId)
    {
        if (Processes.TryGetValue(processId, out var process))
            return process;
        return null;
    }

    public static List<ProcessEx> GetProcesses()
    {
        return Processes.Values.ToList();
    }

    public static List<ProcessEx> GetProcesses(string executable)
    {
        return Processes.Values.Where(a => a.Executable.Equals(executable, StringComparison.InvariantCultureIgnoreCase))
            .ToList();
    }

    private static void OnWindowOpened(object sender, AutomationEventArgs automationEventArgs)
    {
        try
        {
            if (sender is AutomationElement element)
            {
                var processInfo = new ProcessUtils.FindHostedProcess(element.Current.NativeWindowHandle)._realProcess;
                if (processInfo is null)
                    return;

                ProcessCreated((int)processInfo.ProcessId, element.Current.NativeWindowHandle);
            }
        }
        catch
        {
        }
    }

    private static bool OnWindowDiscovered(IntPtr hWnd, int lparam)
    {
        if (IsWindowVisible((int)hWnd))
        {
            var processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
            if (processInfo is null)
                return false;

            ProcessCreated((int)processInfo.ProcessId, (int)hWnd, true);
        }

        return true;
    }

    private static async void OnWindowEvent(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild,
        int dwEventThread, int dwmsEventTime)
    {
        var processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
        if (processInfo is null)
            return;

        try
        {
            var processId = (int)processInfo.ProcessId;

            // events such as EVENT_SYSTEM_FOREGROUND can be raised before OnWindowDiscovered
            // therefore we need to give it some breathing space (2500ms)
            byte attempts = 0;
            while (!Processes.ContainsKey(processId) && attempts < 10)
            {
                attempts++;
                await Task.Delay(250);
            }

            // create process if doesn't exist already
            var success = ProcessCreated(processId, (int)hWnd);
            if (!success)
                return;

            // pull process from running processes
            var process = Processes[processId];

            // save previous process (can be null)
            previousProcess = foregroundProcess;

            switch (iEvent)
            {
                case EVENT_SYSTEM_FOREGROUND:
                {
                    string WindowTitle = ProcessUtils.GetWindowTitle(hWnd);

                    // filter based on current process status
                    var filter = GetFilter(process.Executable, process.Path, WindowTitle);
                    switch (filter)
                    {
                        // skip foreground change
                        case ProcessFilter.Desktop:
                        case ProcessFilter.HandheldCompanion:
                            return;
                        // continue
                    }

                    // update foreground process
                    foregroundProcess = process;

                    // update main window handle
                    foregroundProcess.MainWindowHandle = hWnd;

                    LogManager.LogDebug("{0} executable {1} now has the foreground", foregroundProcess.Platform,
                        foregroundProcess.Executable);
                }
                    break;

                case EVENT_SYSTEM_MINIMIZESTART:
                {
                    // ignore if not foreground window
                    if (foregroundProcess is null || foregroundProcess.MainWindowHandle != hWnd)
                        return;

                    // update foreground process
                    foregroundProcess = Empty;

                    LogManager.LogDebug("{0} executable {1} was minimized or destroyed", previousProcess.Platform,
                        previousProcess.Executable);
                }
                    break;
            }

            // inform service
            PipeClient.SendMessage(new PipeClientProcess
                { executable = foregroundProcess.Executable, platform = foregroundProcess.Platform });

            // raise event
            ForegroundChanged?.Invoke(foregroundProcess, previousProcess);
        }
        catch
        {
            // process has too high elevation
        }
    }

    private static void MonitorHelper(object? sender, EventArgs e)
    {
        if (Monitor.TryEnter(updateLock))
        {
            Parallel.ForEach(Processes.Values,
                new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism },
                proc => { proc.Refresh(); });

            Monitor.Exit(updateLock);
        }
    }

    private static void ProcessHalted(object? sender, EventArgs e)
    {
        var processId = ((Process)sender).Id;

        if (Processes.TryGetValue(processId, out var process))
        {
            // stopped process can't have foreground
            if (foregroundProcess == process)
            {
                ForegroundChanged?.Invoke(Empty, foregroundProcess);
                foregroundProcess = null;
            }

            Processes.TryRemove(new KeyValuePair<int, ProcessEx>(processId, process));

            // raise event
            ProcessStopped?.Invoke(process);

            LogManager.LogDebug("Process halted: {0}", process.Title);

            process.Dispose();
        }
    }

    private static bool ProcessCreated(int ProcessID, int NativeWindowHandle = 0, bool OnStartup = false)
    {
        try
        {
            // process has exited on arrival
            var proc = Process.GetProcessById(ProcessID);
            if (proc.HasExited)
                return false;

            // hook into events
            proc.EnableRaisingEvents = true;

            if (!Processes.ContainsKey(proc.Id))
                // UI thread (synchronous)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // check process path
                    var path = ProcessUtils.GetPathToApp(proc.Id);
                    if (string.IsNullOrEmpty(path))
                        return false;

                    var exec = Path.GetFileName(path);
                    var hWnd = NativeWindowHandle != 0 ? NativeWindowHandle : proc.MainWindowHandle;

                    // get filter
                    var filter = GetFilter(exec, path);

                    var processEx = new ProcessEx(proc, path, exec, filter);
                    processEx.MainWindowHandle = hWnd;

                    // processEx.ChildProcessCreated += ChildProcessCreated;
                    Processes.TryAdd(processEx.GetProcessId(), processEx);
                    proc.Exited += ProcessHalted;

                    switch (processEx.Filter)
                    {
                        // skip foreground change
                        case ProcessFilter.Desktop:
                        case ProcessFilter.HandheldCompanion:
                            return false;
                        // continue
                    }

                    // raise event
                    ProcessStarted?.Invoke(processEx, OnStartup);

                    return true;
                });

            // process already exists
            return true;
        }
        catch
        {
            // process has too high elevation
        }

        return false;
    }

    private static void ChildProcessCreated(ProcessEx parent, int pId)
    {
        var mode = parent.GetEfficiencyMode();
        if (mode == EfficiencyMode.Default)
            return;

        ToggleEfficiencyMode(pId, mode, parent);
    }

    private static ProcessFilter GetFilter(string exec, string path, string MainWindowTitle = "")
    {
        if (string.IsNullOrEmpty(path))
            return ProcessFilter.Restricted;

        // manual filtering
        switch (exec.ToLower())
        {
            // handheld companion
            case "handheldcompanion.exe":
            {
                /* if (!string.IsNullOrEmpty(MainWindowTitle))
                {
                    switch (MainWindowTitle)
                    {
                        case "QuickTools":
                            return ProcessFilter.HandheldCompanion;
                    }
                } */

                return ProcessFilter.HandheldCompanion;
            }

            case "rw.exe": // Used to change TDP
            case "kx.exe": // Used to change TDP
            case "devenv.exe": // Visual Studio
            case "msedge.exe": // Edge has energy awareness
            case "webviewhost.exe":
            case "taskmgr.exe":
            case "procmon.exe":
            case "procmon64.exe":
            case "widgets.exe":

            // System shell
            case "dwm.exe":
            case "sihost.exe":
            case "fontdrvhost.exe":
            case "chsime.exe":
            case "ctfmon.exe":
            case "csrss.exe":
            case "smss.exe":
            case "svchost.exe":
            case "wudfrd.exe":

            // Other
            case "bdagent.exe": // Bitdefender Agent
            case "monotificationux.exe":

            // Controller service
            case "controllerservice.exe":
            case "controllerservice.dll":
                return ProcessFilter.Restricted;

            // Desktop
            case "radeonsoftware.exe":
            case "applicationframehost.exe":
            case "shellexperiencehost.exe":
            case "startmenuexperiencehost.exe":
            case "searchhost.exe":
            case "explorer.exe":
            case "hwinfo64.exe":
            case "searchapp.exe":
            case "ashotplugctrl.exe":
            case "gameinputsvc.exe":
            case "gamebuzz.exe":
            case "asmultidisplaycontrol.exe":
            case "lockapp":
                return ProcessFilter.Desktop;

            default:
                return ProcessFilter.Allowed;
        }
    }

    public static void ResumeProcess(ProcessEx processEx)
    {
        // process has exited
        if (processEx.Process.HasExited)
            return;

        ProcessUtils.NtResumeProcess(processEx.Process.Handle);

        processEx.RefreshChildProcesses();
        Parallel.ForEach(processEx.Children,
            new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, childId =>
            {
                var process = Process.GetProcessById(childId);
                ProcessUtils.NtResumeProcess(process.Handle);
            });

        Task.Delay(500);
        ProcessUtils.ShowWindow(processEx.MainWindowHandle, (int)ProcessUtils.ShowWindowCommands.Restored);
    }

    public static void SuspendProcess(ProcessEx processEx)
    {
        // process has exited
        if (processEx.Process.HasExited)
            return;

        ProcessUtils.ShowWindow(processEx.MainWindowHandle, (int)ProcessUtils.ShowWindowCommands.Minimized);
        Task.Delay(500);

        ProcessUtils.NtSuspendProcess(processEx.Process.Handle);

        processEx.RefreshChildProcesses();
        Parallel.ForEach(processEx.Children,
            new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, childId =>
            {
                var process = Process.GetProcessById(childId);
                ProcessUtils.NtSuspendProcess(process.Handle);
            });
    }

    #region imports

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, int idProcess, int idThread, uint dwflags);

    [DllImport("user32.dll")]
    internal static extern int UnhookWinEvent(IntPtr hWinEventHook);

    internal delegate void WinEventProc(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild,
        int dwEventThread, int dwmsEventTime);

    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003; // The foreground window has changed

    private const uint
        EVENT_SYSTEM_MINIMIZESTART =
            0x0016; // A window object is about to be minimized.This event is sent by the system, never by servers

    private const uint
        EVENT_SYSTEM_MINIMIZEEND =
            0x0017; // A window object is about to be restored.This event is sent by the system, never by servers

    private const uint EVENT_OBJECT_CREATE = 0x8000; // An object has been created
    private const uint EVENT_OBJECT_DESTROY = 0x8001; // An object has been destroyed

    private static IntPtr HookForeground;
    private static IntPtr HookMinimize;
    private static WinEventProc WinEventWindows;

    public delegate bool WindowEnumCallback(IntPtr hwnd, int lparam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowEnumCallback lpEnumFunc, int lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(int h);

    #endregion

    #region events

    public static event ForegroundChangedEventHandler ForegroundChanged;

    public delegate void ForegroundChangedEventHandler(ProcessEx processEx, ProcessEx backgroundEx);

    public static event ProcessStartedEventHandler ProcessStarted;

    public delegate void ProcessStartedEventHandler(ProcessEx processEx, bool OnStartup);

    public static event ProcessStoppedEventHandler ProcessStopped;

    public delegate void ProcessStoppedEventHandler(ProcessEx processEx);

    public static event InitializedEventHandler Initialized;

    public delegate void InitializedEventHandler();

    #endregion
}
