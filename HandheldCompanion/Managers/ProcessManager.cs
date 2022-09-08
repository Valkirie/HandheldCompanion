using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ModernWpf.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.System.Diagnostics;
using static ControllerCommon.Utils.ProcessUtils;
using static ControllerCommon.WinAPI;
using static HandheldCompanion.Managers.EnergyManager;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using ThreadState = System.Diagnostics.ThreadState;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public class ProcessEx
    {
        #region imports
        [DllImport("ntdll.dll", EntryPoint = "NtSuspendProcess", SetLastError = true, ExactSpelling = false)]
        private static extern UIntPtr NtSuspendProcess(IntPtr processHandle);
        [DllImport("ntdll.dll", EntryPoint = "NtResumeProcess", SetLastError = true, ExactSpelling = false)]
        private static extern UIntPtr NtResumeProcess(IntPtr processHandle);

        public enum ShowWindowCommands : int
        {
            Hide = 0,
            Normal = 1,
            Minimized = 2,
            Maximized = 3,
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        #endregion

        public Process Process;
        public IntPtr MainWindowHandle;
        public object processInfo;
        public QualityOfServiceLevel QoL;

        public uint Id;
        public string Name;
        public string Executable;
        public string Path;
        public bool Bypassed;

        private ThreadWaitReason threadWaitReason = ThreadWaitReason.UserRequest;

        // UI vars
        public Border processBorder;
        public Grid processGrid;
        public TextBlock processName;
        public Image processIcon;
        public Button processSuspend;
        public Button processResume;
        public FontIcon processQoS;

        public ProcessEx(Process process)
        {
            this.Process = process;
            this.Id = (uint)process.Id;
        }

        public void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                Process.Refresh();

                if (Process.HasExited)
                    return;

                var processThread = Process.Threads[0];

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (MainWindowHandle != IntPtr.Zero)
                    {
                        if (processBorder is null)
                            return;

                        processBorder.Visibility = Visibility.Visible;
                        string MainWindowTitle = ProcessUtils.GetWindowTitle(MainWindowHandle);
                        if (!string.IsNullOrEmpty(MainWindowTitle))
                            processName.Text = MainWindowTitle;
                    }
                    else
                        processBorder.Visibility = Visibility.Collapsed;

                    // manage process state
                    switch (processThread.ThreadState)
                    {
                        case ThreadState.Wait:

                            if (processThread.WaitReason != threadWaitReason)
                            {
                                switch (processThread.WaitReason)
                                {
                                    case ThreadWaitReason.Suspended:
                                        processSuspend.Visibility = Visibility.Collapsed;
                                        processResume.Visibility = Visibility.Visible;

                                        processResume.IsEnabled = true;
                                        break;
                                    default:
                                        processSuspend.Visibility = Visibility.Visible;
                                        processResume.Visibility = Visibility.Collapsed;

                                        processSuspend.IsEnabled = true;
                                        break;
                                }
                            }

                            threadWaitReason = processThread.WaitReason;
                            break;
                        default:
                            threadWaitReason = ThreadWaitReason.UserRequest;
                            break;
                    }

                    // manage process throttling
                    // var result = EnergyManager.GetProcessInfo(Process.Handle, WinAPI.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, out processInfo);
                    switch (QoL)
                    {
                        // HighQoS
                        case QualityOfServiceLevel.Default:
                        case QualityOfServiceLevel.High:
                            processQoS.Glyph = "\uE945";
                            processQoS.Foreground = new SolidColorBrush(Color.FromRgb(25, 144, 161));
                            break;
                        // EcoQoS
                        case QualityOfServiceLevel.Eco:
                            processQoS.Glyph = "\uE8BE";
                            processQoS.Foreground = new SolidColorBrush(Color.FromRgb(193, 127, 48));
                            break;
                    }
                }), DispatcherPriority.ContextIdle);
            }
            catch (Exception) { }
        }

        public Border GetBorder()
        {
            return processBorder;
        }

        public void Draw()
        {
            if (processBorder != null)
                return;

            processBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Visibility = Visibility.Collapsed,
                Tag = Name
            };
            processBorder.SetResourceReference(Control.BackgroundProperty, "LayerOnMicaBaseAltFillColorDefaultBrush");

            // Create Grid
            processGrid = new();

            // Define the Columns
            ColumnDefinition colDef0 = new ColumnDefinition()
            {
                Width = new GridLength(32, GridUnitType.Pixel),
                MinWidth = 32
            };
            processGrid.ColumnDefinitions.Add(colDef0);

            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(8, GridUnitType.Star)
            };
            processGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 120
            };
            processGrid.ColumnDefinitions.Add(colDef2);

            ColumnDefinition colDef3 = new ColumnDefinition()
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 32
            };
            processGrid.ColumnDefinitions.Add(colDef3);

            // Create PersonPicture
            var icon = Icon.ExtractAssociatedIcon(Path);
            processIcon = new Image()
            {
                Height = 32,
                Width = 32,
                Source = icon.ToImageSource()
            };
            Grid.SetColumn(processIcon, 0);
            processGrid.Children.Add(processIcon);

            // Create SimpleStackPanel
            var StackPanel = new SimpleStackPanel()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            // Create TextBlock(s)
            processName = new TextBlock()
            {
                FontSize = 14,
                Text = Name,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            processName.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            StackPanel.Children.Add(processName);

            var processExecutable = new TextBlock()
            {
                FontSize = 12,
                Text = Executable,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            processExecutable.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
            StackPanel.Children.Add(processExecutable);

            Grid.SetColumn(StackPanel, 1);
            processGrid.Children.Add(StackPanel);

            // Create Download Button
            processSuspend = new Button()
            {
                FontSize = 14,
                Content = "Suspend", // localize me !
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                // Style = Application.Current.FindResource("DefaultButtonStyle") as Style
            };
            processSuspend.Click += ProcessSuspend_Click;

            Grid.SetColumn(processSuspend, 2);
            processGrid.Children.Add(processSuspend);

            // Create Install Button
            processResume = new Button()
            {
                FontSize = 14,
                Content = "Resume", // localize me !
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed,
                Style = Application.Current.FindResource("AccentButtonStyle") as Style
            };
            processResume.Click += ProcessResume_Click;

            Grid.SetColumn(processResume, 2);
            processGrid.Children.Add(processResume);

            // Create EcoQos indicator
            processQoS = new FontIcon()
            {
                FontSize = 14,
                Glyph = "\uE945",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            Grid.SetColumn(processQoS, 3);
            processGrid.Children.Add(processQoS);

            processBorder.Child = processGrid;
        }

        private void ProcessResume_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processResume.IsEnabled = false;
                NtResumeProcess(Process.Handle);
                Task.Delay(500);
                ShowWindow(MainWindowHandle, 9);
            }));
        }

        private void ProcessSuspend_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processSuspend.IsEnabled = false;

                ShowWindow(MainWindowHandle, 2);
                Task.Delay(500);
                NtSuspendProcess(Process.Handle);
            }));
        }
    }

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
            string exec = Path.GetFileName(path);

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
                string exec = Path.GetFileName(path);

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
