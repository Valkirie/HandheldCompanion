using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Utils;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            ControllerManager.Initialized += ControllerManager_Initialized;

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
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
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
                    case "DesktopLayoutEnabled":
                        Toggle_DesktopLayout.IsOn = Convert.ToBoolean(value);
                        break;
                    case "SteamDeckMuteController":
                        Toggle_SDMuteController.IsOn = Convert.ToBoolean(value);
                        ControllerRefresh();
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

        private void OnServiceUpdate(ServiceControllerStatus status, int mode)
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
                    {
                        Task.Factory.StartNew(() =>
                        {
                            while (!hasSettings)
                                Thread.Sleep(250);
                        });

                        isLoading = false;
                        isConnected = true;

                        ControllerRefresh();
                    }
                    break;
                default:
                    isLoading = false;
                    break;
            }

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                navLoad.Visibility = isLoading ? Visibility.Visible : Visibility.Hidden;
                ControllerGrid.IsEnabled = isConnected && !isLoading;
            });
        }

        private void ControllerUnplugged(IController Controller)
        {
            LogManager.LogDebug("Controller unplugged: {0}", Controller.ToString());

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
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

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
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

        private void ControllerManager_Initialized()
        {
            ControllerRefresh();
        }

        private void ControllerHookClicked(IController Controller)
        {
            string path = Controller.GetInstancePath();
            ControllerManager.SetTargetController(path);

            ControllerRefresh();
        }

        private void ControllerHideClicked(IController Controller)
        {
            if (Controller.IsHidden())
                Controller.Unhide();
            else
                Controller.Hide();

            ControllerRefresh();
        }

        private void ControllerRefresh()
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                bool hasPhysiscal = ControllerManager.HasPhysicalController();
                bool hasVirtual = ControllerManager.HasVirtualController();
                bool hasTarget = ControllerManager.GetTargetController() != null;

                // check: do we have any plugged physical controller
                InputDevices.Visibility = hasPhysiscal ? Visibility.Visible : Visibility.Collapsed;
                WarningNoPhysical.Visibility = !hasPhysiscal ? Visibility.Visible : Visibility.Collapsed;

                IController target = ControllerManager.GetTargetController();
                bool isPlugged = hasTarget && target.IsPlugged();
                bool isHidden = hasTarget && target.IsHidden();
                bool isNeptune = hasTarget && target.GetType().Equals(typeof(NeptuneController));
                bool isMuted = SettingsManager.GetBoolean("SteamDeckMuteController");

                // hint: Has physical controller, but is not connected
                HintsNoPhysicalConnected.Visibility = hasPhysiscal && !isPlugged ? Visibility.Visible : Visibility.Collapsed;

                // hint: Has physical controller (not Neptune) hidden, but no virtual controller
                bool hiddenbutnovirtual = isHidden && !hasVirtual;
                HintsNoVirtual.Visibility = hiddenbutnovirtual ? Visibility.Visible : Visibility.Collapsed;

                // hint: Has physical controller (Neptune) hidden, but virtual controller is muted
                bool neptunehidden = isHidden && isNeptune && isMuted;
                HintsNeptuneHidden.Visibility = neptunehidden ? Visibility.Visible : Visibility.Collapsed;

                // hint: Has physical controller not hidden, and virtual controller
                bool notmuted = !isHidden && !isNeptune && hasVirtual;
                HintsNotMuted.Visibility = notmuted ? Visibility.Visible : Visibility.Collapsed;
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

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
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

            SettingsManager.SetProperty("HIDmode", controllerMode, false, true);
        }

        private void cB_ServiceSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cB_HidMode.SelectedIndex == -1)
                return;

            controllerStatus = (HIDstatus)cB_ServiceSwitch.SelectedIndex;

            PipeClientSettings settings = new PipeClientSettings("HIDstatus", controllerStatus);
            PipeClient.SendMessage(settings);

            UpdateController();

            SettingsManager.SetProperty("HIDstatus", controllerStatus, false, true);
        }

        private void Toggle_Cloaked_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("HIDcloakonconnect", Toggle_Cloaked.IsOn);
        }

        private void Toggle_Uncloak_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("HIDuncloakonclose", Toggle_Uncloak.IsOn);
        }

        private void SliderStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = SliderStrength.Value;
            if (double.IsNaN(value))
                return;

            SliderStrength.Value = value;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("HIDstrength", value);
        }

        private void Toggle_SDMuteController_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("SteamDeckMuteController", Toggle_SDMuteController.IsOn);
        }

        private void Toggle_Vibrate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("HIDvibrateonconnect", Toggle_Vibrate.IsOn);
        }

        private void Button_Layout_Click(object sender, RoutedEventArgs e)
        {
            // prepare layout editor, desktopLayout gets saved automatically
            LayoutTemplate desktopTemplate = new(LayoutManager.GetDesktop())
            {
                Name = LayoutTemplate.DesktopLayout.Name,
                Description = LayoutTemplate.DesktopLayout.Description,
                Author = Environment.UserName,
                Executable = string.Empty,
                Product = string.Empty,  // UI might've set something here, nullify
            };
            MainWindow.layoutPage.UpdateLayout(desktopTemplate);
            MainWindow.NavView_Navigate(MainWindow.layoutPage);
        }

        private void Toggle_DesktopLayout_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            // temporary settings
            SettingsManager.SetProperty("DesktopLayoutEnabled", Toggle_DesktopLayout.IsOn, false, true);
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }
    }
}
