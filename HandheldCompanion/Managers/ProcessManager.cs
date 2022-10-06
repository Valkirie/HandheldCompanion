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
using System.Windows.Automation;
using Windows.System.Diagnostics;
using static ControllerCommon.WinAPI;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public class ProcessManager : Manager
    {
        #region imports
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, int idProcess, int idThread, uint dwflags);
        [DllImport("user32.dll")]
        internal static extern int UnhookWinEvent(IntPtr hWinEventHook);
        internal delegate void WinEventProc(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime);

        const uint WINEVENT_OUTOFCONTEXT = 0;
        const uint EVENT_SYSTEM_FOREGROUND = 3;
        private IntPtr winHook;
        private WinEventProc listener;

        public delegate bool WindowEnumCallback(IntPtr hwnd, int lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(WindowEnumCallback lpEnumFunc, int lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(int h);
        #endregion

        // process vars
        private Timer MonitorTimer;
        private ManagementEventWatcher stopWatch;

        private ConcurrentDictionary<uint, ProcessEx> Processes = new();
        private ProcessEx foregroundProcess;
        private ProcessEx backgroundProcess;

        private object updateLock = new();

        public event ForegroundChangedEventHandler ForegroundChanged;
        public delegate void ForegroundChangedEventHandler(ProcessEx processEx, ProcessEx backgroundEx);

        public event ProcessStartedEventHandler ProcessStarted;
        public delegate void ProcessStartedEventHandler(ProcessEx processEx, bool startup);

        public event ProcessStoppedEventHandler ProcessStopped;
        public delegate void ProcessStoppedEventHandler(ProcessEx processEx);

        public ProcessManager() : base()
        {
            MonitorTimer = new Timer(1000);
            MonitorTimer.Elapsed += MonitorHelper;

            stopWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            stopWatch.EventArrived += new EventArrivedEventHandler(ProcessHalted);

            listener = new WinEventProc(EventCallback);// hook: on window foregroud
            winHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, listener, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        public override void Start()
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

            base.Start();
        }

        public override void Stop()
        {
            if (!IsInitialized)
                return;

            // stop processes monitor
            MonitorTimer.Elapsed -= MonitorHelper;
            MonitorTimer.Stop();

            stopWatch.Stop();
            Automation.RemoveAllEventHandlers();

            UnhookWinEvent(winHook);

            base.Stop();
        }

        public ProcessEx GetForegroundProcess()
        {
            return foregroundProcess;
        }

        public List<ProcessEx> GetProcesses()
        {
            return Processes.Values.ToList();
        }

        private void OnWindowOpened(object sender, AutomationEventArgs automationEventArgs)
        {
            try
            {
                var element = sender as AutomationElement;
                if (element != null)
                {
                    IntPtr hWnd = (IntPtr)element.Current.NativeWindowHandle;
                    ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd).Process;

                    Process proc = Process.GetProcessById((int)processInfo.ProcessId);
                    ProcessCreated(proc, element.Current.NativeWindowHandle);
                }
            }
            catch (Exception)
            {
            }
        }

        private bool AddWnd(IntPtr hWnd, int lparam)
        {
            if (IsWindowVisible((int)hWnd))
            {
                ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd).Process;

                Process proc = Process.GetProcessById((int)processInfo.ProcessId);
                ProcessCreated(proc, (int)hWnd, true);
            }
            return true;
        }

        private void EventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime)
        {
            ProcessDiagnosticInfo processInfo = new ProcessUtils.FindHostedProcess(hWnd).Process;

            if (processInfo == null)
                return;

            Process proc = Process.GetProcessById((int)processInfo.ProcessId);
            uint procId = (uint)proc.Id;

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
                    Bypassed = !IsValid(exec, path)
                };

            // update main window handle
            foregroundProcess.MainWindowHandle = hWnd;

            LogManager.LogDebug("{0} now has the foreground", foregroundProcess.Name);

            ForegroundChanged?.Invoke(foregroundProcess, backgroundProcess);
        }

        private void MonitorHelper(object? sender, EventArgs e)
        {
            lock (updateLock)
            {
                foreach (ProcessEx proc in Processes.Values)
                    proc.Timer_Tick(sender, e);
            }
        }

        void ProcessHalted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;
                ProcessHalted(processId);
            }
            catch (Exception) { }
        }

        void ProcessHalted(uint processId)
        {
            if (Processes.ContainsKey(processId))
            {
                ProcessEx processEx = Processes[processId];

                Processes.TryRemove(new KeyValuePair<uint, ProcessEx>(processId, processEx));

                ProcessStopped?.Invoke(processEx);

                LogManager.LogDebug("Process halted: {0}", processEx.Name);
            }

            if (foregroundProcess != null && processId == foregroundProcess.Id)
                foregroundProcess = null;
        }

        private void ProcessCreated(Process proc, int NativeWindowHandle = 0, bool startup = false)
        {
            try
            {
                // process has exited on arrival
                if (proc.HasExited)
                    return;

                string path = ProcessUtils.GetPathToApp(proc);
                string exec = System.IO.Path.GetFileName(path);

                if (!Processes.ContainsKey((uint)proc.Id))
                {
                    ProcessEx processEx = new ProcessEx(proc)
                    {
                        Name = exec,
                        Executable = exec,
                        Path = path,
                        MainWindowHandle = NativeWindowHandle != 0 ? (IntPtr)NativeWindowHandle : proc.MainWindowHandle,
                        Bypassed = !IsValid(exec, path)
                    };

                    Processes.TryAdd(processEx.Id, processEx);

                    if (processEx.Bypassed)
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

        private bool IsValid(string exec, string path)
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

                // handheld companion
                case "handheldcompanion.exe":
                case "controllerservice.exe":
                case "controllerservice.dll":
                    return false;

                default:
                    return true;
            }
        }

        private bool IsSelf(string exec, string path)
        {
            return exec.ToLower().Equals("handheldcompanion.exe");
        }
    }
}
