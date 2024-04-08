using HandheldCompanion.Devices;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_LegionGoLegionSpace : IHint
    {
        private List<string> serviceNames = new()
        {
            "DAService",
        };

        private List<string> taskNames = new()
        {
            "LegionGoQuickSettings",
            "LegionSpace",
            "LSDaemon"
        };

        private List<ServiceController> serviceControllers = new();
        private Timer serviceTimer;

        public Hint_LegionGoLegionSpace() : base()
        {
            if (IDevice.GetCurrent() is not LegionGo)
                return;

            // Get all the services installed on the local computer
            ServiceController[] services = ServiceController.GetServices();
            foreach (string serviceName in serviceNames)
            {
                if (services.Any(s => serviceNames.Contains(s.ServiceName)))
                {
                    // Create a service controller object for the specified service
                    ServiceController serviceController = new ServiceController(serviceName);
                    serviceControllers.Add(serviceController);
                }
            }

            // Check if any of the services in the list exist
            if (!serviceControllers.Any())
                return;

            serviceTimer = new Timer(4000);
            serviceTimer.Elapsed += ServiceTimer_Elapsed;
            serviceTimer.Start();

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_LegionGoServices;
            this.HintDescription.Text = Properties.Resources.Hint_LegionGoServicesDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_LegionGoServicesReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_LegionGoServicesAction;
        }

        private void ServiceTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (!serviceControllers.Any())
                return;

            // Check if any of the services in the list exist and are running
            bool anyRunning = false;

            foreach (ServiceController serviceController in serviceControllers)
            {
                serviceController.Refresh();
                if (serviceController.Status == ServiceControllerStatus.Running)
                {
                    anyRunning = true;
                    break;
                }
            }

            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.Visibility = anyRunning ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        protected override void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!serviceControllers.Any())
                return;

            Task.Run(async () =>
            {
                // Disable services
                foreach (ServiceController serviceController in serviceControllers)
                {
                    // Disable service from doing anything anymore
                    ServiceUtils.ChangeStartMode(serviceController, ServiceStartMode.Disabled, out string error);

                    // Stop tasks related to service
                    foreach (string taskName in taskNames)
                    {
                        var taskProcess = Process.GetProcessesByName(taskName).FirstOrDefault();
                        if (taskProcess != null && !taskProcess.HasExited)
                        {
                            taskProcess.Kill();
                        }
                    }

                    // Stop running service
                    if (serviceController.Status == ServiceControllerStatus.Running)
                        serviceController.Stop();
                }
            });
        }

        public override void Stop()
        {
            serviceTimer?.Stop();
            base.Stop();
        }
    }
}
