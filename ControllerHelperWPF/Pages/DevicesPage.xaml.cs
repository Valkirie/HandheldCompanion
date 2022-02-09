using ControllerCommon;
using Microsoft.Extensions.Logging;
using ModernWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for Devices.xaml
    /// </summary>
    public partial class DevicesPage : Page
    {
        private MainWindow mainWindow;
        private readonly ILogger microsoftLogger;

        // pipe vars
        PipeClient pipeClient;
        bool pipeConnected;

        ServiceManager serviceManager;

        // controllers vars
        private XInputDevice mainController;
        private HIDmode controllerMode = HIDmode.None;
        private HIDstatus controllerStatus = HIDstatus.Disconnected;

        public DevicesPage()
        {
            InitializeComponent();

            foreach (HIDmode mode in ((HIDmode[])Enum.GetValues(typeof(HIDmode))).Where(a => a != HIDmode.None))
                cB_HidMode.Items.Add(Utils.GetDescriptionFromEnumValue(mode));
        }

        public DevicesPage(MainWindow mainWindow, ILogger microsoftLogger) : this()
        {
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = mainWindow.pipeClient;
            this.serviceManager = mainWindow.serviceManager;

            this.pipeClient.ServerMessage += OnServerMessage;
            this.pipeClient.Connected += OnClientConnected;
            this.pipeClient.Disconnected += OnClientDisconnected;

            this.serviceManager.Updated += OnServiceUpdate;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // implement me
        }

        private void UpdateController()
        {
            if (controllerMode == HIDmode.None)
                return;

            // update UI icon to match HIDmode
            ImageBrush uniformToFillBrush = new ImageBrush();
            uniformToFillBrush.Stretch = Stretch.Uniform;
            uniformToFillBrush.ImageSource = new BitmapImage(new Uri($"pack://application:,,,/Resources/controller_{Convert.ToInt32(controllerMode)}_{Convert.ToInt32(controllerStatus)}.png"));

            // Freeze the brush (make it unmodifiable) for performance benefits.
            uniformToFillBrush.Freeze();

            // threaded call to update UI
            this.Dispatcher.Invoke(() =>
            {
                cB_HidMode.SelectedIndex = (int)controllerMode;
                ControllerGrid.Background = uniformToFillBrush;

                B_ServiceSwitch.Content = controllerStatus == HIDstatus.Connected ? "Disconnect" : "Connect";
            });
        }

        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_CONTROLLER:
                    PipeServerController controller = (PipeServerController)message;
                    mainController = new XInputDevice(controller.ProductName, controller.InstanceGuid, controller.ProductGuid, controller.ProductIndex);

                    // threaded call to update UI
                    this.Dispatcher.Invoke(() =>
                    {
                        DeviceName.Text = mainController.ProductName;
                        DeviceInstanceID.Text = mainController.InstanceGuid.ToString();
                        DeviceProductID.Text = mainController.ProductGuid.ToString();
                    });

                    microsoftLogger.LogInformation("{0} connected on port {1}", controller.ProductName, controller.ProductIndex);
                    break;

                case PipeCode.SERVER_SETTINGS:
                    PipeServerSettings settings = (PipeServerSettings)message;
                    UpdateSettings(settings.settings);
                    break;
            }
        }

        private void OnClientDisconnected(object sender)
        {
            controllerStatus = HIDstatus.Disconnected;
            pipeConnected = false;

            UpdateMainGrid();
        }

        private void OnClientConnected(object sender)
        {
            pipeConnected = true;

            UpdateMainGrid();
        }

        private void UpdateMainGrid()
        {
            // threaded call to update UI
            this.Dispatcher.Invoke(() =>
            {
                MainGrid.IsEnabled = pipeConnected;
                UpdateController();
            });
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            foreach (KeyValuePair<string, string> pair in args)
            {
                string name = pair.Key;
                string property = pair.Value;

                switch (name)
                {
                    case "HIDmode":
                        controllerMode = (HIDmode)Enum.Parse(typeof(HIDmode), property);
                        UpdateController();
                        break;
                    case "HIDstatus":
                        controllerStatus = (HIDstatus)Enum.Parse(typeof(HIDstatus), property);
                        UpdateController();
                        break;
                    case "HIDcloaked":
                        break;
                    case "HIDuncloakonclose":
                        break;
                    case "HIDstrength":
                        break;
                }
            }
        }

        private void ToggleTheme(object sender, RoutedEventArgs e)
        {
            if (ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark)
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            }
            else
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }
        }

        private void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            controllerMode = (HIDmode)cB_HidMode.SelectedIndex;

            PipeClientSettings settings = new PipeClientSettings("HIDmode", controllerMode);
            mainWindow.pipeClient.SendMessage(settings);

            UpdateController();
        }

        private void OnServiceUpdate(ServiceControllerStatus status, ServiceStartMode mode)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                        break;
                    case ServiceControllerStatus.Running:
                        break;
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.StopPending:
                        break;
                    default:
                        break;
                }
            });
        }

        private void B_ServiceSwitch_Click(object sender, RoutedEventArgs e)
        {
            controllerStatus = controllerStatus == HIDstatus.Connected ? HIDstatus.Disconnected : HIDstatus.Connected;

            PipeClientSettings settings = new PipeClientSettings("HIDstatus", controllerStatus);
            mainWindow.pipeClient.SendMessage(settings);

            UpdateController();
        }
    }
}
