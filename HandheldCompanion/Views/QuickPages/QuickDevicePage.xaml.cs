using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Views.Windows;
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
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickDevicePage.xaml
/// </summary>
public partial class QuickDevicePage : Page
{
    private IReadOnlyList<Radio> radios;
    private Timer radioTimer;

    public QuickDevicePage()
    {
        InitializeComponent();

        MultimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;
        MultimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        MultimediaManager.Initialized += MultimediaManager_Initialized;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ProfileManager.Applied += ProfileManager_Applied;
        ProfileManager.Discarded += ProfileManager_Discarded;

        // Device specific
        LegionGoPanel.Visibility = IDevice.GetCurrent() is LegionGo ? Visibility.Visible : Visibility.Collapsed;
        AYANEOFlipDSPanel.Visibility = IDevice.GetCurrent() is AYANEOFlipDS ? Visibility.Visible : Visibility.Collapsed;

        // Capabilities specific
        DynamicLightingPanel.IsEnabled = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.DynamicLighting);

        NightLightToggle.IsEnabled = NightLight.Supported;
        NightLightToggle.IsOn = NightLight.Enabled;

        // manage events
        NightLight.Toggled += NightLight_Toggled;

        // why is that part of a timer ?
        radioTimer = new(1000);
        radioTimer.Elapsed += RadioTimer_Elapsed;
        radioTimer.Start();
    }

    public QuickDevicePage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // Go to profile integer scaling resolution
        if (profile.IntegerScalingEnabled)
        {
            DesktopScreen desktopScreen = MultimediaManager.PrimaryDesktop;

            ScreenDivider? profileResolution = desktopScreen?.screenDividers.FirstOrDefault(d => d.divider == profile.IntegerScalingDivider);
            if (profileResolution is not null)
                SetResolution(profileResolution.resolution);
        }

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            var canChangeDisplay = !profile.IntegerScalingEnabled;
            DisplayStack.IsEnabled = canChangeDisplay;
            ResolutionOverrideStack.Visibility = canChangeDisplay ? Visibility.Collapsed : Visibility.Visible;
        });
    }

    private void ProfileManager_Discarded(Profile profile, bool swapped)
    {
        // don't bother discarding settings, new one will be enforce shortly
        if (swapped)
            return;

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (profile.IntegerScalingEnabled)
            {
                DisplayStack.IsEnabled = true;
                ResolutionOverrideStack.Visibility = Visibility.Collapsed;

                // restore default resolution
                if (profile.IntegerScalingDivider != 1)
                    SetResolution();
            }
        });
    }

    private void NightLight_Toggled(bool enabled)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            NightLightToggle.IsOn = enabled;
        });
    }

    private void SettingsManager_SettingValueChanged(string? name, object value)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "LEDSettingsEnabled":
                    UseDynamicLightingToggle.IsOn = Convert.ToBoolean(value);
                    break;
                case "AYANEOFlipScreenEnabled":
                    Toggle_AYANEOFlipScreen.IsOn = Convert.ToBoolean(value);
                    break;
                case "AYANEOFlipScreenBrightness":
                    Slider_AYANEOFlipScreenBrightness.Value = Convert.ToDouble(value);
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

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (radios is null)
                {
                    WifiToggle.IsEnabled = false;
                    BluetoothToggle.IsEnabled = false;
                    return;
                }

                Radio? wifiRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.WiFi);
                Radio? bluetoothRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);

                // WIFI
                WifiToggle.IsEnabled = wifiRadio != null;
                WifiToggle.IsOn = wifiRadio?.State == RadioState.On;

                // Bluetooth
                BluetoothToggle.IsEnabled = bluetoothRadio != null;
                BluetoothToggle.IsOn = bluetoothRadio?.State == RadioState.On;
            });
        }).Start();
    }

    private void MultimediaManager_Initialized()
    {
        // do something
    }

    private void MultimediaManager_PrimaryScreenChanged(DesktopScreen screen)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            ComboBoxResolution.Items.Clear();
            foreach (ScreenResolution resolution in screen.screenResolutions)
                ComboBoxResolution.Items.Add(resolution);
        });
    }

    private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        // We don't want to change the combobox when it's changed from profile integer scaling
        Profile? currentProfile = ProfileManager.GetCurrent();
        if (currentProfile is not null && currentProfile.IntegerScalingEnabled)
        {
            ProfileManager_Applied(currentProfile, UpdateSource.Background);
            return;
        }

        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            ComboBoxResolution.SelectedItem = resolution;

            int screenFrequency = MultimediaManager.PrimaryDesktop.GetCurrentFrequency();
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
        });
    }

    private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxResolution.SelectedItem is null)
            return;

        ScreenResolution resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;
        int screenFrequency = MultimediaManager.PrimaryDesktop.GetCurrentFrequency();

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
        DesktopScreen desktopScreen = MultimediaManager.PrimaryDesktop;

        if (desktopScreen.devMode.dmPelsWidth == resolution.Width &&
            desktopScreen.devMode.dmPelsHeight == resolution.Height &&
            desktopScreen.devMode.dmDisplayFrequency == frequency &&
            desktopScreen.devMode.dmBitsPerPel == resolution.BitsPerPel)
            return;

        MultimediaManager.SetResolution(resolution.Width, resolution.Height, frequency, resolution.BitsPerPel);
    }

    public void SetResolution(ScreenResolution resolution)
    {
        // update current screen resolution
        MultimediaManager.SetResolution(resolution.Width, resolution.Height, MultimediaManager.PrimaryDesktop.GetCurrentFrequency(), resolution.BitsPerPel);
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

    private void Toggle_LegionGoFanOverride_Toggled(object sender, RoutedEventArgs e)
    {
        if (IDevice.GetCurrent() is LegionGo device)
        {
            ToggleSwitch toggleSwitch = (ToggleSwitch)sender;
            device.SetFanFullSpeedAsync(toggleSwitch.IsOn);
        }
    }

    private void NightLightToggle_Toggled(object sender, RoutedEventArgs e)
    {
        NightLight.Enabled = NightLightToggle.IsOn;
    }

    private async void Toggle_AYANEOFlipScreen_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        bool enabled = Toggle_AYANEOFlipScreen.IsOn;
        if (!enabled)
        {
            // todo: translate me
            Task<ContentDialogResult> dialogTask = new Dialog(OverlayQuickTools.GetCurrent())
            {
                Title = "Warning",
                Content = "To reactivate the lower screen, press the dual screen button on your device.",
                CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                PrimaryButtonText = Properties.Resources.ProfilesPage_OK
            }.ShowAsync();

            await dialogTask; // sync call

            switch (dialogTask.Result)
            {
                case ContentDialogResult.Primary:
                    break;

                default:
                case ContentDialogResult.None:
                    // restore previous state
                    Toggle_AYANEOFlipScreen.IsOn = true;
                    return;
            }

            SettingsManager.SetProperty("AYANEOFlipScreenEnabled", enabled);
        }
    }

    private void Slider_AYANEOFlipScreenBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = Slider_AYANEOFlipScreenBrightness.Value;
        if (double.IsNaN(value))
            return;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("AYANEOFlipScreenBrightness", value);
    }
}