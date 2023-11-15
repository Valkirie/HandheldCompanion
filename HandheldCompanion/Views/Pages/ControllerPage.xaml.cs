using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Targets;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using Microsoft.Win32.SafeHandles;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Devices.xaml
/// </summary>
public partial class ControllerPage : Page
{
    public delegate void HIDchangedEventHandler(HIDmode HID);
    public event HIDchangedEventHandler HIDchanged;

    // controllers vars
    private HIDmode controllerMode = HIDmode.NoController;
    private HIDstatus controllerStatus = HIDstatus.Disconnected;

    public ControllerPage()
    {
        InitializeComponent();

        // initialize components
        foreach (var mode in ((HIDmode[])Enum.GetValues(typeof(HIDmode))).Where(a => a != HIDmode.NoController))
            cB_HidMode.Items.Add(EnumUtils.GetDescriptionFromEnumValue(mode));

        foreach (var status in (HIDstatus[])Enum.GetValues(typeof(HIDstatus)))
            cB_ServiceSwitch.Items.Add(EnumUtils.GetDescriptionFromEnumValue(status));

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        ControllerManager.ControllerPlugged += ControllerPlugged;
        ControllerManager.ControllerUnplugged += ControllerUnplugged;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
        ControllerManager.Working += ControllerManager_Working;

        PlatformManager.Initialized += PlatformManager_Initialized;
        PlatformManager.Steam.Updated += Steam_Updated;

        VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;
    }

