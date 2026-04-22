using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Targets;
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
        DataContext = new ControllerPageViewModel();
        InitializeComponent();

        cB_HidModeDInputItem.Visibility = VJoyTarget.IsInstalled() ? Visibility.Visible : Visibility.Collapsed;

        SteamDeckPanel.Visibility = IDevice.GetCurrent() is SteamDeck ? Visibility.Visible : Visibility.Collapsed;

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
    }

    public ControllerPage(string Tag) : this()
    {
        this.Tag = Tag;
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
                    Toggle_UncloakOnClose.IsOn = Convert.ToBoolean(value);
                    break;
                case "HIDuncloakondisconnect":
                    Toggle_UncloakOnDisconnect.IsOn = Convert.ToBoolean(value);
                    break;
                case "HIDvibrateonconnect":
                    Toggle_Vibrate.IsOn = Convert.ToBoolean(value);
                    break;
                case "VibrationStrength":
                    SliderStrength.Value = Convert.ToDouble(value);
                    break;
                case "SteamControllerMode":
                    cB_SCModeController.SelectedIndex = Convert.ToInt32(value);
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
                case "ConnectOnPlug":
                    Toggle_ConnectOnPlug.IsOn = Convert.ToBoolean(value);
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

    private void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (cB_HidMode.SelectedIndex == -1)
            return;

        // only change HIDmode setting if current profile is default or set to default controller
        var currentProfile = ManagerFactory.profileManager.GetCurrent();
        if (currentProfile.Default || currentProfile.HID == HIDmode.NotSelected)
            ManagerFactory.settingsManager.SetProperty("HIDmode", cB_HidMode.SelectedIndex);
    }

    private void cB_ServiceSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (cB_HidMode.SelectedIndex == -1)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDstatus", cB_ServiceSwitch.SelectedIndex);
    }

    private void cB_ControllerSlotManagementMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (cB_ControllerSlotManagementMode.SelectedIndex == -1)
            return;

        ManagerFactory.settingsManager.SetProperty("ControllerSlotManagementMode", cB_ControllerSlotManagementMode.SelectedIndex);
    }

    private void b_SlotFixNow_Click(object sender, RoutedEventArgs e)
    {
        // Manual backup option when toast was ignored.
        ControllerManager.TriggerSlotFix(resetAttempts: true);
    }


    private void Toggle_Cloaked_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDcloakonconnect", Toggle_Cloaked.IsOn);
    }

    private void Toggle_UncloakOnClose_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDuncloakonclose", Toggle_UncloakOnClose.IsOn);
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

    private void Toggle_ConnectOnPlug_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("ConnectOnPlug", Toggle_ConnectOnPlug.IsOn);
    }

    private void Toggle_UncloakOnDisconnect_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDuncloakondisconnect", Toggle_UncloakOnDisconnect.IsOn);
    }
}