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
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.System.Diagnostics;
using Brush = System.Windows.Media.Brush;
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
        public uint Id;
        public string Name;
        public string Executable;
        public string Path;

        private Timer Timer;
        private ThreadWaitReason threadWaitReason = ThreadWaitReason.UserRequest;

        // UI vars
        public Border processBorder;
        public Grid processGrid;
        public TextBlock processName;
        public Image processIcon;
        public Button processSuspend;
        public Button processResume;

        public ProcessEx(Process process)
        {
            this.Process = process;

            this.Id = (uint)process.Id;

            Timer = new Timer(1000);
        }

        public void Start()
        {
            Timer.Elapsed += Timer_Tick;
            Timer.Start();
        }

        public void Stop()
        {
            Timer.Elapsed -= Timer_Tick;
            Timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                Process.Refresh();

                var processThread = Process.Threads[0];

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (string.IsNullOrEmpty(Process.MainWindowTitle))
                        processName.Text = Process.ProcessName;
                    else
                        processName.Text = Process.MainWindowTitle;

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
            processBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Background = (Brush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
                Tag = Name
            };

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
                Width = new GridLength(5, GridUnitType.Star),
                MinWidth = 200
            };
            processGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(3, GridUnitType.Star),
                MinWidth = 120
            };
            processGrid.ColumnDefinitions.Add(colDef2);

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
                Text = Process.ProcessName,
                VerticalAlignment = VerticalAlignment.Center
            };
            StackPanel.Children.Add(processName);

            var processExecutable = new TextBlock()
            {
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
                Text = Executable,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
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

            processBorder.Child = processGrid;
        }

        private void ProcessResume_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processResume.IsEnabled = false;
            }));
            
            NtResumeProcess(Process.Handle);
            Thread.Sleep(500); // breathing
            ShowWindow(Process.MainWindowHandle, 9);
        }

        private void ProcessSuspend_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processSuspend.IsEnabled = false;
            }));

            ShowWindow(Process.MainWindowHandle, 2);
            Thread.Sleep(500); // breathing
            NtSuspendProcess(Process.Handle);
        }
    }

    public class ProcessManager
    {
        // process vars
        private Timer MonitorTimer;
        private ManagementEventWatcher startWatch;
        private ManagementEventWatcher stopWatch;
        private ConcurrentDictionary<uint, ProcessEx> CurrentProcesses = new();

        private uint CurrentProcess;
        private object updateLock = new();
        private bool isRunning;

        public event ForegroundChangedEventHandler ForegroundChanged;
        public delegate void ForegroundChangedEventHandler(ProcessEx processEx);

        public event ProcessStartedEventHandler ProcessStarted;
        public delegate void ProcessStartedEventHandler(ProcessEx processEx);

        public event ProcessStoppedEventHandler ProcessStopped;
        public delegate void ProcessStoppedEventHandler(ProcessEx processEx);

        public ProcessManager()
        {
            startWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatch.EventArrived += new EventArrivedEventHandler(ProcessCreated);

            stopWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            stopWatch.EventArrived += new EventArrivedEventHandler(ProcessHalted);
        }

        public void Start()
        {
            // list all current processes
            ListProcess();

            // start processes monitor
            MonitorTimer = new Timer(1000);
            MonitorTimer.Elapsed += MonitorHelper;
            MonitorTimer.Start();

            startWatch.Start();
            stopWatch.Start();

            isRunning = true;
        }

        public void Stop()
        {
            if (!isRunning)
                return;

            foreach(ProcessEx processEx in CurrentProcesses.Values)
                processEx.Stop();

            // stop processes monitor
            MonitorTimer.Elapsed -= MonitorHelper;
            MonitorTimer.Stop();

            startWatch.Stop();
            stopWatch.Stop();
        }

        private void ListProcess()
        {
            Process[] processCollection = Process.GetProcesses();
            foreach (Process proc in processCollection)
                ProcessCreated(proc);
        }

        private void MonitorHelper(object? sender, EventArgs e)
        {
            lock (updateLock)
            {
                uint processId;
                string exec = string.Empty;
                string path = string.Empty;
                string name = string.Empty;

                ProcessDiagnosticInfo process = new ProcessUtils.FindHostedProcess().Process;
                if (process == null)
                    return;

                name = ProcessUtils.GetActiveWindowTitle();
                if (name == "Overlay")
                    return;

                processId = process.ProcessId;

                if (processId != CurrentProcess)
                {
                    if (CurrentProcesses.ContainsKey(processId))
                    {
                        ProcessEx processEx = CurrentProcesses[processId];
                        path = ProcessUtils.GetPathToApp(processEx.Process);

                        LogManager.LogDebug("ActiveWindow Title: {0}, Path: {1}", name, path);

                        ForegroundChanged?.Invoke(processEx);
                        CurrentProcess = processId;
                    }
                }
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
            if (CurrentProcesses.ContainsKey(processId))
            {
                ProcessEx processEx = CurrentProcesses[processId];
                processEx.Stop();

                CurrentProcesses.TryRemove(new KeyValuePair<uint, ProcessEx>(processId, processEx));

                ProcessStopped?.Invoke(processEx);

                LogManager.LogDebug("Process halted: {0}", processEx.Process.ProcessName);
            }
        }

        void ProcessCreated(object sender, EventArrivedEventArgs e)
        {
            try
            {
                uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;
                Process proc = Process.GetProcessById((int)processId);
                ProcessCreated(proc);
            }
            catch(Exception) { }
        }

        void ProcessCreated(Process proc)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                // breating
                Thread.Sleep(1000);

                // no main window
                if (proc.MainWindowHandle == IntPtr.Zero)
                    return;

                string path = ProcessUtils.GetPathToApp(proc);

                // todo : implement proper filtering
                if (string.IsNullOrEmpty(path))
                    return;

                if (path.ToLower().Contains(Environment.GetEnvironmentVariable("windir").ToLower()))
                    return;

                string exec = Path.GetFileName(path);

                ProcessEx processEx = new ProcessEx(proc)
                {
                    Name = exec,
                    Executable = exec,
                    Path = path
                };
                processEx.Start();

                if (!CurrentProcesses.ContainsKey(processEx.Id))
                    CurrentProcesses.TryAdd(processEx.Id, processEx);

                ProcessStarted?.Invoke(processEx);

                LogManager.LogDebug("Process created: {0}", proc.ProcessName);
            }).Start();
        }
    }
}