    public ControllerPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void PlatformManager_Initialized()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            HintsSteamXboxDrivers.Visibility = PlatformManager.Steam.HasXboxDriversInstalled() ? Visibility.Visible : Visibility.Collapsed;
            Steam_Updated(PlatformManager.Steam.IsRunning ? PlatformStatus.Started : PlatformStatus.Stopped);
        });
    }

    private void Steam_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Stopping:
                case PlatformStatus.Stopped:
                    HintsSteamNeptuneDeskop.Visibility = Visibility.Collapsed;
                    break;
                case PlatformStatus.Started:
                    HintsSteamNeptuneDeskop.Visibility = PlatformManager.Steam.HasDesktopProfileApplied() ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        });
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
                case "VibrationStrength":
                    SliderStrength.Value = Convert.ToDouble(value);
                    break;
                case "DesktopLayoutEnabled":
                    Toggle_DesktopLayout.IsOn = Convert.ToBoolean(value);
                    break;
                case "SteamControllerMute":
                    Toggle_SCMuteController.IsOn = Convert.ToBoolean(value);
                    ControllerRefresh();
                    break;
                case "HIDmode":
                    cB_HidMode.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "HIDstatus":
                    cB_ServiceSwitch.SelectedIndex = Convert.ToInt32(value);
                    UpdateControllerImage();
                    break;
            }
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }

    private void ControllerUnplugged(IController Controller, bool IsPowerCycling)
    {
        LogManager.LogDebug("Controller unplugged: {0}", Controller.ToString());

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // Search for an existing controller, remove it
            foreach (IController ctrl in InputDevices.Children)
            {
                if (ctrl.GetContainerInstancePath() == Controller.GetContainerInstancePath())
                {
                    if (!IsPowerCycling)
                    {
                        InputDevices.Children.Remove(ctrl);
                        ControllerRefresh();
                        break;
                    }
                }
            }
        });
    }

    private void ControllerPlugged(IController Controller, bool IsPowerCycling)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // Search for an existing controller, remove it
            foreach (IController ctrl in InputDevices.Children)
            {
                if (ctrl.GetContainerInstancePath() == Controller.GetContainerInstancePath())
                {
                    InputDevices.Children.Remove(ctrl);
                    break;
                }
            }

            // Add new controller to list if no existing controller was found
            InputDevices.Children.Add(Controller);

            // todo: move me
            var ui_button_hook = Controller.GetButtonHook();
            ui_button_hook.Click += (sender, e) => ControllerHookClicked(Controller);

            var ui_button_hide = Controller.GetButtonHide();
            ui_button_hide.Click += (sender, e) => ControllerHideClicked(Controller);

            ControllerRefresh();
        });
    }

    private void ControllerManager_ControllerSelected(IController Controller)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SteamControllerPanel.Visibility = Controller is SteamController ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void ControllerManager_Working(bool busy)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ControllerLoading.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            ControllerGrid.IsEnabled = !busy;
        });
    }

    private void VirtualManager_ControllerSelected(HIDmode mode)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            cB_HidMode.SelectedIndex = (int)mode;
        });
    }

    private void ControllerHookClicked(IController Controller)
    {
        // todo: move me
        var path = Controller.GetContainerInstancePath();
        ControllerManager.SetTargetController(path, false);

        ControllerRefresh();
    }

    private void ControllerHideClicked(IController Controller)
    {
        // todo: move me
        if (Controller.IsHidden())
            Controller.Unhide();
        else
            Controller.Hide();

        if (!ControllerManager.PowerCyclers.ContainsKey(Controller.GetContainerInstancePath()))
            ControllerRefresh();
    }

    private void ControllerRefresh()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var hasPhysical = ControllerManager.HasPhysicalController();
            var hasVirtual = ControllerManager.HasVirtualController();
            var hasTarget = ControllerManager.GetTargetController() != null;

            // check: do we have any plugged physical controller
            InputDevices.Visibility = hasPhysical ? Visibility.Visible : Visibility.Collapsed;
            WarningNoPhysical.Visibility = !hasPhysical ? Visibility.Visible : Visibility.Collapsed;

            var target = ControllerManager.GetTargetController();
            var isPlugged = hasTarget && target.IsPlugged;
            var isHidden = hasTarget && target.IsHidden();
            var isSteam = hasTarget && (target is NeptuneController || target is GordonController);
            var isMuted = SettingsManager.GetBoolean("SteamControllerMute");

            // hint: Has physical controller, but is not connected
            HintsNoPhysicalConnected.Visibility =
                hasPhysical && !isPlugged ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller (not Neptune) hidden, but no virtual controller
            var hiddenbutnovirtual = isHidden && !hasVirtual;
            HintsNoVirtual.Visibility = hiddenbutnovirtual ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller (Neptune) hidden, but virtual controller is muted
            var neptunehidden = isHidden && isSteam && isMuted;
            HintsNeptuneHidden.Visibility = neptunehidden ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller not hidden, and virtual controller
            var notmuted = !isHidden && hasVirtual && !isMuted;
            HintsNotMuted.Visibility = notmuted ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void UpdateControllerImage()
    {
        BitmapImage controllerImage;
        if (controllerMode == HIDmode.NoController || controllerStatus == HIDstatus.Disconnected)
            controllerImage = new BitmapImage(new Uri($"pack://application:,,,/Resources/controller_2_0.png"));
        else
            controllerImage = new BitmapImage(new Uri($"pack://application:,,,/Resources/controller_{Convert.ToInt32(controllerMode)}_{Convert.ToInt32(controllerStatus)}.png"));

        // update UI icon to match HIDmode
        ImageBrush uniformToFillBrush = new ImageBrush()
        {
            Stretch = Stretch.Uniform,
            ImageSource = controllerImage,
        };
        uniformToFillBrush.Freeze();

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ControllerGrid.Background = uniformToFillBrush;
        });
    }

    private async void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_HidMode.SelectedIndex == -1)
            return;

        controllerMode = (HIDmode)cB_HidMode.SelectedIndex;
        UpdateControllerImage();

        // raise event
        HIDchanged?.Invoke(controllerMode);

        SettingsManager.SetProperty("HIDmode", cB_HidMode.SelectedIndex);

    }

    private void cB_ServiceSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_HidMode.SelectedIndex == -1)
            return;

        controllerStatus = (HIDstatus)cB_ServiceSwitch.SelectedIndex;
        UpdateControllerImage();

        SettingsManager.SetProperty("HIDstatus", cB_ServiceSwitch.SelectedIndex);
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
        var value = SliderStrength.Value;
        if (double.IsNaN(value))
            return;

        SliderStrength.Value = value;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("VibrationStrength", value);
    }

    private void Toggle_SCMuteController_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("SteamControllerMute", Toggle_SCMuteController.IsOn);
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
            Product = string.Empty // UI might've set something here, nullify
        };
        MainWindow.layoutPage.UpdateLayoutTemplate(desktopTemplate);
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

    private void HintsSteamNeptuneDeskopButton_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            PlatformManager.Steam.StopProcess();

            while (PlatformManager.Steam.IsRunning)
                await Task.Delay(1000);

            PlatformManager.Steam.StartProcess();
        });
    }
}