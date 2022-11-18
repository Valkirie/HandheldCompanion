using ControllerCommon.Utils;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerCommon.Managers
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
        Paused = 7,
        Uninstalled = 8
    }

    public class ServiceManager : Manager
    {
        private string ServiceName;
        private string DisplayName;
        private string Description;
        private bool Initialized;

        private ServiceController controller;
        public ServiceControllerStatus status = ServiceControllerStatus.None;
        private int prevStatus, prevType = -1;
        private ServiceControllerStatus nextStatus;
        public ServiceStartMode type = ServiceStartMode.Disabled;

        private Process process;

        private Timer MonitorTimer;
        private object updateLock = new();

        public event ReadyEventHandler Ready;
        public delegate void ReadyEventHandler();

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(ServiceControllerStatus status, int mode);

        public event StartFailedEventHandler StartFailed;
        public delegate void StartFailedEventHandler(ServiceControllerStatus status, string message);

        public event StopFailedEventHandler StopFailed;
        public delegate void StopFailedEventHandler(ServiceControllerStatus status);

        public ServiceManager(string name, string display, string description)
        {
            this.ServiceName = name;
            this.DisplayName = display;
            this.Description = description;

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

        public override void Start()
        {
            MonitorTimer.Elapsed += MonitorHelper;

            base.Start();
        }

        public override void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            MonitorTimer.Elapsed -= MonitorHelper;
            MonitorTimer = null;

            base.Stop();
        }

        public bool Exists()
        {
            try
            {
                process.StartInfo.Arguments = $"interrogate {ServiceName}";
                process.Start();
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
                string error = CommonUtils.Between(output, "FAILED ", ":");

                switch (error)
                {
                    case "1060":
                        return false;
                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Service manager returned error: {0}", ex.Message);
            }

            return false;
        }

        private void MonitorHelper(object sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(updateLock))
            {
                try
                {
                    // refresh service status
                    controller.Refresh();

                    // check if service is installed
                    if (CheckServiceInstalled(ServiceName))
                    {
                        if (!string.IsNullOrEmpty(controller.ServiceName))
                        {
                            status = (ServiceControllerStatus)controller.Status;
                            type = controller.StartType;
                        }
                    }
                    else
                    {
                        status = ServiceControllerStatus.Uninstalled;
                        type = ServiceStartMode.Disabled;
                    }
                }
                catch (Exception ex)
                {
                    status = ServiceControllerStatus.None;
                    type = ServiceStartMode.Disabled;
                }

                if (prevStatus != (int)status || prevType != (int)type || nextStatus != 0)
                {
                    Updated?.Invoke(status, (int)type);
                    nextStatus = ServiceControllerStatus.None;
                    LogManager.LogInformation("Controller Service status has changed to: {0}", status.ToString());
                }

                prevStatus = (int)status;
                prevType = (int)type;

                if (!Initialized)
                {
                    Ready?.Invoke();
                    Initialized = true;
                }

                Monitor.Exit(updateLock);
            }
        }

        public bool CheckServiceInstalled(string serviceToFind)
        {
            ServiceController[] servicelist = ServiceController.GetServices();
            foreach (ServiceController service in servicelist)
            {
                if (service.ServiceName == serviceToFind)
                    return true;
            }
            return false;
        }

        public void CreateService(string path)
        {
            Updated?.Invoke(ServiceControllerStatus.StartPending, -1);
            nextStatus = ServiceControllerStatus.StartPending;

            try
            {
                process.StartInfo.Arguments = $"create {ServiceName} binpath= \"{path}\" start= \"demand\" DisplayName= \"{DisplayName}\"";
                process.Start();
                process.WaitForExit();

                process.StartInfo.Arguments = $"description {ServiceName} \"{Description}\"";
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Service manager returned error: {0}", ex.Message);
            }
        }

        public void DeleteService()
        {
            Updated?.Invoke(ServiceControllerStatus.StopPending, -1);
            nextStatus = ServiceControllerStatus.StopPending;

            process.StartInfo.Arguments = $"delete {ServiceName}";
            process.Start();
            process.WaitForExit();
        }

        private int StartTentative;
        public async Task StartServiceAsync()
        {
            if (type == ServiceStartMode.Disabled)
                return;

            if (status == ServiceControllerStatus.Running)
                return;

            while (!Initialized)
                await Task.Delay(1000);

            while (status != ServiceControllerStatus.Running && status != ServiceControllerStatus.StartPending)
            {
                Updated?.Invoke(ServiceControllerStatus.StartPending, -1);

                try
                {
                    controller.Refresh();
                    switch (controller.Status)
                    {
                        case System.ServiceProcess.ServiceControllerStatus.Running:
                        case System.ServiceProcess.ServiceControllerStatus.StartPending:
                            break;
                        default:
                            controller.Start();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    await Task.Delay(2000);
                    LogManager.LogError("Service manager returned error: {0}", ex.Message);
                    StartTentative++;

                    // exit loop
                    if (StartTentative == 3)
                    {
                        nextStatus = ServiceControllerStatus.Failed;
                        StartFailed?.Invoke(status, ex.Message);
                        break;
                    }
                }
            }

            StartTentative = 0;
            return;
        }

        private int StopTentative;
        public async Task StopServiceAsync()
        {
            if (status != ServiceControllerStatus.Running)
                return;

            while (status != ServiceControllerStatus.Stopped && status != ServiceControllerStatus.StopPending)
            {
                Updated?.Invoke(ServiceControllerStatus.StopPending, -1);

                try
                {
                    controller.Refresh();
                    switch (controller.Status)
                    {
                        case System.ServiceProcess.ServiceControllerStatus.Stopped:
                        case System.ServiceProcess.ServiceControllerStatus.StopPending:
                            break;
                        default:
                            controller.Stop();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    await Task.Delay(2000);
                    LogManager.LogError("Service manager returned error: {0}", ex.Message);
                    StopTentative++;

                    // exit loop
                    if (StopTentative == 3)
                    {
                        nextStatus = ServiceControllerStatus.Failed;
                        StopFailed?.Invoke(status);
                        break;
                    }
                }
            }

            StopTentative = 0;
            return;
        }

        public void SetStartType(ServiceStartMode mode)
        {
            ServiceHelper.ChangeStartMode(controller, mode);
        }
    }
}
