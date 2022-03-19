using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Timers;
using Windows.System.Diagnostics;

namespace HandheldCompanion
{
    public struct ProcessDetails
    {
        public uint processId;
        public string processExec;
        public string processPath;
    }

    public class ProcessManager
    {
        // process vars
        private Timer MonitorTimer;
        private ManagementEventWatcher startWatch;
        private ManagementEventWatcher stopWatch;
        private Dictionary<uint, ProcessDetails> CurrentProcesses = new();

        private uint CurrentProcess;
        private object updateLock = new();
        private bool isRunning;

        public event ForegroundChangedEventHandler ForegroundChanged;
        public delegate void ForegroundChangedEventHandler(uint processid, string path, string exec);

        public event ProcessStartedEventHandler ProcessStarted;
        public delegate void ProcessStartedEventHandler(uint processid, string path, string exec);

        public event ProcessStoppedEventHandler ProcessStopped;
        public delegate void ProcessStoppedEventHandler(uint processid, string path, string exec);

        public ProcessManager()
        {
            startWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatch.EventArrived += new EventArrivedEventHandler(startWatch_EventArrived);

            stopWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            stopWatch.EventArrived += new EventArrivedEventHandler(stopWatch_EventArrived);
        }

        public void Start()
        {
            // start processes monitor
            MonitorTimer = new Timer(1000) { AutoReset = true };
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

        private void MonitorHelper(object? sender, System.Timers.ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                uint processId;
                string exec = string.Empty;
                string path = string.Empty;

                ProcessDiagnosticInfo process = new ProcessUtils.FindHostedProcess().Process;
                if (process == null)
                    return;

                processId = process.ProcessId;

                if (processId != CurrentProcess)
                {
                    Process proc = Process.GetProcessById((int)processId);
                    path = ProcessUtils.GetPathToApp(proc);
                    exec = process.ExecutableFileName;

                    ForegroundChanged?.Invoke(processId, path, exec);

                    CurrentProcess = processId;
                }
            }
        }

        void stopWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;

                if (CurrentProcesses.ContainsKey(processId))
                {
                    ProcessDetails proc = CurrentProcesses[processId];
                    CurrentProcesses.Remove(processId);

                    ProcessStopped?.Invoke(proc.processId, proc.processPath, proc.processExec);
                }
            }
            catch (Exception ex) { }

            Console.WriteLine("Process stopped: {0}", e.NewEvent.Properties["ProcessName"].Value);
        }

        void startWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;
                Process proc = Process.GetProcessById((int)processId);
                string path = ProcessUtils.GetPathToApp(proc);
                string exec = Path.GetFileName(path);

                ProcessDetails details = new ProcessDetails()
                {
                    processId = processId,
                    processExec = exec,
                    processPath = path
                };

                if (!CurrentProcesses.ContainsKey(processId))
                    CurrentProcesses.Add(processId, details);

                ProcessStarted?.Invoke(processId, path, exec);
            }
            catch (Exception ex) { }

            Console.WriteLine("Process started: {0}", e.NewEvent.Properties["ProcessName"].Value);
        }
    }
}
