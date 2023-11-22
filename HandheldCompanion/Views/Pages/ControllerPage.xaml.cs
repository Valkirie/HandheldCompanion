using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
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
                case "ControllerManagement":
                    Toggle_ControllerManagement.IsOn = Convert.ToBoolean(value);
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
                case "LegionControllerPassthrough":
                    Toggle_TouchpadPassthrough.IsOn = Convert.ToBoolean(value);
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
                if (ctrl.GetContainerInstancePath() == Controller.GetContainerInstancePath())
                    return;

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
        ControllerRefresh();
    }

    private void ControllerManager_Working(int status)
    {
        // status: 0:wip, 1:sucess, 2:failed

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch(status)
            {
                case 0:
                    ControllerLoading.Visibility = Visibility.Visible;
                    InputDevices.IsEnabled = false;
                    ControllerGrid.IsEnabled = false;
                    ControllerLoadingText.Text = GetRandomPhrase();
                    break;
                case 1:
                case 2:
                    ControllerLoading.Visibility = Visibility.Collapsed;
                    InputDevices.IsEnabled = true;
                    ControllerGrid.IsEnabled = true;
                    break;
            }

            if (status == 2)
            {
                // todo: translate me
                _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                    $"We've failed to reorder your controllers. For maximum compatibility, we encourage you to restart HandheldCompanion",
                    ContentDialogButton.Primary, string.Empty, $"{Properties.Resources.ProfilesPage_OK}");
            }
        });
    }

    // A function that returns a random phrase from a list of phrases
    public static string GetRandomPhrase()
    {
        // A list of phrases to be displayed by software while it reorders controllers
        List<string> phrases = new List<string>()
        {
            "Reordering controllers in progress. Please wait a moment.",
            "Hang on tight. We are reordering your controllers for optimal performance.",
            "Your controllers are being reordered. This may take a few seconds.",
            "Reordering controllers. Thank you for your patience.",
            "One moment please. We are reordering your controllers to make them work better.",
            "Reordering controllers. This will not affect your data or settings.",
            "Please do not turn off your device. We are reordering your controllers.",
            "Reordering controllers. Almost done.",
            "Your controllers are being reordered. Please do not interrupt the process.",
            "Reordering controllers. You will be notified when it is complete.",
            "Reordering controllers. This is a routine maintenance task.",
            "Your controllers are being reordered. This will improve your user experience.",
            "Reordering controllers. No action is required from you.",
            "Your controllers are being reordered. Please stand by.",
            "Reordering controllers. This is a quick and easy process.",
            "Your controllers are being reordered. You can continue using your device normally.",
            "Reordering controllers. This will enhance your device's functionality.",
            "Your controllers are being reordered. Please relax and enjoy the music.",
            "Reordering controllers. This is a one-time operation.",
            "Your controllers are being reordered. You will be amazed by the results."
        };

        // A random number generator
        Random random = new Random();

        // A random index between 0 and the number of phrases
        int index = random.Next(phrases.Count);

        // Return the phrase at the random index
        return phrases[index];
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
            var isPlugged = hasTarget;
            var isHidden = hasTarget && target.IsHidden();
            var isSteam = hasTarget && (target is NeptuneController || target is GordonController);
            var isMuted = SettingsManager.GetBoolean("SteamControllerMute");

            DeviceSpecificPanel.Visibility = target is SteamController || target is LegionController ? Visibility.Visible : Visibility.Collapsed;
            MuteVirtualController.Visibility = target is SteamController ? Visibility.Visible : Visibility.Collapsed;
            TouchpadPassthrough.Visibility = target is LegionController ? Visibility.Visible : Visibility.Collapsed;

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
            var notmuted = !isHidden && hasVirtual && (!isSteam || (isSteam && !isMuted));
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

    private void Toggle_ControllerManagement_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("ControllerManagement", Toggle_ControllerManagement.IsOn);
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

    private void Toggle_TouchpadPassthrough_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("LegionControllerPassthrough", Toggle_TouchpadPassthrough.IsOn);
    }
}