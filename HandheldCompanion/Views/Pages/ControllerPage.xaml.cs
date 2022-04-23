using ControllerCommon;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using ModernWpf.Controls;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.ServiceControllerStatus;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for Devices.xaml
    /// </summary>
    public partial class ControllerPage : Page
    {
        private MainWindow mainWindow;
        private HidHide Hidder;

        private readonly ILogger microsoftLogger;
        private ServiceManager serviceManager;

        // pipe vars
        PipeClient pipeClient;
        bool isConnected;
        bool isLoading;
        bool hasSettings;

        // controllers vars
        private HIDmode controllerMode = HIDmode.None;
        private HIDstatus controllerStatus = HIDstatus.Disconnected;

        private ControllerManager controllerManager;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(HIDmode controllerMode);

        public ControllerPage()
        {
            InitializeComponent();

            foreach (HIDmode mode in ((HIDmode[])Enum.GetValues(typeof(HIDmode))).Where(a => a != HIDmode.None))
                cB_HidMode.Items.Add(EnumUtils.GetDescriptionFromEnumValue(mode));

            // initialize controller manager
            controllerManager = new ControllerManager(microsoftLogger);
            controllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;
            controllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;
            controllerManager.Start();
        }

        public ControllerPage(string Tag, MainWindow mainWindow, ILogger microsoftLogger) : this()
        {
            this.Tag = Tag;

            this.mainWindow = mainWindow;
            this.Hidder = mainWindow.Hidder;

            this.microsoftLogger = microsoftLogger;

            this.pipeClient = mainWindow.pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;

            this.serviceManager = mainWindow.serviceManager;
            this.serviceManager.Updated += ServiceManager_Updated;
        }

        private async void ServiceManager_Updated(ServiceControllerStatus status, int mode)
        {
            switch (status)
            {
                case ServiceControllerStatus.ContinuePending:
                case ServiceControllerStatus.PausePending:
                case ServiceControllerStatus.StartPending:
                case ServiceControllerStatus.StopPending:
                    isLoading = true;
                    break;
                case ServiceControllerStatus.Paused:
                    isLoading = false;
                    break;
                case ServiceControllerStatus.Stopped:
                    controllerStatus = HIDstatus.Disconnected;
                    isLoading = false;
                    isConnected = false;
                    break;
                case ServiceControllerStatus.Running:

                    while (!hasSettings)
                        await Task.Delay(500);

                    isLoading = false;
                    isConnected = true;
                    break;
                default:
                    isLoading = false;
                    break;
            }
            UpdateMainGrid();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void ControllerManager_ControllerUnplugged(UserIndex idx, ControllerEx controller)
        {
            // implement me
            this.Dispatcher.Invoke(() =>
            {
                ControllerEx removeme = null;
                foreach (ControllerEx ctrl in RadioControllers.Items)
                {
                    if (ctrl.Controller.UserIndex == idx)
                        removeme = ctrl;
                }

                if(removeme != null)
                    RadioControllers.Items.Remove(removeme);

                if (RadioControllers.Items.Count == 0)
                    InputDevices.Visibility = Visibility.Collapsed;
            });
        }

        private void ControllerManager_ControllerPlugged(UserIndex idx, ControllerEx controller)
        {
            // implement me
            this.Dispatcher.Invoke(() =>
            {
                RadioControllers.Items.Add(controller);
                InputDevices.Visibility = Visibility.Visible;
            });
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
                case PipeCode.SERVER_SETTINGS:
                    PipeServerSettings settings = (PipeServerSettings)message;
                    UpdateSettings(settings.settings);
                    break;
            }
        }

        private void UpdateMainGrid()
        {
            // threaded call to update UI
            this.Dispatcher.Invoke(() =>
            {
                navLoad.Visibility = isLoading ? Visibility.Visible : Visibility.Hidden;
                MainGrid.IsEnabled = isConnected && !isLoading;
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
                    case "HIDidx":
                        this.Dispatcher.Invoke(() =>
                        {
                            int index = int.Parse(property);
                            if (RadioControllers.Items.Count > index)
                                RadioControllers.SelectedIndex = index;
                        });
                        break;
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
                            SliderStrength.Value = double.Parse(property, CultureInfo.InvariantCulture);
                        });
                        break;
                }
            }

            hasSettings = true;

            UpdateMainGrid();
        }

        private void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            controllerMode = (HIDmode)cB_HidMode.SelectedIndex;

            PipeClientSettings settings = new PipeClientSettings("HIDmode", controllerMode);
            mainWindow.pipeClient.SendMessage(settings);

            Updated?.Invoke(controllerMode);

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
            pipeClient?.SendMessage(settings);
        }

        private void Toggle_Uncloak_Toggled(object sender, RoutedEventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDuncloakonclose", Toggle_Uncloak.IsOn);
            pipeClient?.SendMessage(settings);
        }

        private void SliderStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDstrength", SliderStrength.Value);
            pipeClient?.SendMessage(settings);
        }

        private void Scrolllock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = false;
        }

        private void RadioControllers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RadioControllers.SelectedIndex == -1)
                return;

            ControllerEx controllerEx = (ControllerEx)RadioControllers.SelectedItem;

            if (controllerEx == null)
                return;

            if (!controllerEx.IsConnected())
                return;

            PipeControllerIndex settings = new PipeControllerIndex((int)controllerEx.UserIndex, controllerEx.deviceInstancePath, controllerEx.baseContainerDeviceInstancePath);
            pipeClient?.SendMessage(settings);

            // vibrate controller
            controllerEx.Identify();
        }
    }
}
