using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
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

            foreach (HIDstatus status in ((HIDstatus[])Enum.GetValues(typeof(HIDstatus))))
                cB_ServiceSwitch.Items.Add(EnumUtils.GetDescriptionFromEnumValue(status));

            PipeClient.ServerMessage += OnServerMessage;
            MainWindow.serviceManager.Updated += OnServiceUpdate;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            ControllerManager.ControllerPlugged += ControllerPlugged;
            ControllerManager.ControllerUnplugged += ControllerUnplugged;
            SystemManager.Initialized += SystemManager_Initialized;

            // device specific settings
            Type DeviceType = MainWindow.handheldDevice.GetType();
            if (DeviceType == typeof(SteamDeck))
                SteamDeckPanel.Visibility = Visibility.Visible;
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
                    case "HIDcloaked":
                        Toggle_Cloaked.IsOn = Convert.ToBoolean(value);
                        break;
                    case "HIDuncloakonclose":
                        Toggle_Uncloak.IsOn = Convert.ToBoolean(value);
                        break;
                    case "HIDstrength":
                        SliderStrength.Value = Convert.ToDouble(value);
                        break;

                    case "SteamDeckLizardMouse":
                        Toggle_SDLizardMouse.IsOn = Convert.ToBoolean(value);
                        break;
                    case "SteamDeckLizardButtons":
                        Toggle_SDLizardButtons.IsOn = Convert.ToBoolean(value);
                        break;
                }
            });
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            PipeClient.ServerMessage -= OnServerMessage;
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

            this.Dispatcher.Invoke(() =>
            {
                navLoad.Visibility = isLoading ? Visibility.Visible : Visibility.Hidden;
                ControllerGrid.IsEnabled = isConnected && !isLoading;
            });
        }

        private void ControllerUnplugged(IController Controller)
        {
            LogManager.LogDebug("Controller unplugged: {0}", Controller.ToString());

            this.Dispatcher.Invoke(() =>
            {
                // Search for an existing controller, remove it
                foreach (IController ctrl in RadioControllers.Items)
                {
                    if (ctrl.GetInstancePath() == Controller.GetInstancePath() || !ctrl.IsConnected())
                    {
                        RadioControllers.Items.Remove(ctrl);
                        break;
                    }
                }

                ControllerRefresh();
            });
        }

        private void ControllerPlugged(IController Controller)
        {
            LogManager.LogDebug("Controller plugged: {0}", Controller.ToString());

            this.Dispatcher.Invoke(() =>
            {
                // Search for an existing controller, update it
                var found = false;
                foreach (IController ctrl in RadioControllers.Items)
                {
                    found = ctrl.GetInstancePath() == Controller.GetInstancePath();
                    if (found)
                    {
                        int idx = RadioControllers.Items.IndexOf(ctrl);
                        RadioControllers.Items[idx] = Controller;
                        break;
                    }
                }

                // Add new controller to list if no existing controller was found
                if (!found)
                    RadioControllers.Items.Add(Controller);

                ControllerRefresh();
            });
        }

        private void ControllerRefresh()
        {
            this.Dispatcher.Invoke(() =>
            {
                NoDevices.Visibility = RadioControllers.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                InputDevices.Visibility = RadioControllers.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        private void SystemManager_Initialized()
        {
            // get last picked controller
            string path = SettingsManager.GetString("HIDInstancePath");

            foreach (IController ctrl in RadioControllers.Items)
            {
                if (ctrl.GetInstancePath() == path)
                {
                    RadioControllers.SelectedItem = ctrl;
                    return;
                }
            }

            RadioControllers.SelectedIndex = 0;
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
                cB_ServiceSwitch.SelectedIndex = (int)controllerStatus;

                ControllerGrid.Background = uniformToFillBrush;
            });
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_SETTINGS:
                    PipeServerSettings settings = (PipeServerSettings)message;
                    UpdateSettings(settings.settings);
                    break;
            }
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            this.Dispatcher.Invoke(() =>
            {
                foreach (KeyValuePair<string, string> pair in args)
                {
                    string name = pair.Key;
                    string property = pair.Value;

                    switch (name)
                    {
                        case "HIDmode":
                            cB_HidMode.SelectedIndex = (int)Enum.Parse(typeof(HIDmode), property);
                            break;
                        case "HIDstatus":
                            cB_ServiceSwitch.SelectedIndex = (int)Enum.Parse(typeof(HIDstatus), property);
                            break;
                    }
                }
            });

            hasSettings = true;
        }

        private void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_HidMode.SelectedIndex == -1)
                return;

            controllerMode = (HIDmode)cB_HidMode.SelectedIndex;

            // raise event
            HIDchanged?.Invoke(controllerMode);

            PipeClientSettings settings = new PipeClientSettings("HIDmode", controllerMode);
            PipeClient.SendMessage(settings);

            UpdateController();
        }

        private void cB_ServiceSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_HidMode.SelectedIndex == -1)
                return;

            controllerStatus = (HIDstatus)cB_ServiceSwitch.SelectedIndex;

            PipeClientSettings settings = new PipeClientSettings("HIDstatus", controllerStatus);
            PipeClient.SendMessage(settings);

            UpdateController();
        }

        private void Toggle_Cloaked_Toggled(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            HidHide.SetCloaking(Toggle_Cloaked.IsOn);
            SettingsManager.SetProperty("HIDcloaked", Toggle_Cloaked.IsOn);
        }

        private void Toggle_Uncloak_Toggled(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("HIDuncloakonclose", Toggle_Uncloak.IsOn);
        }

        private void SliderStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderStrength.Value;
            if (double.IsNaN(value))
                return;

            SliderStrength.Value = value;

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("HIDstrength", value);
        }

        private void RadioControllers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RadioControllers.SelectedIndex == -1)
            {
                ControllerManager.ClearTargetController();
                return;
            }

            IController Controller = (IController)RadioControllers.SelectedItem;

            string path = Controller.GetInstancePath();
            ControllerManager.SetTargetController(path);

            SettingsManager.SetProperty("HIDInstancePath", path);
        }

        private void Toggle_SDLizardButtons_Toggled(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("SteamDeckLizardButtons", Toggle_SDLizardButtons.IsOn);
        }

        private void Toggle_SDLizardMouse_Toggled(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("SteamDeckLizardMouse", Toggle_SDLizardMouse.IsOn);
        }
    }
}
