using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
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
        private ServiceControllerStatus prevStatus = ServiceControllerStatus.Stopped;
        private ServiceControllerStatus nextStatus;

        private Process process;

        private Timer MonitorTimer;
        private object updateLock = new();

        private readonly ControllerHelper helper;

        public ServiceManager(string name, ControllerHelper helper, string display, string description)
        {
            this.helper = helper;

            this.name = name;
            this.display = display;
            this.description = description;

            this.controller = new ServiceController(name);

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
                }
                catch(Exception)
                {
                    status = 0;
                }

                if(nextStatus != 0)
                    controller.WaitForStatus(nextStatus, TimeSpan.FromSeconds(5));

                if (prevStatus != status)
                    helper.UpdateService(status);

                prevStatus = status;
            }
        }

        public void CreateService(string path)
        {
            process.StartInfo.Arguments = $"create {name} binpath= \"{path}\" start= \"auto\" DisplayName= \"{display}\"";
            process.Start();
            process.WaitForExit();

            process.StartInfo.Arguments = $"description {name} \"{description}\"";
            process.Start();
            process.WaitForExit();

            nextStatus = ServiceControllerStatus.Stopped;
        }

        public void DeleteService()
        {
            process.StartInfo.Arguments = $"delete {name}";
            process.Start();
            process.WaitForExit();

            nextStatus = 0;
        }

        public void StartService()
        {
            process.StartInfo.Arguments = $"start {name}";
            process.Start();
            process.WaitForExit();

            nextStatus = ServiceControllerStatus.Running;
        }

        public void StopService()
        {
            process.StartInfo.Arguments = $"stop {name}";
            process.Start();
            process.WaitForExit();

            nextStatus = ServiceControllerStatus.Stopped;
        }
    }
}
