using HandheldCompanion.Controls;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using Windows.System.Diagnostics;
using static HandheldCompanion.Controls.ProcessEx;
using static HandheldCompanion.WinAPI;
using ThreadState = System.Diagnostics.ThreadState;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ProcessManager
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowEnumCallback lpEnumFunc, int lParam);
    public delegate bool WindowEnumCallback(IntPtr hwnd, int lparam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(int h);

    // Import the necessary user32.dll functions
    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    // Declare the WinEventDelegate
    private static WinEventDelegate winDelegate = null;

    // Define the WinEventDelegate
    delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // Constants for WinEvent hook
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 3;

    // process vars
    private static readonly Timer ForegroundTimer;
    private static readonly Timer ProcessWatcher;

    private static readonly ConcurrentDictionary<int, ProcessEx> Processes = new();
    private static ProcessEx foregroundProcess;
    private static IntPtr foregroundWindow;

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
        // hook: on window opened
        Automation.AddAutomationEventHandler(
            WindowPattern.WindowOpenedEvent,
            AutomationElement.RootElement,
            TreeScope.Children,
            OnWindowOpened);

        // Set up the WinEvent hook
        winDelegate = new WinEventDelegate(WinEventProc);
        IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, winDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

        ForegroundTimer = new Timer(2000);
        ForegroundTimer.Elapsed += (sender, e) => ForegroundCallback();

        ProcessWatcher = new Timer(2000);
        ProcessWatcher.Elapsed += (sender, e) => ProcessWatcher_Elapsed();
    }

    private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // avoid locking UI thread
        new Thread(() => { ForegroundCallback(); }).Start();        
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

                CreateProcess((int)processInfo.ProcessId, element.Current.NativeWindowHandle);
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

            CreateProcess((int)processInfo.ProcessId, (int)hWnd, true);
        }

        return true;
    }

    public static void Start()
    {
        // list all current windows
        EnumWindows(OnWindowDiscovered, 0);

        // start processes monitor
        ForegroundTimer.Start();
        ProcessWatcher.Start();

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
        ForegroundTimer.Stop();
        ProcessWatcher.Stop();

        LogManager.LogInformation("{0} has stopped", "ProcessManager");
    }

    public static ProcessEx GetForegroundProcess()
    {
        return foregroundProcess;
    }

    public static ProcessEx GetLastSuspendedProcess()
    {
        return Processes.Values.LastOrDefault(item => item.IsSuspended);
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
        return Processes.Values.Where(a => a.Executable.Equals(executable, StringComparison.InvariantCultureIgnoreCase)).ToList();
    }

    private static void ForegroundCallback()
    {
        IntPtr hWnd = GetforegroundWindow();
        if (foregroundWindow == hWnd)
            return;

        int processId = 0;

        // update current foreground window
        foregroundWindow = hWnd;

        ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
        if (processInfo is not null)
        {
            processId = (int)processInfo.ProcessId;
        }
        else
        {
            // we couldn't find the hosted process
            // use Levenshtein to find the process with closest name
            Process process = ProcessUtils.FindProcessByWindowName(hWnd);
            if (process is not null)
                processId = process.Id;
        }

        // failed to retrieve process
        if (processId == 0)
            return;

        try
        {
            if (!Processes.TryGetValue(processId, out ProcessEx process))
            {
                if (!CreateProcess(processId, (int)hWnd))
                    return;
                process = Processes[processId];
            }

            ProcessEx prevProcess = foregroundProcess;

            // filter based on current process status
            ProcessFilter filter = GetFilter(process.Executable, process.Path /*, ProcessUtils.GetWindowTitle(hWnd) */);
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
            ForegroundChanged?.Invoke(null, foregroundProcess);
        }

        Processes.TryRemove(new KeyValuePair<int, ProcessEx>(processId, processEx));

        // raise event
        ProcessStopped?.Invoke(processEx);

        LogManager.LogDebug("Process halted: {0}", processEx.Executable);

        processEx.Dispose();
    }

    private static bool CreateProcess(int ProcessID, int NativeWindowHandle = 0, bool OnStartup = false)
    {
        try
        {
            // process has exited on arrival
            Process proc = Process.GetProcessById(ProcessID);
            if (proc.HasExited)
                return false;

            if (Processes.ContainsKey(proc.Id))
                return true;

            // hook exited event
            try
            {
                proc.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                // access denied
            }
            proc.Exited += ProcessHalted;

            // check process path
            string path = ProcessUtils.GetPathToApp(proc.Id);
            if (string.IsNullOrEmpty(path))
                return false;

            string exec = Path.GetFileName(path);
            IntPtr hWnd = NativeWindowHandle != 0 ? NativeWindowHandle : proc.MainWindowHandle;

            // get filter
            ProcessFilter filter = GetFilter(exec, path);

            // UI thread (synchronous)
            ProcessEx processEx = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                // create process
                processEx = new ProcessEx(proc, path, exec, filter);
            });

            if (processEx is null)
                return false;

            processEx.MainWindowHandle = hWnd;
            processEx.MainThread = GetMainThread(proc);
            if (processEx.MainThread is not null)
                processEx.MainThread.Disposed += (sender, e) => processEx.MainThreadDisposed();
            processEx.Platform = PlatformManager.GetPlatform(proc);

            Processes.TryAdd(ProcessID, processEx);

            if (processEx.Filter != ProcessFilter.Allowed)
                return true;

            // raise event
            ProcessStarted?.Invoke(processEx, OnStartup);

            LogManager.LogDebug("Process detected: {0}", processEx.Executable);

            return true;
        }
        catch (Exception ex)
        {
            // process has too high elevation
        }

        return false;
    }

    private static ProcessFilter GetFilter(string exec, string path, string MainWindowTitle = "")
    {
        if (string.IsNullOrEmpty(path))
            return ProcessFilter.Restricted;

        // manual filtering, case entries need to be all lower case
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
            // case "devenv.exe": // Visual Studio
            // case "msedge.exe": // Edge has energy awareness
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
                return ProcessFilter.Restricted;

            // Desktop
            case "applicationframehost.exe":
            case "ashotplugctrl.exe":
            case "asmultidisplaycontrol.exe":
            case "asusosd.exe":
            case "explorer.exe":
            case "gamebuzz.exe":
            case "gameinputsvc.exe":
            case "gamepadcustomizeosd":
            case "gog galaxy notifications renderer.exe":
            case "hwinfo64.exe":
            case "lockapp.exe":
            case "logioverlay.exe":
            case "losslessscaling.exe":
            case "mspcmanager.exe":
            case "powertoys.mousewithoutbordershelper.exe":
            case "radeonsoftware.exe":
            case "rtkuwp.exe":
            case "searchapp.exe":
            case "searchhost.exe":
            case "shellexperiencehost.exe":
            case "startmenuexperiencehost.exe":
                return ProcessFilter.Desktop;

            default:
                return ProcessFilter.Allowed;
        }
    }

    public static ProcessThread GetMainThread(Process process)
    {
        ProcessThread mainThread = null;
        var startTime = DateTime.MaxValue;

        try
        {
            if (process.Threads is null || process.Threads.Count == 0)
                return null;

            foreach (ProcessThread thread in process.Threads)
            {
                if (thread.ThreadState != ThreadState.Running)
                    continue;

                if (thread.StartTime < startTime)
                {
                    startTime = thread.StartTime;
                    mainThread = thread;
                }
            }

            if (mainThread is null)
                mainThread = process.Threads[0];
        }
        catch (Win32Exception)
        {
            // Access if denied
        }
        catch (InvalidOperationException)
        {
            // thread has exited
        }

        return mainThread;
    }

    private static void ProcessWatcher_Elapsed()
    {
        Parallel.ForEach(Processes,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, process =>
            {
                ProcessEx processEx = process.Value;
                processEx.Refresh();
            });
    }

    public static void ResumeProcess(ProcessEx processEx)
    {
        // process has exited
        if (processEx.Process.HasExited)
            return;

        ProcessUtils.NtResumeProcess(processEx.Process.Handle);

        // refresh child processes list (most likely useless, a suspended process shouldn't have new child processes)
        processEx.RefreshChildProcesses();

        Parallel.ForEach(processEx.Children,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, childId =>
            {
                Process process = Process.GetProcessById(childId);
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

        ProcessUtils.ShowWindow(processEx.MainWindowHandle, (int)ProcessUtils.ShowWindowCommands.Hide);
        Task.Delay(500);

        ProcessUtils.NtSuspendProcess(processEx.Process.Handle);

        // refresh child processes list
        processEx.RefreshChildProcesses();

        Parallel.ForEach(processEx.Children,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, childId =>
            {
                Process process = Process.GetProcessById(childId);
                ProcessUtils.NtSuspendProcess(process.Handle);
            });
    }

    // A function that takes a Process as a parameter and returns true if it has any xinput related dlls in its modules
    public static bool CheckXInput(Process process)
    {
        // Loop through the modules of the process
        foreach (ProcessModule module in process.Modules)
        {
            // Get the name of the module
            string moduleName = module.ModuleName.ToLower();

            // Check if the name contains "xinput"
            if (moduleName.Contains("xinput"))
            {
                // Return true if found
                return true;
            }
        }

        // Return false if not found
        return false;
    }

    #region events

    public static event ForegroundChangedEventHandler ForegroundChanged;

    public delegate void ForegroundChangedEventHandler(ProcessEx? processEx, ProcessEx? backgroundEx);

    public static event ProcessStartedEventHandler ProcessStarted;

    public delegate void ProcessStartedEventHandler(ProcessEx processEx, bool OnStartup);

    public static event ProcessStoppedEventHandler ProcessStopped;

    public delegate void ProcessStoppedEventHandler(ProcessEx processEx);

    public static event InitializedEventHandler Initialized;

    public delegate void InitializedEventHandler();

    #endregion
}
