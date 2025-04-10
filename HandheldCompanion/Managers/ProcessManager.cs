using HandheldCompanion.Helpers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;
using Windows.System.Diagnostics;
using static HandheldCompanion.Misc.ProcessEx;
using static HandheldCompanion.WinAPI;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public class ProcessManager : IManager
{
    #region imports
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowEnumCallback lpEnumFunc, int lParam);
    public delegate bool WindowEnumCallback(IntPtr hwnd, int lparam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(int h);

    // Import the necessary user32.dll functions
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    #endregion

    // Declare the WinEventDelegate
    private WinEventDelegate winDelegate = null;
    private IntPtr m_hhook = IntPtr.Zero;

    // Define the WinEventDelegate
    delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // Constants for WinEvent hook
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 3;

    // process vars
    private readonly Timer ForegroundTimer;
    private readonly Timer ProcessWatcher;

    private static readonly ConcurrentDictionary<int, ProcessEx> Processes = new();
    private object processLock = new();

    private static ProcessEx foregroundProcess;
    private IntPtr foregroundWindow;

    private AutomationEventHandler _windowOpenedHandler;

    public ProcessManager()
    {
        // hook: on window opened
        _windowOpenedHandler = OnWindowOpened;

        Automation.AddAutomationEventHandler(
            WindowPattern.WindowOpenedEvent,
            AutomationElement.RootElement,
            TreeScope.Children,
            _windowOpenedHandler);

        // Set up the WinEvent hook
        winDelegate = new WinEventDelegate(WinEventProc);
        m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, winDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

        ForegroundTimer = new Timer(2000);
        ForegroundTimer.Elapsed += (sender, e) => ForegroundCallback();

        ProcessWatcher = new Timer(2000);
        ProcessWatcher.Elapsed += (sender, e) => ProcessWatcher_Elapsed();
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // list all current windows
        EnumWindows(OnWindowDiscovered, 0);

        // start processes monitor
        ForegroundTimer.Start();
        ProcessWatcher.Start();

        base.Start();
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // Remove the WindowOpened event handler
        Automation.RemoveAutomationEventHandler(
            WindowPattern.WindowOpenedEvent,
            AutomationElement.RootElement,
            _windowOpenedHandler);

        // Unhook the event when no longer needed
        if (m_hhook != IntPtr.Zero)
        {
            UnhookWinEvent(m_hhook);
            m_hhook = IntPtr.Zero; // Reset handle to indicate it's unhooked
        }

        // stop processes monitor
        ForegroundTimer.Stop();
        ProcessWatcher.Stop();

        base.Stop();
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Avoid locking UI thread by running the action in a task
        Task.Run(() => ForegroundCallback());
    }

    private void OnWindowOpened(object sender, AutomationEventArgs automationEventArgs)
    {
        try
        {
            if (sender is AutomationElement senderElement)
            {
                int processId = 0;

                try
                {
                    processId = senderElement.Current.ProcessId;
                }
                catch
                {
                    // Automation failed to retrieve process id
                }

                // is this a hosted process
                ProcessDiagnosticInfo? processInfo = new ProcessUtils.FindHostedProcess(senderElement.Current.NativeWindowHandle)._realProcess;
                if (processInfo != null)
                    processId = (int)processInfo.ProcessId;

                // skip if we couldn't find a process id
                if (processId == 0)
                    return;

                // create process
                CreateOrUpdateProcess(processId, senderElement);
            }
        }
        catch { }
    }

    private bool OnWindowDiscovered(IntPtr hWnd, int lparam)
    {
        if (IsWindowVisible((int)hWnd))
        {
            try
            {
                AutomationElement element = AutomationElement.FromHandle(hWnd);
                if (element is null)
                    return false;

                int processId = element.Current.NativeWindowHandle;

                ProcessDiagnosticInfo? processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
                if (processInfo != null)
                    processId = (int)processInfo.ProcessId;

                // skip if we couldn't find a process id
                if (processId == 0)
                    return false;

                // create process
                CreateOrUpdateProcess(processId, element, true);
            }
            catch
            {
                // timeout
            }
        }

        return true;
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

    private async void ForegroundCallback()
    {
        IntPtr hWnd = GetforegroundWindow();

        // skip if this window is already in foreground
        if (foregroundWindow == hWnd || hWnd == IntPtr.Zero)
            return;

        AutomationElement element = null;
        int processId = 0;

        try
        {
            element = AutomationElement.FromHandle(hWnd);

            if (element is null)
                return;

            processId = element.Current.ProcessId;
        }
        catch
        {
            // Automation failed to retrieve process id
        }

        ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
        if (processInfo is not null)
            processId = (int)processInfo.ProcessId;

        // failed to retrieve process
        if (processId == 0)
            return;

        try
        {
            if (!Processes.ContainsKey(processId))
                if (!CreateOrUpdateProcess(processId, element))
                    return;

            if (!Processes.TryGetValue(processId, out ProcessEx process))
                return;

            ProcessEx prevProcess = foregroundProcess;

            // filter based on current process status
            ProcessFilter filter = GetFilter(process.Executable, process.Path /*, ProcessUtils.GetWindowTitle(hWnd) */);
            switch (filter)
            {
                // do nothing on QuickTools window, current process is kept
                case ProcessFilter.HandheldCompanion:
                    // case ProcessFilter.Desktop:
                    return;
                // update foreground process
                default:
                    foregroundProcess = process;
                    foregroundProcess.Refresh();
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

            // update current foreground window
            foregroundWindow = hWnd;
        }
        catch
        {
            // process has too high elevation
            return;
        }
    }

    private void ProcessHalted(object? sender, EventArgs e)
    {
        // Get the processId
        int processId = ((Process)sender).Id;

        object lockObject = processLocks.GetOrAdd(processId, id => new object());
        lock (lockObject)
        {
            if (!Processes.TryGetValue(processId, out ProcessEx processEx))
                return;

            // If the halted process had foreground, log and raise event.
            if (foregroundProcess == processEx)
            {
                LogManager.LogDebug("{0} process {1} that had foreground has halted", foregroundProcess.Platform, foregroundProcess.Executable);
                ForegroundChanged?.Invoke(null, foregroundProcess);
            }

            // Remove the process from the dictionary and raise the stopped event.
            if (Processes.TryRemove(processId, out _))
            {
                ProcessStopped?.Invoke(processEx);
                LogManager.LogDebug("Process halted: {0}", processEx.Executable);
            }

            // Dispose the process.
            processEx.Dispose();
        }
        
        processLocks.TryRemove(processId, out _);
    }

    // Define a thread-safe dictionary to hold lock objects per process id.
    private static readonly ConcurrentDictionary<int, object> processLocks = new ConcurrentDictionary<int, object>();

    private bool CreateOrUpdateProcess(int processID, AutomationElement automationElement, bool OnStartup = false)
    {
        object lockObject = processLocks.GetOrAdd(processID, id => new object());
        lock (lockObject)
        {
            try
            {
                if (!automationElement.Current.IsContentElement && !automationElement.Current.IsControlElement)
                    return false;

                // Process has exited on arrival
                Process proc = Process.GetProcessById(processID);
                if (proc.HasExited)
                    return false;

                if (!Processes.TryGetValue(proc.Id, out ProcessEx processEx))
                {
                    // Hook exited event
                    try
                    {
                        proc.EnableRaisingEvents = true;
                    }
                    catch (Exception)
                    {
                        // Access denied
                    }
                    proc.Exited += ProcessHalted;

                    // Check process path
                    string path = ProcessUtils.GetPathToApp(proc.Id);
                    if (string.IsNullOrEmpty(path))
                        return false;

                    // Get filter
                    string exec = Path.GetFileName(path);
                    ProcessFilter filter = GetFilter(exec, path);

                    // Create process 
                    // UI thread (synchronous)
                    UIHelper.TryInvoke(() =>
                    {
                        try
                        {
                            processEx = new ProcessEx(proc, path, exec, filter);
                        }
                        catch
                        {
                            // Handle exception if needed
                        }
                    });

                    if (processEx is null)
                        return false;

                    // Attach current window
                    processEx.AttachWindow(automationElement);

                    // Get the proper platform
                    processEx.Platform = PlatformManager.GetPlatform(proc);

                    // Add to dictionary
                    Processes.TryAdd(processID, processEx);

                    // Raise event if allowed
                    if (processEx.Filter != ProcessFilter.Allowed)
                        return true;

                    ProcessStarted?.Invoke(processEx, OnStartup);
                    LogManager.LogDebug("Process detected: {0}", processEx.Executable);
                }
                else
                {
                    // Process already exists; attach current window
                    processEx.AttachWindow(automationElement);
                }

                return true;
            }
            catch (Exception)
            {
                // Process has too high elevation or other error occurred
            }
            return false;
        }
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
            case "textinputhost.exe":
                return ProcessFilter.Desktop;

            default:
                return ProcessFilter.Allowed;
        }
    }

    private void ProcessWatcher_Elapsed()
    {
        Parallel.ForEach(Processes,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, process =>
            {
                try
                {
                    ProcessEx processEx = process.Value;
                    processEx.Refresh();
                }
                catch { }
            });
    }

    private static Dictionary<int, int[]> windowsCache = new();

    public static async void ResumeProcess(ProcessEx processEx)
    {
        // process has exited
        if (processEx.Process.HasExited)
            return;

        // refresh processes handles
        List<nint> handles = ProcessUtils.GetChildProcesses(processEx.ProcessId).Select(p => p.Handle).ToList();
        handles.Insert(0, processEx.Handle);

        // resume processes
        foreach (int handle in handles)
            ProcessUtils.NtResumeProcess(handle);

        // wait a bit
        await Task.Delay(500).ConfigureAwait(false); // Avoid blocking the synchronization context

        // restore process windows
        foreach (int Hwnd in windowsCache[processEx.ProcessId])
            ProcessUtils.ShowWindow(Hwnd, (int)ProcessUtils.ShowWindowCommands.Restored);

        // clear cache
        windowsCache.Remove(processEx.ProcessId);
    }

    public static async void SuspendProcess(ProcessEx processEx)
    {
        // process has exited
        if (processEx.Process.HasExited)
            return;

        // store process windows in cache
        windowsCache[processEx.ProcessId] = processEx.ProcessWindows.Keys.ToArray();

        // hide process windows
        foreach (int Hwnd in windowsCache[processEx.ProcessId])
            ProcessUtils.ShowWindow(Hwnd, (int)ProcessUtils.ShowWindowCommands.Hide);

        // wait a bit
        await Task.Delay(500).ConfigureAwait(false); // Avoid blocking the synchronization context

        // refresh processes handles
        List<nint> handles = ProcessUtils.GetChildProcesses(processEx.ProcessId).Select(p => p.Handle).ToList();
        handles.Insert(0, processEx.Handle);

        // suspend processes
        foreach(int handle in handles)
            ProcessUtils.NtSuspendProcess(handle);
    }

    // A function that takes a Process as a parameter and returns true if it has any xinput related dlls in its modules
    public static bool CheckXInput(Process process)
    {
        try
        {
            // Loop through the modules of the process
            foreach (ProcessModule module in process.Modules)
            {
                try
                {
                    // Get the name of the module
                    string moduleName = module.ModuleName.ToLower();

                    // Check if the name contains "xinput"
                    if (moduleName.Contains("xinput", StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
            }
        }
        catch { }

        return false;
    }

    #region events

    public event ForegroundChangedEventHandler ForegroundChanged;
    public delegate void ForegroundChangedEventHandler(ProcessEx? processEx, ProcessEx? backgroundEx);

    public event ProcessStartedEventHandler ProcessStarted;
    public delegate void ProcessStartedEventHandler(ProcessEx processEx, bool OnStartup);

    public event ProcessStoppedEventHandler ProcessStopped;
    public delegate void ProcessStoppedEventHandler(ProcessEx processEx);

    #endregion
}
