using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using Inkore.UI.WPF.Modern.Controls;
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

        radioTimer = new(1000);
        radioTimer.Elapsed += RadioTimer_Elapsed;
        radioTimer.Start();
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
        foreach (var resolution in screen.resolutions)
            ComboBoxResolution.Items.Add(resolution);
    }

    private void DesktopManager_DisplaySettingsChanged(ScreenResolution resolution)
    {
        ComboBoxResolution.SelectedItem = resolution;
        ComboBoxFrequency.SelectedItem = SystemManager.GetDesktopScreen().GetFrequency();
    }

    private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxResolution.SelectedItem is null)
            return;

        var resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;

        ComboBoxFrequency.Items.Clear();
        foreach (var frequency in resolution.Frequencies.Values)
            ComboBoxFrequency.Items.Add(frequency);

        ComboBoxFrequency.SelectedItem = SystemManager.GetDesktopScreen().GetFrequency();

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

        var resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;
        var frequency = (ScreenFrequency)ComboBoxFrequency.SelectedItem;

        // update current screen resolution
        SystemManager.SetResolution(resolution.Width, resolution.Height, (int)frequency.GetValue(Frequency.Full), resolution.BitsPerPel);
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