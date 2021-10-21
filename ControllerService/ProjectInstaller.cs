using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace ControllerService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        protected override void OnAfterInstall(IDictionary savedState)
        {
            base.OnAfterInstall(savedState);

            // The following code ticks the "Allow Windows service to interact with desktop"
            ConnectionOptions coOptions = new ConnectionOptions();
            coOptions.Impersonation = ImpersonationLevel.Impersonate;
            ManagementScope mgmtScope = new ManagementScope(@"root\CIMV2", coOptions);
            mgmtScope.Connect();
            ManagementObject wmiService;
            wmiService = new ManagementObject("Win32_Service.Name='" + serviceInstaller1.ServiceName + "'");
            ManagementBaseObject InParam = wmiService.GetMethodParameters("Change");
            InParam["DesktopInteract"] = true;
            ManagementBaseObject OutParam = wmiService.InvokeMethod("Change", InParam, null);

            //The following code starts the services after it is installed.
            using (System.ServiceProcess.ServiceController serviceController = new System.ServiceProcess.ServiceController(serviceInstaller1.ServiceName))
                serviceController.Start();
        }
    }
}
