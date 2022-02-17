using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerCommon
{
    public class ServiceManager
    {
        private string name;
        private string display;
        private string description;

        private ServiceController controller;
        public ServiceControllerStatus status;
        private int prevStatus, prevType = -1;
        private ServiceControllerStatus nextStatus;
        private ServiceStartMode type;

        private Process process;

        private Timer MonitorTimer;
        private object updateLock = new();

        private readonly ILogger logger;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(ServiceControllerStatus status, ServiceStartMode mode);

        public ServiceManager(string name, string display, string description, ILogger logger)
        {
            this.logger = logger;

            this.name = name;
            this.display = display;
            this.description = description;

            controller = new ServiceController(name);

            process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = @"C:\Windows\system32\sc.exe",
                    Verb = "runas"
                }
            };

            // monitor service
            MonitorTimer = new Timer(1000) { Enabled = true, AutoReset = true };
        }

        public void Start()
        {
            MonitorTimer.Elapsed += MonitorHelper;
        }

        public void Stop()
        {
            MonitorTimer.Elapsed -= MonitorHelper;
            MonitorTimer = null;
        }

        private void MonitorHelper(object sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                // refresh service status
                try
                {
                    controller.Refresh();
                    status = controller.Status;
                    type = controller.StartType;
                }
                catch (Exception)
                {
                    status = 0;
                    type = ServiceStartMode.Disabled;
                }

                if (prevStatus != (int)status || prevType != (int)type || nextStatus != 0)
                {
                    Updated?.Invoke(status, type);
                    nextStatus = 0;
                    logger.LogInformation("Controller Service status has changed to: {0}", status.ToString());
                }

                prevStatus = (int)status;
                prevType = (int)type;
            }
        }

        public void CreateService(string path)
        {
            Updated?.Invoke(ServiceControllerStatus.StartPending, ServiceStartMode.Disabled);
            nextStatus = ServiceControllerStatus.StartPending;

            try
            {
                process.StartInfo.Arguments = $"create {name} binpath= \"{path}\" start= \"auto\" DisplayName= \"{display}\"";
                process.Start();
                process.WaitForExit();

                process.StartInfo.Arguments = $"description {name} \"{description}\"";
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                logger.LogError("Service manager returned error: {0}", ex.Message);
            }
        }

        public void DeleteService()
        {
            Updated?.Invoke(ServiceControllerStatus.StopPending, ServiceStartMode.Disabled);
            nextStatus = ServiceControllerStatus.StopPending;

            process.StartInfo.Arguments = $"delete {name}";
            process.Start();
            process.WaitForExit();
        }

        public void StartService()
        {
            Updated?.Invoke(ServiceControllerStatus.StartPending, ServiceStartMode.Disabled);
            nextStatus = ServiceControllerStatus.Running;

            try
            {
                if (type != ServiceStartMode.Disabled)
                    controller.Start();
            }
            catch (Exception ex)
            {
                logger.LogError("Service manager returned error: {0}", ex.Message);
            }
        }

        public void StopService()
        {
            Updated?.Invoke(ServiceControllerStatus.StopPending, ServiceStartMode.Disabled);
            nextStatus = ServiceControllerStatus.Stopped;

            try
            {
                if (status == ServiceControllerStatus.Running)
                    controller.Stop();
            }
            catch (Exception ex)
            {
                logger.LogError("Service manager returned error: {0}", ex.Message);
            }
        }

        public void SetStartType(ServiceStartMode mode)
        {
            ServiceHelper.ChangeStartMode(controller, mode);
        }
    }
}
