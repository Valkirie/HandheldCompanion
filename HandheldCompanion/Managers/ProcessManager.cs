using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using Windows.System.Diagnostics;
using static ControllerCommon.WinAPI;
using static HandheldCompanion.Controls.ProcessEx;
using static HandheldCompanion.Managers.EnergyManager;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public static class ProcessManager
    {
        #region imports
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, int idProcess, int idThread, uint dwflags);
        [DllImport("user32.dll")]
        internal static extern int UnhookWinEvent(IntPtr hWinEventHook);
        internal delegate void WinEventProc(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime);

        const uint WINEVENT_OUTOFCONTEXT = 0;
        const uint EVENT_SYSTEM_FOREGROUND = 3;
        private static IntPtr winHook;
        private static WinEventProc listener;

        public delegate bool WindowEnumCallback(IntPtr hwnd, int lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(WindowEnumCallback lpEnumFunc, int lParam);

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

        // process vars
        private static Timer MonitorTimer;
        private static ManagementEventWatcher MonitorWatcher;

        private static ConcurrentDictionary<int, ProcessEx> Processes = new();

        private static ProcessEx foregroundProcess;
        private static ProcessEx backgroundProcess;

        private static object updateLock = new();
        private static bool IsInitialized;

        static ProcessManager()
        {
            MonitorTimer = new Timer(2000);
            MonitorTimer.Elapsed += MonitorHelper;

            // hook: on process halt
            MonitorWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            MonitorWatcher.EventArrived += new EventArrivedEventHandler(ProcessHalted);
        }

        public static void Start()
        {
            // start processes monitor
            MonitorTimer.Start();

            // hook: on window opened
            Automation.AddAutomationEventHandler(
                eventId: WindowPattern.WindowOpenedEvent,
                element: AutomationElement.RootElement,
                scope: TreeScope.Children,
                eventHandler: OnWindowOpened);

            // hook: on window foregroud
            listener = new WinEventProc(OnWindowForeground);
            winHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, listener, 0, 0, WINEVENT_OUTOFCONTEXT);

            // hook: on process stop
            MonitorWatcher.Start();

            // list all current windows
            EnumWindows(new WindowEnumCallback(OnWindowDiscovered), 0);

            // get current foreground process
            IntPtr hWnd = GetforegroundWindow();
            OnWindowForeground((IntPtr)0, 0, hWnd, 0, 0, 0, 0);

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

            MonitorWatcher.Stop();
            Automation.RemoveAllEventHandlers();

            UnhookWinEvent(winHook);

            LogManager.LogInformation("{0} has stopped", "ProcessManager");
        }

        public static ProcessEx GetForegroundProcess()
        {
            return foregroundProcess;
        }

        public static ProcessEx GetLastSuspendedProcess()
        {
            return Processes.Values.Where(item => item.IsSuspended()).LastOrDefault();
        }

        public static List<ProcessEx> GetProcesses()
        {
            return Processes.Values.ToList();
        }

        public static List<ProcessEx> GetProcesses(string executable)
        {
            return Processes.Values.Where(a => a.Executable.Equals(executable, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        public static ProcessEx GetProcesses(int pId)
        {
            if (Processes.ContainsKey(pId))
                return Processes[pId];

            return null;
        }

        private static void OnWindowOpened(object sender, AutomationEventArgs automationEventArgs)
        {
            try
            {
                var element = sender as AutomationElement;
                if (element is not null)
                {
                    ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(element.Current.NativeWindowHandle)._realProcess;
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
                ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
                if (processInfo is null)
                    return false;

                ProcessCreated((int)processInfo.ProcessId, (int)hWnd, true);
            }
            return true;
        }

        private static async void OnWindowForeground(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime)
        {
            ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd)._realProcess;
            if (processInfo is null)
                return;

            try
            {
                int processId = (int)processInfo.ProcessId;

                byte attempts = 0;
                while (!Processes.ContainsKey(processId))
                {
                    attempts++;

                    // exlusive fullscreen applications won't trigger WindowOpenedEvent
                    // therefore we have to force call a process creation
                    if (attempts == 10)
                    {
                        ProcessCreated(processId, (int)hWnd);
                        attempts = 0;
                    }

                    await Task.Delay(250);
                }

                // pull process from running processes
                ProcessEx process = Processes[processId];

                // get filter
                ProcessFilter filter = GetFilter(process.Executable, process.Path, ProcessUtils.GetWindowTitle(hWnd));
                switch(filter)
                {
                    case ProcessFilter.HandheldCompanion:
                    // case ProcessFilter.Desktop:
                        return;
                }

                // save previous process (if exists)
                if (foregroundProcess is not null)
                    backgroundProcess = foregroundProcess;

                // update foreground process
                foregroundProcess = process;

                // update main window handle
                foregroundProcess.MainWindowHandle = hWnd;

                // inform service
                PipeClient.SendMessage(new PipeClientProcess { executable = foregroundProcess.Executable, platform = foregroundProcess.Platform });

                // raise event
                ForegroundChanged?.Invoke(foregroundProcess, backgroundProcess);

                LogManager.LogDebug("{0} executable {1} now has the foreground", foregroundProcess.Platform, foregroundProcess.Executable);
            }
            catch
            {
                // process has too high elevation
                return;
            }

        }

        private static void MonitorHelper(object? sender, EventArgs e)
        {
            if (Monitor.TryEnter(updateLock))
            {
                Parallel.ForEach(Processes.Values, new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, proc =>
                {
                    proc.Refresh();
                });

                Monitor.Exit(updateLock);
            }
        }

        private static void ProcessHalted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                int processId = int.Parse(e.NewEvent.Properties["ProcessID"].Value.ToString());
                ProcessHalted(processId);
            }
            catch { }
        }

        private static void ProcessHalted(int processId)
        {
            if (Processes.ContainsKey(processId))
            {
                ProcessEx processEx = Processes[processId];

                Processes.TryRemove(new KeyValuePair<int, ProcessEx>(processId, processEx));

                // raise event
                ProcessStopped?.Invoke(processEx);

                LogManager.LogDebug("Process halted: {0}", processEx.Title);

                processEx.Dispose();
            }
        }

        private static void ProcessCreated(int ProcessID, int NativeWindowHandle = 0, bool OnStartup = false)
        {
            try
            {
                // process has exited on arrival
                Process proc = Process.GetProcessById(ProcessID);
                if (proc.HasExited)
                    return;

                if (!Processes.ContainsKey(proc.Id))
                {
                    // UI thread (async)
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // check process path
                        string path = ProcessUtils.GetPathToApp(proc.Id);
                        if (string.IsNullOrEmpty(path))
                            return;

                        string exec = Path.GetFileName(path);
                        IntPtr hWnd = NativeWindowHandle != 0 ? NativeWindowHandle : proc.MainWindowHandle;

                        // get filter
                        ProcessFilter filter = GetFilter(exec, path);

                        ProcessEx processEx = new ProcessEx(proc, path, exec, filter);
                        processEx.MainWindowHandle = hWnd;
                        // processEx.ChildProcessCreated += ChildProcessCreated;

                        Processes.TryAdd(processEx.GetProcessId(), processEx);

                        if (processEx.Filter != ProcessFilter.Allowed)
                            return;

                        // raise event
                        ProcessStarted?.Invoke(processEx, OnStartup);
                    });
                }
            }
            catch
            {
                // process has too high elevation
                return;
            }
        }

        private static void ChildProcessCreated(ProcessEx parent, int pId)
        {
            EfficiencyMode mode = parent.GetEfficiencyMode();
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
                        if (!string.IsNullOrEmpty(MainWindowTitle))
                        {
                            switch (MainWindowTitle)
                            {
                                case "QuickTools":
                                    return ProcessFilter.HandheldCompanion;
                            }
                        }

                        return ProcessFilter.Restricted;
                    }

                case "rw.exe":                  // Used to change TDP
                case "kx.exe":                  // Used to change TDP
                case "devenv.exe":              // Visual Studio
                case "msedge.exe":              // Edge has energy awareness
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
                case "bdagent.exe":             // Bitdefender Agent
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
            Parallel.ForEach(processEx.Children, new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, childId =>
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

            ProcessUtils.ShowWindow(processEx.MainWindowHandle, (int)ProcessUtils.ShowWindowCommands.Minimized);
            Task.Delay(500);

            ProcessUtils.NtSuspendProcess(processEx.Process.Handle);

            processEx.RefreshChildProcesses();
            Parallel.ForEach(processEx.Children, new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, childId =>
            {
                Process process = Process.GetProcessById(childId);
                ProcessUtils.NtSuspendProcess(process.Handle);
            });
        }
    }
}
