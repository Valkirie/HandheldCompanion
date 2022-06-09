using ControllerCommon.Managers;
using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.System.Diagnostics;

namespace HandheldCompanion.Managers
{
    public class ProcessEx
    {
        public Process Process;
        public uint Id;
        public string Executable;
        public string Path;
        public bool Status; // suspended / resumed
        public IntPtr MainHandle;

        // UI vars
        public Border updateBorder;
        public Grid updateGrid;
        public TextBlock updateFilename;
        public TextBlock updatePercentage;
        public Button updateDownload;
        public Button updateInstall;

        public Border GetBorder()
        {
            return updateBorder;
        }

        public void Draw()
        {
            updateBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Background = (Brush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
                Tag = Executable
            };

            // Create Grid
            updateGrid = new();

            // Define the Columns
            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(5, GridUnitType.Star),
                MinWidth = 200
            };
            updateGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(3, GridUnitType.Star),
                MinWidth = 120
            };
            updateGrid.ColumnDefinitions.Add(colDef2);

            // Create TextBlock
            updateFilename = new TextBlock()
            {
                FontSize = 14,
                Text = Executable,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(updateFilename, 0);
            updateGrid.Children.Add(updateFilename);

            // Create TextBlock
            updatePercentage = new TextBlock()
            {
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(updatePercentage, 1);
            updateGrid.Children.Add(updatePercentage);

            // Create Download Button
            updateDownload = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_Download,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Grid.SetColumn(updateDownload, 1);
            updateGrid.Children.Add(updateDownload);

            // Create Install Button
            updateInstall = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_InstallNow,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };

            Grid.SetColumn(updateInstall, 1);
            updateGrid.Children.Add(updateInstall);

            updateBorder.Child = updateGrid;
        }
    }

    public class ProcessManager
    {
        // process vars
        private Timer MonitorTimer;
        private ManagementEventWatcher startWatch;
        private ManagementEventWatcher stopWatch;
        private Dictionary<uint, ProcessEx> CurrentProcesses = new();

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
            // start processes monitor
            MonitorTimer = new Timer(250) { AutoReset = true };
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

            // stop processes monitor
            MonitorTimer.Elapsed -= MonitorHelper;
            MonitorTimer.Stop();

            startWatch.Stop();
            stopWatch.Stop();
        }

        private void MonitorHelper(object? sender, ElapsedEventArgs e)
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
                    Process proc = Process.GetProcessById((int)processId);
                    path = ProcessUtils.GetPathToApp(proc);
                    exec = process.ExecutableFileName;

                    ProcessEx processEx = new ProcessEx()
                    {
                        Process = proc,
                        Id = processId,
                        Executable = exec,
                        Path = path
                    };

                    ForegroundChanged?.Invoke(processEx);

                    CurrentProcess = processId;
                }
            }
        }

        void ProcessHalted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;

                if (CurrentProcesses.ContainsKey(processId))
                {
                    ProcessEx processEx = CurrentProcesses[processId];
                    CurrentProcesses.Remove(processId);

                    ProcessStopped?.Invoke(processEx);
                }
            }
            catch (Exception) { }

            LogManager.LogDebug("Process halted: {0}", e.NewEvent.Properties["ProcessName"].Value);
        }

        void ProcessCreated(object sender, EventArrivedEventArgs e)
        {
            try
            {
                uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;
                Process proc = Process.GetProcessById((int)processId);
                string path = ProcessUtils.GetPathToApp(proc);
                string exec = Path.GetFileName(path);

                ProcessEx processEx = new ProcessEx()
                {
                    Process = proc,
                    Id = processId,
                    Executable = exec,
                    Path = path
                };

                if (exec == "")
                    return;

                if (!CurrentProcesses.ContainsKey(processId))
                    CurrentProcesses.Add(processId, processEx);

                ProcessStarted?.Invoke(processEx);
            }
            catch (Exception) { }

            LogManager.LogDebug("Process created: {0}", e.NewEvent.Properties["ProcessName"].Value);
        }
    }
}
