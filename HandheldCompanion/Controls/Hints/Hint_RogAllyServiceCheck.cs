using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_RogAllyServiceCheck : IHint
    {
        private const string serviceName = "ARMOURY CRATE SE Service";
        private Timer serviceTimer;
        private ServiceController serviceController;

        public Hint_RogAllyServiceCheck() : base()
        {
            if (MainWindow.CurrentDevice is not ROGAlly)
                return;

            // Get all the services installed on the local computer
            ServiceController[] services = ServiceController.GetServices();
            bool serviceExists = services.Any(s => s.ServiceName == serviceName);

            if (!serviceExists)
                return;

            // Create a service controller object for the specified service
            serviceController = new ServiceController(serviceName);
            serviceTimer = new Timer(4000);
            serviceTimer.Elapsed += ServiceTimer_Elapsed;
            serviceTimer.Start();

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_SteamNeptuneDesktopWarning;
            this.HintDescription.Text = Properties.Resources.Hint_SteamNeptuneDesktopDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_SteamNeptuneDesktopAction;

            this.HintActionButton.Content = "Disable service";
        }

        private void ServiceTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (serviceController is null)
                return;

            serviceController.Refresh();

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                this.Visibility = serviceController.Status == ServiceControllerStatus.Running ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        protected override void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (serviceController is null)
                return;

            Task.Run(async () =>
            {
                serviceController.Stop();
                serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
                ServiceUtils.ChangeStartMode(serviceController, ServiceStartMode.Disabled, out _);
            });
        }
    }
}
