using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ControllerCommon.Utils;
using Timer = System.Timers.Timer;

namespace ControllerCommon.Managers;

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
    public delegate void StartFailedEventHandler(ServiceControllerStatus status, string message);

    public delegate void StopFailedEventHandler(ServiceControllerStatus status);

    public delegate void UpdatedEventHandler(ServiceControllerStatus status, int mode);

    private readonly Timer MonitorTimer = new(2000) { Enabled = true, AutoReset = true };
    private readonly object updateLock = new();

    private readonly ServiceController controller;
    private readonly string Description;
    private readonly string DisplayName;
    private ServiceControllerStatus nextStatus;
    private int prevStatus, prevType = -1;

    private readonly Process process;
    private readonly string ServiceName;

    private int StartTentative;
    public ServiceControllerStatus status = ServiceControllerStatus.None;

    private int StopTentative;
    public ServiceStartMode type = ServiceStartMode.Disabled;

    public ServiceManager(string name, string display, string description)
    {
        ServiceName = name;
        DisplayName = display;
        Description = description;

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
    }

    public event UpdatedEventHandler Updated;

    public event StartFailedEventHandler StartFailed;

    public event StopFailedEventHandler StopFailed;

    public override void Start()
    {
        MonitorTimer.Elapsed += MonitorHelper;
    }

    public override void Stop()
    {
        if (!IsInitialized)
            return;

        MonitorTimer.Elapsed -= MonitorHelper;
        MonitorTimer.Dispose();

        base.Stop();
    }

    public bool Exists()
    {
        try
        {
            process.StartInfo.Arguments = $"interrogate {ServiceName}";
            process.Start();
            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = CommonUtils.Between(output, "FAILED ", ":");

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
            catch
            {
                status = ServiceControllerStatus.None;
                type = ServiceStartMode.Disabled;
            }

            // exit lock before calling base function ?
            Monitor.Exit(updateLock);

            // initialize only once we've pulled service status
            if (!IsInitialized)
            {
                base.Start();
            }
            else
            {
                if (prevStatus != (int)status || prevType != (int)type || nextStatus != 0)
                {
                    Updated?.Invoke(status, (int)type);
                    nextStatus = ServiceControllerStatus.None;
                    LogManager.LogInformation("Controller Service status has changed to: {0}", status.ToString());
                }

                prevStatus = (int)status;
                prevType = (int)type;
            }
        }
    }

    public bool CheckServiceInstalled(string serviceToFind)
    {
        var servicelist = ServiceController.GetServices();
        foreach (var service in servicelist)
            if (service.ServiceName == serviceToFind)
                return true;
        return false;
    }

    public void CreateService(string path)
    {
        Updated?.Invoke(ServiceControllerStatus.StartPending, -1);
        nextStatus = ServiceControllerStatus.StartPending;

        try
        {
            process.StartInfo.Arguments =
                $"create {ServiceName} binpath= \"{path}\" start= \"demand\" DisplayName= \"{DisplayName}\"";
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

    public async Task StartServiceAsync()
    {
        if (type == ServiceStartMode.Disabled)
            return;

        if (status == ServiceControllerStatus.Running)
            return;

        if (!IsInitialized)
            return;

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
    }

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
    }

    public void SetStartType(ServiceStartMode mode)
    {
        ServiceHelper.ChangeStartMode(controller, mode);
    }
}