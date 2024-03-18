using HandheldCompanion.Devices;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_RogAllyServiceCheck : IHint
    {
        private List<string> serviceNames = new()
        {
            "ArmouryCrateSEService",
            "AsusAppService",
            "ArmouryCrateControlInterface",
        };

        private List<ServiceController> serviceControllers = new();
        private Timer serviceTimer;

        public Hint_RogAllyServiceCheck() : base()
        {
            if (IDevice.GetCurrent() is not ROGAlly)
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

            this.HintTitle.Text = Properties.Resources.Hint_RogAllyServiceCheck;
            this.HintDescription.Text = Properties.Resources.Hint_RogAllyServiceCheckDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_RogAllyServiceCheckReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_RogAllyServiceCheckAction;
        }

        private void ServiceTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if(!serviceControllers.Any())
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
            Application.Current.Dispatcher.BeginInvoke(() =>
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
                foreach (ServiceController serviceController in serviceControllers)
                {
                    if (serviceController.Status == ServiceControllerStatus.Running)
                        serviceController.Stop();
                    serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
                    ServiceUtils.ChangeStartMode(serviceController, ServiceStartMode.Disabled, out _);
                }
            });
        }

        public override void Stop()
        {
            serviceTimer.Stop();
            base.Stop();
        }
    }
}
