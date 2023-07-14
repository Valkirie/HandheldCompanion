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
using Windows.System.Diagnostics;
using static ControllerCommon.WinAPI;
using static HandheldCompanion.Controls.ProcessEx;
using static HandheldCompanion.Managers.EnergyManager;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ProcessManager
{
    // process vars
    private static readonly Timer ForegroundTimer;

    private static readonly ConcurrentDictionary<int, ProcessEx> Processes = new();

    private static ProcessEx foregroundProcess;
    private static ProcessEx previousProcess;

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
        ForegroundTimer = new Timer(1000);
        ForegroundTimer.Elapsed += ForegroundCallback;
    }

    public static void Start()
    {
        // start processes monitor
        ForegroundTimer.Start();

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
        ForegroundTimer.Elapsed -= ForegroundCallback;
        ForegroundTimer.Stop();

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

    public static bool HasProcess(int pId)
    {
        return Processes.ContainsKey(pId);
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
    
    private static void ForegroundCallback(object? sender, EventArgs e)
    {
        IntPtr hWnd = GetforegroundWindow();

        ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
        if (processInfo is null)
            return;

        try
        {
            int processId = (int)processInfo.ProcessId;

            if (!Processes.TryGetValue(processId, out ProcessEx process))
            {
                if (!ProcessCreated(processId, (int)hWnd))
                    return;
                process = Processes[processId];
            }

            ProcessEx prevProcess = foregroundProcess;

            // filter based on current process status
            ProcessFilter filter = GetFilter(process.Executable, process.Path, ProcessUtils.GetWindowTitle(hWnd));
            switch (filter)
            {
                // do nothing on QuickTools window, current process is kept
                case ProcessFilter.HandheldCompanion:
                    return;
                // foreground of those processes is ignored, they fallback to default
                case ProcessFilter.Desktop:
                    return;
                // update foreground process
                default:
                    foregroundProcess = process;
                    foregroundProcess.MainWindowHandle = hWnd;
                    break;
            }

            // nothing's changed
            if (foregroundProcess == prevProcess)
                return;

            if (foregroundProcess is not null)
                LogManager.LogDebug("{0} process {1} now has the foreground", foregroundProcess.Platform, foregroundProcess.Executable);
            else
                LogManager.LogDebug("No current foreground process or it is ignored");

            // raise event
            ForegroundChanged?.Invoke(foregroundProcess, prevProcess);
        }
        catch
        {
            // process has too high elevation
            return;
        }
    }

    private static void ProcessHalted(object? sender, EventArgs e)
    {
        int processId = ((Process)sender).Id;

        if (!Processes.TryGetValue(processId, out ProcessEx processEx))
            return;

        // stopped process can't have foreground
        if (foregroundProcess == processEx)
        {
            LogManager.LogDebug("{0} process {1} that had foreground has halted", foregroundProcess.Platform, foregroundProcess.Executable);
            ForegroundChanged?.Invoke(Empty, foregroundProcess);
        }

        Processes.TryRemove(new KeyValuePair<int, ProcessEx>(processId, processEx));

        // raise event
        ProcessStopped?.Invoke(processEx);

        LogManager.LogDebug("Process halted: {0}", processEx.Executable);

        processEx.Dispose();
    }

    private static bool ProcessCreated(int ProcessID, int NativeWindowHandle = 0, bool OnStartup = false)
    {
        try
        {
            // process has exited on arrival
            Process proc = Process.GetProcessById(ProcessID);
            if (proc.HasExited)
                return false;

            // hook into events
            proc.EnableRaisingEvents = true;

            if (Processes.ContainsKey(proc.Id))
                return true;

            // UI thread (synchronous)
            Application.Current.Dispatcher.Invoke(() =>
            {
                // check process path
                string path = ProcessUtils.GetPathToApp(proc.Id);
                if (string.IsNullOrEmpty(path))
                    return false;

                string exec = Path.GetFileName(path);
                IntPtr hWnd = NativeWindowHandle != 0 ? NativeWindowHandle : proc.MainWindowHandle;

                // get filter
                ProcessFilter filter = GetFilter(exec, path);

                ProcessEx processEx = new ProcessEx(proc, path, exec, filter);
                processEx.MainWindowHandle = hWnd;

                Processes.TryAdd(processEx.GetProcessId(), processEx);
                proc.Exited += ProcessHalted;

                if (processEx.Filter != ProcessFilter.Allowed)
                    return true;

                // raise event
                ProcessStarted?.Invoke(processEx, OnStartup);

                LogManager.LogDebug("Process detected: {0}", processEx.Executable);

                return true;
            });
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
            case "logioverlay.exe":
            case "gog galaxy notifications renderer.exe":
            case "ashotplugctrl.exe":
            case "gameinputsvc.exe":
            case "gamebuzz.exe":
            case "asmultidisplaycontrol.exe":
            case "lockapp.exe":
            case "asusosd.exe":
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
