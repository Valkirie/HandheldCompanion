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
            this.pipeClient.ServerMessage += OnServerMessage;
            this.pipeClient.Connected += OnClientConnected;
            this.pipeClient.Disconnected += OnClientDisconnected;
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
            ImageBrush uniformToFillBrush = new ImageBrush()
            {
                Stretch = Stretch.Uniform,
                ImageSource = new BitmapImage(new Uri($"pack://application:,,,/Resources/controller_{Convert.ToInt32(controllerMode)}_{Convert.ToInt32(controllerStatus)}.png"))
            };

            // Freeze the brush (make it unmodifiable) for performance benefits.
            uniformToFillBrush.Freeze();

            // threaded call to update UI
            this.Dispatcher.Invoke(() =>
            {
                cB_HidMode.SelectedIndex = (int)controllerMode;
                ControllerGrid.Background = uniformToFillBrush;

                // todo: localization
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
                        this.Dispatcher.Invoke(() =>
                        {
                            Toggle_Cloaked.IsOn = bool.Parse(property);
                        });
                        break;
                    case "HIDuncloakonclose":
                        this.Dispatcher.Invoke(() =>
                        {
                            Toggle_Uncloak.IsOn = bool.Parse(property);
                        });
                        break;
                    case "HIDstrength":
                        this.Dispatcher.Invoke(() =>
                        {
                            SliderStrength.Value = float.Parse(property);
                        });
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

        private void B_ServiceSwitch_Click(object sender, RoutedEventArgs e)
        {
            controllerStatus = controllerStatus == HIDstatus.Connected ? HIDstatus.Disconnected : HIDstatus.Connected;

            PipeClientSettings settings = new PipeClientSettings("HIDstatus", controllerStatus);
            mainWindow.pipeClient.SendMessage(settings);

            UpdateController();
        }

        private void Toggle_Cloaked_Toggled(object sender, RoutedEventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDcloaked", Toggle_Cloaked.IsOn);
            pipeClient.SendMessage(settings);
        }

        private void Toggle_Uncloak_Toggled(object sender, RoutedEventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDuncloakonclose", Toggle_Uncloak.IsOn);
            pipeClient.SendMessage(settings);
        }
    }
}
