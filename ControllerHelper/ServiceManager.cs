using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerHelper
{
    public class ServiceManager
    {
        private string name;
        private string display;
        private string description;

        private ServiceController controller;
        private ServiceControllerStatus status;
        private int prevStatus, prevType = -1;
        private ServiceControllerStatus nextStatus;
        private ServiceStartMode type;

        private Process process;

        private Timer MonitorTimer;
        private object updateLock = new();

        private readonly ControllerHelper helper;
        private readonly ILogger logger;

        private AutoResetEvent autoEvent;

        public ServiceManager(string name, ControllerHelper helper, string display, string description, ILogger logger)
        {
            this.helper = helper;
            this.logger = logger;

            this.name = name;
            this.display = display;
            this.description = description;

            controller = new ServiceController(name);
            autoEvent = new AutoResetEvent(false);

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

                if (nextStatus != 0)
                {
                    try
                    {
                        controller.WaitForStatus(nextStatus, TimeSpan.FromSeconds(2));
                    }
                    catch (Exception ex)
                    {
                        nextStatus = 0;
                        prevStatus = 0;
                        prevType = 0;
                        logger.LogError("Service manager returned error: {0}", ex.Message);
                    }
                }

                if (prevStatus != (int)status || prevType != (int)type)
                {
                    helper.UpdateService(status, type);
                    logger.LogInformation("Controller Service status has changed to: {0}", status.ToString());
                    autoEvent.Set();
                }

                prevStatus = (int)status;
                prevType = (int)type;
            }
        }

        public void CreateService(string path)
        {
            autoEvent.WaitOne(1000);

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
            finally
            {
                nextStatus = ServiceControllerStatus.Stopped;
            }
        }

        public void DeleteService()
        {
            autoEvent.WaitOne(1000);

            process.StartInfo.Arguments = $"delete {name}";
            process.Start();
            process.WaitForExit();

            nextStatus = 0;
        }

        public void StartService()
        {
            autoEvent.WaitOne(1000);

            try
            {
                if (type != ServiceStartMode.Disabled)
                    controller.Start();
            }
            catch (Exception ex)
            {
                logger.LogError("Service manager returned error: {0}", ex.Message);
            }
            finally
            {
                nextStatus = ServiceControllerStatus.Running;
            }
        }

        public void StopService()
        {
            autoEvent.WaitOne(1000);

            try
            {
                if (status == ServiceControllerStatus.Running)
                    controller.Stop();
            }
            catch (Exception ex)
            {
                logger.LogError("Service manager returned error: {0}", ex.Message);
            }
            finally
            {
                nextStatus = ServiceControllerStatus.Stopped;
            }
        }

        public void SetStartType(ServiceStartMode mode)
        {
            autoEvent.WaitOne(1000);

            ServiceHelper.ChangeStartMode(controller, mode);
        }
    }
}
