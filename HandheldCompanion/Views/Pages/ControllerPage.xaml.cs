using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        // manage events
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
                case "HIDmode":
                    cB_HidMode.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "HIDstatus":
                    cB_ServiceSwitch.SelectedIndex = Convert.ToInt32(value);
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

    private void ControllerUnplugged(IController Controller, bool IsPowerCycling, bool WasTarget)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SimpleStackPanel targetPanel = Controller.IsVirtual() ? VirtualDevices : PhysicalDevices;

            // Search for an existing controller, remove it
            foreach (IController ctrl in targetPanel.Children)
            {
                if (ctrl.GetContainerInstancePath() == Controller.GetContainerInstancePath())
                {
                    if (!IsPowerCycling)
                    {
                        targetPanel.Children.Remove(ctrl);
                        break;
                    }
                }
            }

            ControllerRefresh();
        });
    }

    private void ControllerPlugged(IController Controller, bool IsPowerCycling)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SimpleStackPanel targetPanel = Controller.IsVirtual() ? VirtualDevices : PhysicalDevices;

            // Search for an existing controller, remove it
            foreach (IController ctrl in targetPanel.Children)
                if (ctrl.GetContainerInstancePath() == Controller.GetContainerInstancePath())
                    return;

            // Add new controller to list if no existing controller was found
            targetPanel.Children.Add(Controller);

            // todo: move me
            Button ui_button_hook = Controller.GetButtonHook();
            ui_button_hook.Click += (sender, e) => ControllerHookClicked(Controller);

            Button ui_button_hide = Controller.GetButtonHide();
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
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            // status: 0:wip, 1:sucess, 2:failed
            switch (status)
            {
                case 0:
                    ControllerLoading.Visibility = Visibility.Visible;
                    VirtualDevices.IsEnabled = false;
                    PhysicalDevices.IsEnabled = false;
                    MainGrid.IsEnabled = false;
                    break;
                case 1:
                case 2:
                    ControllerLoading.Visibility = Visibility.Hidden;
                    VirtualDevices.IsEnabled = true;
                    PhysicalDevices.IsEnabled = true;
                    MainGrid.IsEnabled = true;
                    break;
            }

            ControllerRefresh();

            // failed
            if (status == 2)
            {
                // todo: translate me
                Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                {
                    Title = Properties.Resources.SettingsPage_UpdateWarning,
                    Content = $"We've failed to reorder your controllers. For maximum compatibility, we encourage you to restart HandheldCompanion",
                    DefaultButton = ContentDialogButton.Close,
                    CloseButtonText = Properties.Resources.ControllerPage_Close,
                    PrimaryButtonText = Properties.Resources.ControllerPage_TryAgain
                }.ShowAsync();

                await dialogTask; // sync call

                switch (dialogTask.Result)
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
        if (Controller.IsBusy)
            return;

        var path = Controller.GetContainerInstancePath();
        ControllerManager.SetTargetController(path, false);

        ControllerRefresh();
    }

    private void ControllerHideClicked(IController Controller)
    {
        if (Controller.IsBusy)
            return;

        if (Controller.IsHidden())
            Controller.Unhide();
        else
            Controller.Hide();

        ControllerRefresh();
    }

    private void ControllerRefresh()
    {
        IController targetController = ControllerManager.GetTargetController();
        bool hasPhysical = ControllerManager.HasPhysicalController();
        bool hasVirtual = ControllerManager.HasVirtualController();
        bool hasTarget = targetController != null;
        bool isMuted = SettingsManager.GetBoolean("SteamControllerMute");

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            PhysicalDevices.Visibility = hasPhysical ? Visibility.Visible : Visibility.Collapsed;
            WarningNoPhysical.Visibility = !hasPhysical ? Visibility.Visible : Visibility.Collapsed;

            bool isPlugged = hasTarget;
            bool isHidden = hasTarget && targetController.IsHidden();
            bool isSteam = hasTarget && (targetController is NeptuneController || targetController is GordonController);

            MuteVirtualController.Visibility = targetController is SteamController ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller, but is not connected
            HintsNoPhysicalConnected.Visibility =
                hasPhysical && !isPlugged ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller (not Neptune) hidden, but no virtual controller
            VirtualDevices.Visibility = hasVirtual ? Visibility.Visible : Visibility.Collapsed;
            WarningNoVirtual.Visibility = !hasVirtual ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller (Neptune) hidden, but virtual controller is muted
            bool neptunehidden = isHidden && isSteam && isMuted;
            HintsNeptuneHidden.Visibility = neptunehidden ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller not hidden, and virtual controller
            bool notmuted = !isHidden && hasVirtual && (!isSteam || (isSteam && !isMuted));
            HintsNotMuted.Visibility = notmuted ? Visibility.Visible : Visibility.Collapsed;

            Hints.Visibility =  (HintsNoPhysicalConnected.Visibility == Visibility.Visible ||
                                HintsHIDManagedByProfile.Visibility == Visibility.Visible ||
                                HintsNeptuneHidden.Visibility == Visibility.Visible ||
                                HintsNotMuted.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_HidMode.SelectedIndex == -1)
            return;

        controllerMode = (HIDmode)cB_HidMode.SelectedIndex;

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
}