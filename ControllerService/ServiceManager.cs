using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ControllerService
{
    class ServiceManager
    {
        private string name;
        private ServiceController controller;

        public ServiceManager(string name)
        {
            this.name = name;
            this.controller = new ServiceController(name);
        }

        private bool IsInstalled()
        {
            try
            {
                ServiceControllerStatus status = controller.Status;
            }
            catch
            {
                return false;
            }
            return true;
        }

        private bool IsRunning()
        {
            if (!this.IsInstalled()) return false;
            return (controller.Status == ServiceControllerStatus.Running);
        }

        protected void InstallService()
        {
            if (this.IsInstalled()) return;

            // try
        }
    }
}
