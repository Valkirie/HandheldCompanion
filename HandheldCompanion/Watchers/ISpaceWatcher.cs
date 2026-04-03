using HandheldCompanion.Notifications;
using HandheldCompanion.Utils;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using TaskScheduled = Microsoft.Win32.TaskScheduler.Task;

namespace HandheldCompanion.Watchers
{
    public class ISpaceWatcher
    {
        protected List<string> serviceNames = new();
        protected List<ServiceController> serviceControllers = new();
        protected List<string> taskNames = new();
        protected List<string> executableNames = new();

        public Notification notification;

        protected Timer watchdogTimer;

        protected bool prevStatus = false;
        public event StatusChangedHandler StatusChanged;
        public delegate void StatusChangedHandler(bool enabled);

        public virtual void Start()
        { }

        public virtual void Stop()
        { }

        public virtual void Enable()
        {
            Stop();

            EnableTasks();
            EnableServices();

            Start();
        }

        public virtual void Disable()
        {
            Stop();

            DisableTasks();
            DisableServices();
            KillProcesses();

            Start();
        }

        protected void UpdateStatus(bool enabled)
        {
            StatusChanged?.Invoke(enabled);
        }

        #region executables
        protected IEnumerable<Process> GetProcesses()
        {
            foreach (string name in executableNames)
            {
                Process[] matches = Process.GetProcessesByName(name);
                foreach (Process process in matches)
                    yield return process;
            }
        }

        public bool HasProcesses()
        {
            foreach (string name in executableNames)
            {
                Process[] matches = Process.GetProcessesByName(name);
                bool found = matches.Length > 0;
                foreach (Process p in matches)
                    p.Dispose();
                if (found)
                    return true;
            }
            return false;
        }

        private void KillProcesses()
        {
            foreach (Process process in GetProcesses())
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        #endregion

        #region tasks
        protected IEnumerable<TaskScheduled> GetTasks()
        {
            using (TaskService taskService = new TaskService())
            {
                foreach (string task in taskNames)
                {
                    TaskScheduled taskScheduled = taskService.GetTask(task);
                    if (taskScheduled != null)
                        yield return taskScheduled;
                }
            }
        }

        public bool HasEnabledTasks()
        {
            return GetTasks().Any(task => task.Enabled);
        }

        private void EnableTasks()
        {
            foreach (TaskScheduled task in GetTasks())
            {
                if (!task.Enabled)
                {
                    task.Enabled = true;
                    task.Run();
                }
            }
        }

        private void DisableTasks()
        {
            foreach (TaskScheduled task in GetTasks())
            {
                if (task.Enabled)
                {
                    task.Stop();
                    task.Enabled = false;
                }
            }
        }
        #endregion

        #region services
        protected List<ServiceController> GetServices()
        {
            List<ServiceController> result = new();
            foreach (string serviceName in serviceNames)
            {
                ServiceController sc = new ServiceController(serviceName);
                try
                {
                    // Accessing Status will throw if the service does not exist
                    _ = sc.Status;
                    result.Add(sc);
                }
                catch (InvalidOperationException)
                {
                    sc.Dispose();
                }
            }
            return result;
        }

        public bool HasRunningServices()
        {
            foreach (string serviceName in serviceNames)
            {
                ServiceController sc = new ServiceController(serviceName);
                try
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return true;
                }
                catch (InvalidOperationException)
                {
                    // service does not exist
                }
                finally
                {
                    sc.Dispose();
                }
            }
            return false;
        }

        private void DisableServices()
        {
            foreach (ServiceController service in GetServices())
            {
                try
                {
                    ServiceUtils.ChangeStartMode(service, ServiceStartMode.Disabled, out string error);

                    if (service.Status != ServiceControllerStatus.Stopped)
                        service.Stop();
                }
                finally
                {
                    service.Dispose();
                }
            }
        }

        private void EnableServices()
        {
            foreach (ServiceController service in GetServices())
            {
                try
                {
                    ServiceUtils.ChangeStartMode(service, ServiceStartMode.Automatic, out string error);

                    if (service.Status != ServiceControllerStatus.Running)
                        service.Start();
                }
                finally
                {
                    service.Dispose();
                }
            }
        }
        #endregion
    }
}
