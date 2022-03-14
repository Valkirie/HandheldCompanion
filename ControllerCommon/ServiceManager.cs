using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerCommon
{
    public enum ServiceControllerStatus
    {
        Failed = -1,
        None = 0,
        Stopped = 1,
        StartPending = 2,
        StopPending = 3,
        Running = 4,
        ContinuePending = 5,
        PausePending = 6,
        Paused = 7
    }

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
        public delegate void UpdatedEventHandler(ServiceControllerStatus status, int mode);

        public event StartFailedEventHandler StartFailed;
        public delegate void StartFailedEventHandler(ServiceControllerStatus status);

        public event StopFailedEventHandler StopFailed;
        public delegate void StopFailedEventHandler(ServiceControllerStatus status);

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
                    status = (ServiceControllerStatus)controller.Status;
                    type = controller.StartType;
                }
                catch (Exception)
                {
                    status = ServiceControllerStatus.None;
                    type = ServiceStartMode.Disabled;
                }

                if (prevStatus != (int)status || prevType != (int)type || nextStatus != 0)
                {
                    Updated?.Invoke(status, (int)type);
                    nextStatus = ServiceControllerStatus.None;
                    logger.LogInformation("Controller Service status has changed to: {0}", status.ToString());
                }

                prevStatus = (int)status;
                prevType = (int)type;
            }
        }

        public void CreateService(string path)
        {
            Updated?.Invoke(ServiceControllerStatus.StartPending, -1);
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
            Updated?.Invoke(ServiceControllerStatus.StopPending, -1);
            nextStatus = ServiceControllerStatus.StopPending;

            process.StartInfo.Arguments = $"delete {name}";
            process.Start();
            process.WaitForExit();
        }

        private int StartTentative;
        public async Task StartServiceAsync()
        {
            while (status != ServiceControllerStatus.Running)
            {
                Updated?.Invoke(ServiceControllerStatus.StartPending, -1);

                try
                {
                    if (type != ServiceStartMode.Disabled)
                        controller.Start();
                    StartTentative = 0;
                }
                catch (Exception ex)
                {
                    await Task.Delay(2000);
                    logger.LogError("Service manager returned error: {0}", ex.Message);
                    StartTentative++;

                    // exit loop
                    if (StartTentative == 3)
                    {
                        StartTentative = 0;
                        nextStatus = ServiceControllerStatus.Failed;
                        StartFailed?.Invoke(status);
                        return;
                    }
                }
            }
        }

        private int StopTentative;
        public async Task StopServiceAsync()
        {
            while (status != ServiceControllerStatus.Stopped && status != ServiceControllerStatus.StopPending)
            {
                Updated?.Invoke(ServiceControllerStatus.StopPending, -1);

                try
                {
                    if (status == ServiceControllerStatus.Running)
                        controller.Stop();
                    StopTentative = 0;
                }
                catch (Exception ex)
                {
                    await Task.Delay(2000);
                    logger.LogError("Service manager returned error: {0}", ex.Message);
                    StopTentative++;

                    // exit loop
                    if (StopTentative == 3)
                    {
                        StopTentative = 0;
                        nextStatus = ServiceControllerStatus.Failed;
                        StopFailed?.Invoke(status);
                        return;
                    }
                }
            }
        }

        public void SetStartType(ServiceStartMode mode)
        {
            ServiceHelper.ChangeStartMode(controller, mode);
        }
    }
}
