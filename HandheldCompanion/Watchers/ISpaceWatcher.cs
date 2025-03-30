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
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                if (executableNames.Any(name => process.ProcessName.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                    yield return process;
            }
        }

        public bool HasProcesses()
        {
            return GetProcesses().Any();
        }

        private void KillProcesses()
        {
            foreach (Process process in GetProcesses())
            {
                if (process.HasExited)
                    continue;

                process.Kill();
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
        protected IEnumerable<ServiceController> GetServices()
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (string serviceName in serviceNames)
            {
                if (services.Any(s => serviceNames.Contains(s.ServiceName)))
                {
                    ServiceController serviceController = new ServiceController(serviceName);
                    yield return serviceController;
                }
            }
        }

        public bool HasRunningServices()
        {
            return GetServices().Any(service => service.Status == ServiceControllerStatus.Running);
        }

        private void DisableServices()
        {
            foreach (ServiceController service in GetServices())
            {
                ServiceUtils.ChangeStartMode(service, ServiceStartMode.Disabled, out string error);

                if (service.Status != ServiceControllerStatus.Stopped)
                    service.Stop();
            }
        }

        private void EnableServices()
        {
            foreach (ServiceController service in GetServices())
            {
                ServiceUtils.ChangeStartMode(service, ServiceStartMode.Automatic, out string error);

                if (service.Status != ServiceControllerStatus.Running)
                    service.Start();
            }
        }
        #endregion
    }
}
