using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Linq;
using System.Threading;
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
    // controllers vars
    private HIDmode controllerMode = HIDmode.NoController;
    private HIDstatus controllerStatus = HIDstatus.Disconnected;

    public ControllerPage()
    {
        InitializeComponent();

        // initialize components
        foreach (var mode in ((HIDmode[])Enum.GetValues(typeof(HIDmode))).Where(a => a != HIDmode.NoController && a != HIDmode.NotSelected))
            cB_HidMode.Items.Add(EnumUtils.GetDescriptionFromEnumValue(mode));

        foreach (var status in (HIDstatus[])Enum.GetValues(typeof(HIDstatus)))
            cB_ServiceSwitch.Items.Add(EnumUtils.GetDescriptionFromEnumValue(status));

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        ControllerManager.ControllerPlugged += ControllerPlugged;
        ControllerManager.ControllerUnplugged += ControllerUnplugged;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
        ControllerManager.Working += ControllerManager_Working;
        ProfileManager.Applied += ProfileManager_Applied;

        VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;
    }

    public ControllerPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // disable emulated controller combobox if profile is not default or set to default controller
            if (!profile.Default && (HIDmode)profile.HID != HIDmode.NotSelected)
            {
                cB_HidMode.IsEnabled = false;
                HintsHIDManagedByProfile.Visibility = Visibility.Visible;
            }
            else
            {
                cB_HidMode.IsEnabled = true;
                HintsHIDManagedByProfile.Visibility = Visibility.Collapsed;
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

        if (Controller.IsVirtual())
            Controller.UserIndexChanged -= Controller_UserIndexChanged;
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

        if (Controller.IsVirtual())
            Controller.UserIndexChanged += Controller_UserIndexChanged;
    }

    private void ControllerManager_ControllerSelected(IController Controller)
    {
        // UI thread (async)
        ControllerRefresh();
    }

    private void SetVirtualControllerVisualIndex(int value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (FrameworkElement frameworkElement in UserIndexPanel.Children)
            {
                if (frameworkElement is not Border)
                    continue;

                Border border = (Border)frameworkElement;
                int idx = UserIndexPanel.Children.IndexOf(border);

                if (idx == value)
                    border.SetResourceReference(BackgroundProperty, "AccentAAFillColorDefaultBrush");
                else
                    border.SetResourceReference(BackgroundProperty, "SystemControlForegroundBaseLowBrush");
            }
        });
    }

    private int workingIdx = 0;
    private Thread workingThread;
    private bool workingThreadRunning;

    private void workingThreadLoop(object? obj)
    {
        int maxIdx = 8;
        int direction = 1; // 1 for increasing, -1 for decreasing

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            maxIdx = UserIndexPanel.Children.Count;
        });

        while (workingThreadRunning)
        {
            workingIdx += direction; // increment or decrement the index
            if (workingIdx == maxIdx - 1 || workingIdx == 0) // if the index reaches the limit or zero
            {
                direction = -direction; // reverse the direction
            }

            SetVirtualControllerVisualIndex(workingIdx);

            Thread.Sleep(100);
        }
    }

    private void ControllerManager_Working(int status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            // status: 0:wip, 1:sucess, 2:failed
            switch (status)
            {
                case 0:
                    ControllerLoading.Visibility = Visibility.Visible;
                    InputDevices.IsEnabled = false;
                    ControllerGrid.IsEnabled = false;

                    if (!workingThreadRunning)
                    {
                        workingThreadRunning = true;

                        workingThread = new Thread(workingThreadLoop);
                        workingThread.IsBackground = true;
                        workingThread.Start();
                    }
                    break;
                case 1:
                case 2:
                    ControllerLoading.Visibility = Visibility.Hidden;
                    InputDevices.IsEnabled = true;
                    ControllerGrid.IsEnabled = true;

                    if (workingThreadRunning)
                    {
                        workingThreadRunning = false;
                        workingThread.Join();
                    }
                    break;
            }

            ControllerRefresh();

            // failed
            if (status == 2)
            {
                // todo: translate me
                var result = Dialog.ShowAsync(
                    Properties.Resources.SettingsPage_UpdateWarning,
                    $"We've failed to reorder your controllers. For maximum compatibility, we encourage you to restart HandheldCompanion",
                    ContentDialogButton.Close,
                    Properties.Resources.ControllerPage_TryAgain,
                    Properties.Resources.ControllerPage_Close);

                await result; // sync call

                switch (result.Result)
                {
                    default:
                    case ContentDialogResult.Primary:
                            Toggle_ControllerManagement.IsOn = false;
                        break;
                    case ContentDialogResult.None:
                            Toggle_ControllerManagement.IsOn = true;
                        break;
                }
            }
        });
    }

    private void Controller_UserIndexChanged(byte UserIndex)
    {
        SetVirtualControllerVisualIndex(UserIndex);
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
            // we're busy
            if (ControllerLoading.Visibility is Visibility.Visible)
                return;

            bool hasPhysical = ControllerManager.HasPhysicalController();
            bool hasVirtual = ControllerManager.HasVirtualController();
            bool hasTarget = ControllerManager.GetTargetController() != null;

            // check: do we have any plugged physical controller
            InputDevices.Visibility = hasPhysical ? Visibility.Visible : Visibility.Collapsed;
            WarningNoPhysical.Visibility = !hasPhysical ? Visibility.Visible : Visibility.Collapsed;

            IController targetController = ControllerManager.GetTargetController();
            IController virtualController = ControllerManager.GetVirtualControllers().FirstOrDefault();
            int idx = virtualController is null ? -1 : virtualController.GetUserIndex();
            SetVirtualControllerVisualIndex(idx);

            bool isPlugged = hasTarget;
            bool isHidden = hasTarget && targetController.IsHidden();
            bool isSteam = hasTarget && (targetController is NeptuneController || targetController is GordonController);
            bool isMuted = SettingsManager.GetBoolean("SteamControllerMute");

            DeviceSpecificPanel.Visibility = targetController is SteamController || targetController is LegionController ? Visibility.Visible : Visibility.Collapsed;
            MuteVirtualController.Visibility = targetController is SteamController ? Visibility.Visible : Visibility.Collapsed;
            TouchpadPassthrough.Visibility = targetController is LegionController ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller, but is not connected
            HintsNoPhysicalConnected.Visibility =
                hasPhysical && !isPlugged ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller (not Neptune) hidden, but no virtual controller
            bool hiddenbutnovirtual = isHidden && !hasVirtual;
            HintsNoVirtual.Visibility = hiddenbutnovirtual ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller (Neptune) hidden, but virtual controller is muted
            bool neptunehidden = isHidden && isSteam && isMuted;
            HintsNeptuneHidden.Visibility = neptunehidden ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller not hidden, and virtual controller
            bool notmuted = !isHidden && hasVirtual && (!isSteam || (isSteam && !isMuted));
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

        // only change HIDmode setting if current profile is default or set to default controller
        var currentProfile = ProfileManager.GetCurrent();
        if (currentProfile.Default || (HIDmode)currentProfile.HID == HIDmode.NotSelected)
        {
            SettingsManager.SetProperty("HIDmode", cB_HidMode.SelectedIndex);
        }
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

    private void Toggle_TouchpadPassthrough_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("LegionControllerPassthrough", Toggle_TouchpadPassthrough.IsOn);
    }
}