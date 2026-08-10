using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Devices.xaml
/// </summary>
public partial class ControllerPage : Page
{
    private readonly ControllerPageViewModel ViewModel;

    public ControllerPage()
    {
        ViewModel = new ControllerPageViewModel();
        DataContext = ViewModel;
        InitializeComponent();

        UpdateTrackpadSettingsVisibility();
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

        // manage events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("HIDcloakonconnect", ManagerFactory.settingsManager.GetString("HIDcloakonconnect"), false, false);
        SettingsManager_SettingValueChanged("HIDuncloakonclose", ManagerFactory.settingsManager.GetString("HIDuncloakonclose"), false, false);
        SettingsManager_SettingValueChanged("HIDuncloakondisconnect", ManagerFactory.settingsManager.GetString("HIDuncloakondisconnect"), false, false);
        SettingsManager_SettingValueChanged("HIDvibrateonconnect", ManagerFactory.settingsManager.GetString("HIDvibrateonconnect"), false, false);
        SettingsManager_SettingValueChanged("VibrationStrength", ManagerFactory.settingsManager.GetString("VibrationStrength"), false, false);
        SettingsManager_SettingValueChanged("SteamControllerMode", ManagerFactory.settingsManager.GetString("SteamControllerMode"), false, false);
        SettingsManager_SettingValueChanged("TrackpadClickHaptics", ManagerFactory.settingsManager.GetString("TrackpadClickHaptics"), false, false);
        SettingsManager_SettingValueChanged("SteamControllerRumbleInterval", ManagerFactory.settingsManager.GetString("SteamControllerRumbleInterval"), false, false);
        SettingsManager_SettingValueChanged("HIDmode", ManagerFactory.settingsManager.GetString("HIDmode"), false, false);
        SettingsManager_SettingValueChanged("HIDstatus", ManagerFactory.settingsManager.GetString("HIDstatus"), false, false);
        SettingsManager_SettingValueChanged("ControllerPlugBehavior", ManagerFactory.settingsManager.GetString("ControllerPlugBehavior"), false, false);
        SettingsManager_SettingValueChanged("MasterInterval", ManagerFactory.settingsManager.GetString("MasterInterval"), false, false);
    }

    public ControllerPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
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
                case "TrackpadClickHaptics":
                    cB_TrackpadClickHaptics.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "SteamControllerRumbleInterval":
                    SliderInterval.Value = Convert.ToDouble(value);
                    break;
                case "HIDmode":
                    {
                        foreach (object? item in cB_HidMode.Items)
                        {
                            if (item is ComboBoxItem comboBoxItem && comboBoxItem.Tag is string hidRaw && Convert.ToInt32(hidRaw) == Convert.ToInt32(value))
                            {
                                cB_HidMode.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    break;
                case "HIDstatus":
                    cB_ServiceSwitch.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "ConnectOnPlug":
                    // legacy setting: migrate to ControllerPlugBehavior
                    bool connectOnPlug = Convert.ToBoolean(value);
                    ManagerFactory.settingsManager.SetProperty("ControllerPlugBehavior", connectOnPlug ? 0 : 2);
                    break;
                case "ControllerPlugBehavior":
                    cB_ControllerPlugBehavior.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "MasterInterval":
                    if (FindName("cB_MasterInterval") is ComboBox masterIntervalComboBox)
                        masterIntervalComboBox.SelectedIndex = Convert.ToInt32(value);
                    break;
            }
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
        ViewModel.Dispose();
    }

    private void ControllerManager_ControllerSelected(HandheldCompanion.Controllers.IController controller)
    {
        UIHelper.TryInvoke(UpdateTrackpadSettingsVisibility);
    }

    private void UpdateTrackpadSettingsVisibility()
    {
        bool isSteamDeck = IDevice.GetCurrent() is SteamDeck;
        IController? controller = ControllerManager.GetTarget();
        if (controller is null)
            return;

        bool hasTrackpadClicks = controller.HasSourceButton(ButtonFlags.LeftPadClick) || controller.HasSourceButton(ButtonFlags.RightPadClick);

        SteamDeckPanel.Visibility = isSteamDeck ? Visibility.Visible : Visibility.Collapsed;
        TrackpadHapticsPanel.Visibility = isSteamDeck || hasTrackpadClicks ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetResourceString(string baseKey, int attempts)
    {
        // Combine the base key with the attempts number to form the resource key
        string resourceKey = $"{baseKey}{attempts}";
        return Properties.Resources.ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture) ?? string.Empty;
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

        // only change HIDmode setting is set to default controller
        Profile currentProfile = ManagerFactory.profileManager.GetCurrent();
        if (currentProfile.HID != HIDmode.NotSelected)
            return;

        if (cB_HidMode.SelectedItem is ComboBoxItem comboBoxItem && comboBoxItem.Tag is string hidRaw)
        {
            int hidValue = Convert.ToInt32(hidRaw);
            ManagerFactory.settingsManager.SetProperty("HIDmode", hidValue);
        }
    }

    private void cB_ServiceSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (cB_ServiceSwitch.SelectedIndex == -1)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDstatus", cB_ServiceSwitch.SelectedIndex);
    }

    private void cB_MasterInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is not ComboBox masterIntervalComboBox || masterIntervalComboBox.SelectedIndex == -1)
            return;

        ManagerFactory.settingsManager.SetProperty("MasterInterval", masterIntervalComboBox.SelectedIndex);
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
        Layout? desktopLayout = ManagerFactory.layoutManager.GetDesktop();
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

    private void cB_SCModeController_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("SteamControllerMode", cB_SCModeController.SelectedIndex);
    }

    private void cB_TrackpadClickHaptics_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || cB_TrackpadClickHaptics.SelectedIndex == -1)
            return;

        ManagerFactory.settingsManager.SetProperty("TrackpadClickHaptics", cB_TrackpadClickHaptics.SelectedIndex);
    }

    private void cB_ControllerPlugBehavior_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("ControllerPlugBehavior", cB_ControllerPlugBehavior.SelectedIndex);
    }

    private void Toggle_UncloakOnDisconnect_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("HIDuncloakondisconnect", Toggle_UncloakOnDisconnect.IsOn);
    }
}
