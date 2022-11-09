using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
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
using ServiceControllerStatus = ControllerCommon.Managers.ServiceControllerStatus;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for Devices.xaml
    /// </summary>
    public partial class ControllerPage : Page
    {
        // pipe vars
        bool isConnected;
        bool isLoading;
        bool hasSettings;

        // controllers vars
        private HIDmode controllerMode = HIDmode.NoController;
        private HIDstatus controllerStatus = HIDstatus.Disconnected;

        public event HIDchangedEventHandler HIDchanged;
        public delegate void HIDchangedEventHandler(HIDmode HID);

        public ControllerPage()
        {
            InitializeComponent();

            // initialize components
            foreach (HIDmode mode in ((HIDmode[])Enum.GetValues(typeof(HIDmode))).Where(a => a != HIDmode.NoController))
                cB_HidMode.Items.Add(EnumUtils.GetDescriptionFromEnumValue(mode));

            MainWindow.pipeClient.ServerMessage += OnServerMessage;
            MainWindow.serviceManager.Updated += OnServiceUpdate;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            ControllerManager.ControllerPlugged += ControllerPlugged;
            ControllerManager.ControllerUnplugged += ControllerUnplugged;
        }

        public ControllerPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (name)
                {
                    case "HIDmode":
                        cB_HidMode.SelectedIndex = Convert.ToInt32(value);
                        cB_HidMode_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
                        break;
                }
            });
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            MainWindow.pipeClient.ServerMessage -= OnServerMessage;
            MainWindow.serviceManager.Updated -= OnServiceUpdate;
        }

        private async void OnServiceUpdate(ServiceControllerStatus status, int mode)
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

        private void ControllerUnplugged(IController Controller)
        {
            this.Dispatcher.Invoke(() =>
            {
                foreach (IController ctrl in RadioControllers.Items)
                {
                    if (ctrl.GetInstancePath() == Controller.GetInstancePath())
                    {
                        RadioControllers.Items.Remove(ctrl);
                        break;
                    }
                }
            });
        }

        private void ControllerPlugged(IController Controller)
        {
            this.Dispatcher.Invoke(() =>
            {
                foreach (IController ctrl in RadioControllers.Items)
                {
                    if (ctrl.GetInstancePath() == Controller.GetInstancePath())
                    {
                        int idx = RadioControllers.Items.IndexOf(ctrl);
                        RadioControllers.Items[idx] = Controller;

                        return;
                    }
                }

                RadioControllers.Items.Add(Controller);

                InputDevices.Visibility = Visibility.Visible;
            });
        }

        private void UpdateController()
        {
            if (controllerMode == HIDmode.NoController)
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

                B_ServiceSwitch.Content = controllerStatus == HIDstatus.Connected ? Properties.Resources.ControllerPage_Disconnect : Properties.Resources.ControllerPage_Connect;
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

                ControllerGrid.IsEnabled = isConnected && !isLoading;
                DeviceCloakingStackPanel.IsEnabled = isConnected && !isLoading;

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
                            else if (RadioControllers.Items.Count >= 1)
                                RadioControllers.SelectedIndex = 0;
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
            if (cB_HidMode.SelectedIndex == -1)
                return;

            controllerMode = (HIDmode)cB_HidMode.SelectedIndex;

            // raise event
            HIDchanged?.Invoke(controllerMode);

            PipeClientSettings settings = new PipeClientSettings("HIDmode", controllerMode);
            MainWindow.pipeClient?.SendMessage(settings);

            UpdateController();

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("HIDmode", cB_HidMode.SelectedIndex);
        }

        private void B_ServiceSwitch_Click(object sender, RoutedEventArgs e)
        {
            controllerStatus = controllerStatus == HIDstatus.Connected ? HIDstatus.Disconnected : HIDstatus.Connected;

            PipeClientSettings settings = new PipeClientSettings("HIDstatus", controllerStatus);
            MainWindow.pipeClient?.SendMessage(settings);

            UpdateController();
        }

        private void Toggle_Cloaked_Toggled(object sender, RoutedEventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDcloaked", Toggle_Cloaked.IsOn);
            MainWindow.pipeClient?.SendMessage(settings);
        }

        private void Toggle_Uncloak_Toggled(object sender, RoutedEventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDuncloakonclose", Toggle_Uncloak.IsOn);
            MainWindow.pipeClient?.SendMessage(settings);
        }

        private void SliderStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDstrength", SliderStrength.Value);
            MainWindow.pipeClient?.SendMessage(settings);
        }

        private void RadioControllers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RadioControllers.SelectedIndex == -1)
                return;

            IController Controller = (IController)RadioControllers.SelectedItem;
            ControllerManager.SetTargetController(Controller.GetContainerInstancePath());
        }
    }
}
