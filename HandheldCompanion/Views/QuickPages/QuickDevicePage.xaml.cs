using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using static HandheldCompanion.Utils.DeviceUtils;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickDevicePage.xaml
/// </summary>
public partial class QuickDevicePage : Page
{
    private IReadOnlyList<Radio> radios;
    private Timer radioTimer;

    public QuickDevicePage(string Tag) : this()
    {
        this.Tag = Tag;

        SystemManager.PrimaryScreenChanged += DesktopManager_PrimaryScreenChanged;
        SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ProfileManager.Applied += ProfileManager_Applied;
        ProfileManager.Discarded += ProfileManager_Discarded;

        radioTimer = new(1000);
        radioTimer.Elapsed += RadioTimer_Elapsed;
        radioTimer.Start();
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // Go to profile integer scaling resolution
        if (profile.IntegerScalingEnabled)
        {
            DesktopScreen desktopScreen = SystemManager.GetDesktopScreen();
            var profileResolution = desktopScreen?.screenDividers.FirstOrDefault(d => d.divider == profile.IntegerScalingDivider);
            if (profileResolution is not null)
            {
                SetResolution(profileResolution.resolution);
            }
        }
        else
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // Revert back to resolution in device settings
                SetResolution();
            });
        }

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var canChangeDisplay = !profile.IntegerScalingEnabled;
            DisplayStack.IsEnabled = canChangeDisplay;
            ResolutionOverrideStack.Visibility = canChangeDisplay ? Visibility.Collapsed : Visibility.Visible;

        });
    }

    private void ProfileManager_Discarded(Profile profile)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SetResolution();

            if (profile.IntegerScalingEnabled)
            {
                DisplayStack.IsEnabled = true;
                ResolutionOverrideStack.Visibility = Visibility.Collapsed;
            }
        });
    }

    public QuickDevicePage()
    {
        InitializeComponent();

        LegionGoPanel.Visibility = MainWindow.CurrentDevice is LegionGo ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SettingsManager_SettingValueChanged(string? name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
                case "LEDSettingsEnabled":
                    UseDynamicLightingToggle.IsOn = Convert.ToBoolean(value);
                    break;
            }
        });
    }

    private void RadioTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        new Task(async () =>
        {
            // Get the Bluetooth adapter
            BluetoothAdapter adapter = await BluetoothAdapter.GetDefaultAsync();

            // Get the Bluetooth radio
            radios = await Radio.GetRadiosAsync();

            // UI thread (async)
            _ = Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // WIFI
                WifiToggle.IsEnabled = radios.Where(radio => radio.Kind == RadioKind.WiFi).Any();
                WifiToggle.IsOn = radios.Where(radio => radio.Kind == RadioKind.WiFi && radio.State == RadioState.On).Any();

                // Bluetooth
                BluetoothToggle.IsEnabled = radios.Where(radio => radio.Kind == RadioKind.Bluetooth).Any();
                BluetoothToggle.IsOn = radios.Where(radio => radio.Kind == RadioKind.Bluetooth && radio.State == RadioState.On).Any();
            });
        }).Start();
    }

    private void DesktopManager_PrimaryScreenChanged(DesktopScreen screen)
    {
        ComboBoxResolution.Items.Clear();
        foreach (ScreenResolution resolution in screen.screenResolutions)
            ComboBoxResolution.Items.Add(resolution);
    }

    private void DesktopManager_DisplaySettingsChanged(ScreenResolution resolution)
    {
        // We don't want to change the combobox when it's changed from profile integer scaling
        var currentProfile = ProfileManager.GetCurrent();
        if (ComboBoxResolution.SelectedItem is not null && currentProfile is not null && currentProfile.IntegerScalingEnabled)
            return;

        ComboBoxResolution.SelectedItem = resolution;

        int screenFrequency = SystemManager.GetDesktopScreen().GetCurrentFrequency();
        foreach (ComboBoxItem comboBoxItem in ComboBoxFrequency.Items)
        {
            if (comboBoxItem.Tag is int frequency)
            {
                if (frequency == screenFrequency)
                {
                    ComboBoxFrequency.SelectedItem = comboBoxItem;
                    break;
                }
            }
        }
    }

    private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxResolution.SelectedItem is null)
            return;

        ScreenResolution resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;
        int screenFrequency = SystemManager.GetDesktopScreen().GetCurrentFrequency();

        ComboBoxFrequency.Items.Clear();
        foreach (int frequency in resolution.Frequencies.Keys)
        {
            ComboBoxItem comboBoxItem = new()
            {
                Content = $"{frequency} Hz",
                Tag = frequency,
            };

            ComboBoxFrequency.Items.Add(comboBoxItem);

            if (frequency == screenFrequency)
                ComboBoxFrequency.SelectedItem = comboBoxItem;
        }

        SetResolution();
    }

    private void ComboBoxFrequency_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxFrequency.SelectedItem is null)
            return;

        SetResolution();
    }

    private void SetResolution()
    {
        if (ComboBoxResolution.SelectedItem is null)
            return;

        if (ComboBoxFrequency.SelectedItem is null)
            return;

        ScreenResolution resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;
        int frequency = (int)((ComboBoxItem)ComboBoxFrequency.SelectedItem).Tag;

        // update current screen resolution
        SystemManager.SetResolution(resolution.Width, resolution.Height, frequency, resolution.BitsPerPel);
    }

    public void SetResolution(ScreenResolution resolution)
    {
        // update current screen resolution
        SystemManager.SetResolution(resolution.Width, resolution.Height, SystemManager.GetDesktopScreen().GetCurrentFrequency(), resolution.BitsPerPel);
    }

    private void WIFIToggle_Toggled(object sender, RoutedEventArgs e)
    {
        foreach (Radio radio in radios.Where(r => r.Kind == RadioKind.WiFi))
            _ = radio.SetStateAsync(WifiToggle.IsOn ? RadioState.On : RadioState.Off);
    }

    private void BluetoothToggle_Toggled(object sender, RoutedEventArgs e)
    {
        foreach (Radio radio in radios.Where(r => r.Kind == RadioKind.Bluetooth))
            _ = radio.SetStateAsync(BluetoothToggle.IsOn ? RadioState.On : RadioState.Off);
    }

    private void UseDynamicLightingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("LEDSettingsEnabled", UseDynamicLightingToggle.IsOn);
    }

    internal void Close()
    {
        radioTimer.Stop();
    }

    private void Toggle_cFFanSpeed_Toggled(object sender, RoutedEventArgs e)
    {
        if (MainWindow.CurrentDevice is LegionGo device)
        {
            ToggleSwitch toggleSwitch = (ToggleSwitch)sender;
            device.SetFanFullSpeed(toggleSwitch.IsOn);
        }
    }
}