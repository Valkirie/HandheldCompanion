using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers.Classes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Windows.System.Diagnostics;
using static ControllerCommon.WinAPI;
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

        // process vars
        private static Timer MonitorTimer;
        private static ManagementEventWatcher stopWatch;

        private static ConcurrentDictionary<int, ProcessEx> Processes = new();

        private static ProcessEx foregroundProcess;
        private static ProcessEx backgroundProcess;

        private static object updateLock = new();
        private static bool IsInitialized;

        public static event ForegroundChangedEventHandler ForegroundChanged;
        public delegate void ForegroundChangedEventHandler(ProcessEx processEx, ProcessEx backgroundEx);

        public static event ProcessStartedEventHandler ProcessStarted;
        public delegate void ProcessStartedEventHandler(ProcessEx processEx, bool startup);

        public static event ProcessStoppedEventHandler ProcessStopped;
        public delegate void ProcessStoppedEventHandler(ProcessEx processEx);

        static ProcessManager()
        {
            MonitorTimer = new Timer(1000);
            MonitorTimer.Elapsed += MonitorHelper;

            // hook: on process halt
            stopWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            stopWatch.EventArrived += new EventArrivedEventHandler(ProcessHalted);

            // hook: on window foregroud
            listener = new WinEventProc(EventCallback);
            winHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, listener, 0, 0, WINEVENT_OUTOFCONTEXT);
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

            // hook: on process stop
            stopWatch.Start();

            // list all current processes
            EnumWindows(new WindowEnumCallback(AddWnd), 0);

            // get current foreground process
            IntPtr hWnd = GetforegroundWindow();
            EventCallback((IntPtr)0, 0, hWnd, 0, 0, 0, 0);
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            // stop processes monitor
            MonitorTimer.Elapsed -= MonitorHelper;
            MonitorTimer.Stop();

            stopWatch.Stop();
            Automation.RemoveAllEventHandlers();

            UnhookWinEvent(winHook);
        }

        public static ProcessEx GetForegroundProcess()
        {
            return foregroundProcess;
        }

        public static ProcessEx GetSuspendedProcess()
        {
            return Processes.Values.Where(item => !item.IsIgnored && item.IsSuspended()).FirstOrDefault();
        }

        public static List<ProcessEx> GetProcesses()
        {
            return Processes.Values.ToList();
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
                if (element != null)
                {
                    IntPtr hWnd = (IntPtr)element.Current.NativeWindowHandle;
                    ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd).Process;

                    if (processInfo == null)
                        return;

                    Process proc = Process.GetProcessById((int)processInfo.ProcessId);
                    ProcessCreated(proc, element.Current.NativeWindowHandle);
                }
            }
            catch (Exception)
            {
            }
        }

        private static bool AddWnd(IntPtr hWnd, int lparam)
        {
            if (IsWindowVisible((int)hWnd))
            {
                ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd).Process;

                Process proc = Process.GetProcessById((int)processInfo.ProcessId);
                ProcessCreated(proc, (int)hWnd, true);
            }
            return true;
        }

        private static void EventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime)
        {
            ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd).Process;

            if (processInfo == null)
                return;

            Process proc = Process.GetProcessById((int)processInfo.ProcessId);
            int procId = proc.Id;

            string path = ProcessUtils.GetPathToApp(proc);
            string exec = System.IO.Path.GetFileName(path);

            bool self = IsSelf(exec, path);

            if (self)
                return;

            // save previous process (if exists)
            if (foregroundProcess != null)
                backgroundProcess = foregroundProcess;

            if (Processes.ContainsKey(procId))
                foregroundProcess = Processes[procId];
            else
                foregroundProcess = new ProcessEx(proc)
                {
                    Name = exec,
                    Executable = exec,
                    Path = path,
                    IsIgnored = !IsValid(exec, path)
                };

            // update main window handle
            foregroundProcess.MainWindowHandle = hWnd;

            LogManager.LogDebug("{0} now has the foreground", foregroundProcess.Name);

            ForegroundChanged?.Invoke(foregroundProcess, backgroundProcess);
        }

        private static void MonitorHelper(object? sender, EventArgs e)
        {
            if (Monitor.TryEnter(updateLock))
            {
                foreach (ProcessEx proc in Processes.Values)
                    proc.Refresh();

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
            catch (Exception) { }
        }

        private static void ProcessHalted(int processId)
        {
            if (Processes.ContainsKey(processId))
            {
                ProcessEx processEx = Processes[processId];

                Processes.TryRemove(new KeyValuePair<int, ProcessEx>(processId, processEx));

                ProcessStopped?.Invoke(processEx);

                LogManager.LogDebug("Process halted: {0}", processEx.Name);
            }

            if (foregroundProcess != null && processId == foregroundProcess.Id)
                foregroundProcess = null;
        }

        private static void ProcessCreated(Process proc, int NativeWindowHandle = 0, bool startup = false)
        {
            try
            {
                // process has exited on arrival
                if (proc.HasExited)
                    return;

                string path = ProcessUtils.GetPathToApp(proc);
                string exec = System.IO.Path.GetFileName(path);

                if (!Processes.ContainsKey(proc.Id))
                {
                    ProcessEx processEx = new ProcessEx(proc)
                    {
                        Name = exec,
                        Executable = exec,
                        Path = path,
                        MainWindowHandle = NativeWindowHandle != 0 ? (IntPtr)NativeWindowHandle : proc.MainWindowHandle,
                        IsIgnored = !IsValid(exec, path)
                    };

                    processEx.ChildProcessCreated += ChildProcessCreated;

                    Processes.TryAdd(processEx.Id, processEx);

                    if (processEx.IsIgnored)
                        return;

                    // raise event
                    ProcessStarted?.Invoke(processEx, startup);

                    LogManager.LogDebug("Process created: {0}", proc.ProcessName);
                }
            }
            catch (Exception)
            {
                // process has too high elevation
                return;
            }
        }

        private static void ChildProcessCreated(ProcessEx parent, int Id)
        {
            EnergyManager.ToggleEfficiencyMode(Id, parent.EcoQoS, parent);
        }

        private static bool IsValid(string exec, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.Contains(Environment.GetEnvironmentVariable("windir"), StringComparison.InvariantCultureIgnoreCase))
                return false;

            // manual filtering
            switch (exec.ToLower())
            {
                case "rw.exe":                  // Used to change TDP
                case "kx.exe":                  // Used to change TDP

#if DEBUG
                case "devenv.exe":              // Visual Studio
#endif

                case "msedge.exe":              // Edge has energy awareness
                case "webviewhost.exe":
                case "taskmgr.exe":
                case "procmon.exe":
                case "procmon64.exe":
                case "widgets.exe":

                // System shell
                case "dwm.exe":
                case "explorer.exe":
                case "shellexperiencehost.exe":
                case "startmenuexperiencehost.exe":
                case "searchhost.exe":
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
                case "radeonsoftware.exe":
                case "monotificationux.exe":

                // handheld companion
                case "handheldcompanion.exe":
                case "controllerservice.exe":
                case "controllerservice.dll":
                    return false;

                default:
                    return true;
            }
        }

        private static bool IsSelf(string exec, string path)
        {
            return exec.ToLower().Equals("handheldcompanion.exe");
        }

        public static void ResumeProcess(ProcessEx processEx)
        {
            // process has exited
            if (processEx.Process.HasExited)
                return;

            ProcessUtils.NtResumeProcess(processEx.Process.Handle);
            foreach (int pId in processEx.Children)
            {
                Process process = Process.GetProcessById(pId);
                ProcessUtils.NtResumeProcess(process.Handle);
            }

            Task.Delay(500);
            ProcessUtils.ShowWindow(processEx.MainWindowHandle, ProcessUtils.SW_RESTORE);
        }

        public static void SuspendProcess(ProcessEx processEx)
        {
            // process has exited
            if (processEx.Process.HasExited)
                return;

            ProcessUtils.ShowWindow(processEx.MainWindowHandle, ProcessUtils.SW_MINIMIZE);
            Task.Delay(500);

            ProcessUtils.NtSuspendProcess(processEx.Process.Handle);
            foreach (int pId in processEx.Children)
            {
                Process process = Process.GetProcessById(pId);
                ProcessUtils.NtSuspendProcess(process.Handle);
            }
        }
    }
}
