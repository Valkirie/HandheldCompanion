using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
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

            // device specific settings
            Type DeviceType = MainWindow.CurrentDevice.GetType();
            if (DeviceType == typeof(SteamDeck))
                SteamDeckPanel.Visibility = Visibility.Visible;
        }

        public ControllerPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (name)
                {
                    case "HIDcloakonconnect":
                        Toggle_Cloaked.IsOn = Convert.ToBoolean(value);
                        break;
                    case "HIDuncloakonclose":
                        Toggle_Uncloak.IsOn = Convert.ToBoolean(value);
                        break;
                    case "HIDvibrateonconnect":
                        Toggle_Vibrate.IsOn = Convert.ToBoolean(value);
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
                    case "SteamDeckMuteController":
                        Toggle_SDMuteController.IsOn = Convert.ToBoolean(value);
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

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                navLoad.Visibility = isLoading ? Visibility.Visible : Visibility.Hidden;
                ControllerGrid.IsEnabled = isConnected && !isLoading;
            });
        }

        private void ControllerUnplugged(IController Controller)
        {
            LogManager.LogDebug("Controller unplugged: {0}", Controller.ToString());

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Search for an existing controller, remove it
                foreach (Border border in InputDevices.Children)
                {
                    // pull controller from panel
                    IController ctrl = (IController)border.Tag;
                    if (ctrl is null)
                        continue;

                    if (ctrl.GetInstancePath() == Controller.GetInstancePath())
                    {
                        InputDevices.Children.Remove(border);
                        break;
                    }
                }

                ControllerRefresh();
            });
        }

        private void ControllerPlugged(IController Controller)
        {
            LogManager.LogDebug("Controller plugged: {0}", Controller.ToString());

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Add new controller to list if no existing controller was found
                FrameworkElement control = Controller.GetControl();
                InputDevices.Children.Add(control);

                Button ui_button_hook = Controller.GetButtonHook();
                ui_button_hook.Click += (sender, e) => ControllerHookClicked(Controller);

                Button ui_button_hide = Controller.GetButtonHide();
                ui_button_hide.Click += (sender, e) => ControllerHideClicked(Controller);

                ControllerRefresh();
            });
        }

        private void ControllerHookClicked(IController Controller)
        {
            string path = Controller.GetInstancePath();
            ControllerManager.SetTargetController(path);
        }

        private void ControllerHideClicked(IController Controller)
        {
            if (Controller.IsHidden())
                Controller.Unhide();
            else
                Controller.Hide();
        }

        private void ControllerRefresh()
        {
            bool hascontroller = InputDevices.Children.Count != 0;

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                InputDevices.Visibility = hascontroller ? Visibility.Visible : Visibility.Collapsed;
                NoDevices.Visibility = hascontroller ? Visibility.Collapsed : Visibility.Visible;
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

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
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
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
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

            SettingsManager.SetProperty("HIDcloakonconnect", Toggle_Cloaked.IsOn);
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

        private void Toggle_SDMuteController_Toggled(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("SteamDeckMuteController", Toggle_SDMuteController.IsOn);
        }

        private void Toggle_Vibrate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("HIDvibrateonconnect", Toggle_Vibrate.IsOn);
        }

        private void Button_Layout_Click(object sender, RoutedEventArgs e)
        {
            // update layout page with current layout
            MainWindow.layoutPage.UpdateLayout(LayoutManager.LayoutTemplates["Desktop"]);
            MainWindow.NavView_Navigate(MainWindow.layoutPage);
        }
    }
}
