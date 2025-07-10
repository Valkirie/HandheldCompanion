using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using static HandheldCompanion.Managers.ControllerManager;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Devices.xaml
/// </summary>
public partial class ControllerPage : Page
{
    public ControllerPage()
    {
        DataContext = new ControllerPageViewModel(this);
        InitializeComponent();

        SteamDeckPanel.Visibility = IDevice.GetCurrent() is SteamDeck ? Visibility.Visible : Visibility.Collapsed;

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ControllerManager.StatusChanged += ControllerManager_Working;
        ManagerFactory.profileManager.Applied += ProfileManager_Applied;
    }

    public ControllerPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // disable emulated controller combobox if profile is not default or set to default controller
            if (!profile.Default && profile.HID != HIDmode.NotSelected)
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
    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
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
                case "SteamControllerMode":
                    cB_SCModeController.SelectedIndex = Convert.ToInt32(value);
                    ControllerRefresh();
                    break;
                case "SteamControllerRumbleInterval":
                    SliderInterval.Value = Convert.ToDouble(value);
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
        ((ControllerPageViewModel)DataContext).Dispose();
    }

    private void ControllerManager_Working(ControllerManagerStatus status, int attempts)
    {
        // UI thread
        UIHelper.TryInvoke(async () =>
        {
            switch (status)
            {
                case ControllerManagerStatus.Busy:
                    ControllerSettings.IsEnabled = false;
                    ScanHardwareCard.IsEnabled = false;
                    break;

                case ControllerManagerStatus.Succeeded:
                    ControllerSettings.IsEnabled = true;
                    ScanHardwareCard.IsEnabled = true;
                    break;

                case ControllerManagerStatus.Failed:
                    ControllerSettings.IsEnabled = true;
                    ScanHardwareCard.IsEnabled = true;
                    break;
            }

            // here ?
            ControllerRefresh();
        });
    }

    private string GetResourceString(string baseKey, int attempts)
    {
        // Combine the base key with the attempts number to form the resource key
        string resourceKey = $"{baseKey}{attempts}";
        return Properties.Resources.ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture);
    }

    private TextBlock CreateFormattedContent(string title, string description)
    {
        TextBlock textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };

        textBlock.Inlines.Add(new Run { Text = title, FontWeight = FontWeights.Bold });
        //textBlock.Inlines.Add(new LineBreak());
        textBlock.Inlines.Add(new Run { Text = description });

        return textBlock;
    }

    public void ControllerRefresh()
    {
        IController targetController = ControllerManager.GetTarget();
        bool hasPhysical = ControllerManager.HasPhysicalController<IController>();
        bool hasVirtual = ControllerManager.HasVirtualController<IController>();
        bool hasTarget = targetController != null;

        bool isPlugged = hasPhysical && hasTarget;
        bool isHidden = targetController is not null && targetController.IsHidden();

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            PhysicalDevices.Visibility = hasPhysical ? Visibility.Visible : Visibility.Collapsed;
            WarningNoPhysical.Visibility = !hasPhysical ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller hidden, but no virtual controller
            VirtualDevices.Visibility = hasVirtual ? Visibility.Visible : Visibility.Collapsed;
            WarningNoVirtual.Visibility = isHidden && !hasVirtual ? Visibility.Visible : Visibility.Collapsed;

            // hint: Has physical controller not hidden, and virtual controller
            bool hasDualInput = isPlugged && !isHidden && hasVirtual;
            HintsNotMuted.Visibility = hasDualInput ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_HidMode.SelectedIndex == -1)
            return;

        // only change HIDmode setting if current profile is default or set to default controller
        var currentProfile = ManagerFactory.profileManager.GetCurrent();
        if (currentProfile.Default || currentProfile.HID == HIDmode.NotSelected)
        {
            ManagerFactory.settingsManager.SetProperty("HIDmode", cB_HidMode.SelectedIndex);
        }
    }

    private void cB_ServiceSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_HidMode.SelectedIndex == -1)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDstatus", cB_ServiceSwitch.SelectedIndex);
    }

    private void Toggle_Cloaked_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDcloakonconnect", Toggle_Cloaked.IsOn);
    }

    private void Toggle_Uncloak_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDuncloakonclose", Toggle_Uncloak.IsOn);
    }

    private void SliderStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("VibrationStrength", SliderStrength.Value);
    }

    private void SliderInterval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("SteamControllerRumbleInterval", Convert.ToInt32(SliderInterval.Value));
    }

    private void Toggle_Vibrate_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDvibrateonconnect", Toggle_Vibrate.IsOn);
    }

    private void Toggle_ControllerManagement_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("ControllerManagement", Toggle_ControllerManagement.IsOn);
    }

    private void Button_Layout_Click(object sender, RoutedEventArgs e)
    {
        Layout desktopLayout = ManagerFactory.layoutManager.GetDesktop();
        if (desktopLayout is null)
            return;

        // prepare layout editor, desktopLayout gets saved automatically
        LayoutTemplate desktopTemplate = new(desktopLayout)
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

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        ((Expander)sender).BringIntoView();
    }

    private void cB_SCModeController_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("SteamControllerMode", Convert.ToBoolean(cB_SCModeController.SelectedIndex));
    }
}