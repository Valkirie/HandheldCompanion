using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
    private DispatcherTimer radioTimer;

    public QuickDevicePage()
    {
        InitializeComponent();

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ManagerFactory.profileManager.Applied += ProfileManager_Applied;
        ManagerFactory.profileManager.Discarded += ProfileManager_Discarded;
        NightLight.Toggled += NightLight_Toggled;

        // Device specific
        AYANEOFlipDSPanel.Visibility = IDevice.GetCurrent() is AYANEOFlipDS ? Visibility.Visible : Visibility.Collapsed;

        // Capabilities specific
        DynamicLightingPanel.IsEnabled = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.DynamicLighting);
        FanOverridePanel.Visibility = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.FanOverride) ? Visibility.Visible : Visibility.Collapsed;

        NightLightToggle.IsEnabled = NightLight.Supported;
        NightLightToggle.IsOn = NightLight.Enabled;

        radioTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2) // 2s is plenty; go 5s if you like
        };
        radioTimer.Tick += RadioTimer_Tick;
        radioTimer.Start();
    }

    public void Close()
    {
        // manage events
        ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
        ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;
        ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
        ManagerFactory.profileManager.Discarded -= ProfileManager_Discarded;
        NightLight.Toggled -= NightLight_Toggled;

        radioTimer.Stop();
    }

    public QuickDevicePage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private bool _loadedOnce;
    private void QuickDevice_Loaded(object sender, RoutedEventArgs e)
    {
        // one-time setup
        if (_loadedOnce) return;
        _loadedOnce = true;

        // raise events
        switch (ManagerFactory.multimediaManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryMedia();
                break;
        }
    }

    private void QueryMedia()
    {
        // manage events
        ManagerFactory.multimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;
        ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;

        // raise events
        if (ManagerFactory.multimediaManager.PrimaryDesktop is not null)
        {
            MultimediaManager_PrimaryScreenChanged(ManagerFactory.multimediaManager.PrimaryDesktop);
            MultimediaManager_DisplaySettingsChanged(ManagerFactory.multimediaManager.PrimaryDesktop, ManagerFactory.multimediaManager.PrimaryDesktop.GetResolution());
        }
    }

    private void MultimediaManager_Initialized()
    {
        QueryMedia();
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // Go to profile integer scaling resolution
        if (profile.IntegerScalingEnabled)
        {
            DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;

            ScreenDivider? profileResolution = desktopScreen?.screenDividers.FirstOrDefault(d => d.divider == profile.IntegerScalingDivider);
            if (profileResolution is not null)
                SetResolution(profileResolution.resolution);
        }

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            var canChangeDisplay = !profile.IntegerScalingEnabled;
            DisplayStack.IsEnabled = canChangeDisplay;
            ResolutionOverrideStack.Visibility = canChangeDisplay ? Visibility.Collapsed : Visibility.Visible;
        });
    }

    private void ProfileManager_Discarded(Profile profile, bool swapped, Profile nextProfile)
    {
        // don't bother discarding settings, new one will be enforce shortly
        if (swapped && nextProfile.IntegerScalingEnabled)
            return;

        if (profile.IntegerScalingEnabled)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                DisplayStack.IsEnabled = true;
                ResolutionOverrideStack.Visibility = Visibility.Collapsed;
            });

            // restore default resolution
            if (profile.IntegerScalingDivider != 1)
                SetResolution();
        }
    }

    private void NightLight_Toggled(bool enabled)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            NightLightToggle.IsOn = enabled;
        });
    }

    private void SettingsManager_SettingValueChanged(string? name, object value, bool temporary)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
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

    private async void RadioTimer_Tick(object? sender, EventArgs e)
    {
        // Query radios directly; we’re on the UI dispatcher, no new Task()
        var adapter = await BluetoothAdapter.GetDefaultAsync();
        radios = await Radio.GetRadiosAsync();

        WifiToggle.IsEnabled = radios?.Any(r => r.Kind == RadioKind.WiFi) == true;
        BluetoothToggle.IsEnabled = radios?.Any(r => r.Kind == RadioKind.Bluetooth) == true;

        var wifi = radios?.FirstOrDefault(r => r.Kind == RadioKind.WiFi);
        var bt = radios?.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);

        WifiToggle.IsOn = wifi?.State == RadioState.On;
        BluetoothToggle.IsOn = bt?.State == RadioState.On;
    }

    private CrossThreadLock multimediaLock = new();
    private void MultimediaManager_PrimaryScreenChanged(DesktopScreen screen)
    {
        if (multimediaLock.TryEnter())
        {
            try
            {
                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    ComboBoxResolution.Items.Clear();
                    foreach (ScreenResolution resolution in screen.screenResolutions)
                        ComboBoxResolution.Items.Add(resolution);
                });
            }
            catch { }
            finally
            {
                multimediaLock.Exit();
            }
        }
    }

    private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        if (multimediaLock.TryEnter())
        {
            try
            {
                // We don't want to change the combobox when it's changed from profile integer scaling
                Profile? currentProfile = ManagerFactory.profileManager.GetCurrent();
                if (currentProfile is not null && currentProfile.IntegerScalingEnabled)
                {
                    ProfileManager_Applied(currentProfile, UpdateSource.Background);
                    return;
                }

                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    if (resolution != ComboBoxResolution.SelectedItem)
                    {
                        // update target resolution
                        ComboBoxResolution.SelectedItem = resolution;

                        // update frequency list
                        ComboBoxFrequency.Items.Clear();
                        foreach (int frequency in resolution.Frequencies.Keys)
                        {
                            ComboBoxItem comboBoxItem = new()
                            {
                                Content = $"{frequency} Hz",
                                Tag = frequency,
                            };

                            ComboBoxFrequency.Items.Add(comboBoxItem);
                        }
                    }

                    // pick current frequency
                    int screenFrequency = desktopScreen.GetCurrentFrequency();
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
            catch { }
            finally
            {
                multimediaLock.Exit();
            }
        }
    }

    private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxResolution.SelectedItem is null)
            return;

        // prevent update loop
        if (multimediaLock.IsEntered())
            return;

        SetResolution();
    }

    private void ComboBoxFrequency_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxFrequency.SelectedItem is null)
            return;

        // prevent update loop
        if (multimediaLock.IsEntered())
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
        DesktopScreen desktopScreen = ManagerFactory.multimediaManager.PrimaryDesktop;

        if (desktopScreen.devMode.dmPelsWidth == resolution.Width &&
            desktopScreen.devMode.dmPelsHeight == resolution.Height &&
            desktopScreen.devMode.dmDisplayFrequency == frequency &&
            desktopScreen.devMode.dmBitsPerPel == resolution.BitsPerPel)
            return;

        ManagerFactory.multimediaManager.SetResolution(resolution.Width, resolution.Height, frequency, resolution.BitsPerPel);
    }

    public void SetResolution(ScreenResolution resolution)
    {
        // update current screen resolution
        ManagerFactory.multimediaManager.SetResolution(resolution.Width, resolution.Height, ManagerFactory.multimediaManager.PrimaryDesktop.GetCurrentFrequency(), resolution.BitsPerPel);
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

        ManagerFactory.settingsManager.SetProperty("LEDSettingsEnabled", UseDynamicLightingToggle.IsOn);
    }

    private void Toggle_FanOverride_Toggled(object sender, RoutedEventArgs e)
    {
        ToggleSwitch toggleSwitch = (ToggleSwitch)sender;
        if (IDevice.GetCurrent() is LegionGo device)
            device.SetFanFullSpeed(toggleSwitch.IsOn);
        else if (IDevice.GetCurrent() is ClawA2VM claw8)
            claw8.SetFanFullSpeed(toggleSwitch.IsOn);
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

            ManagerFactory.settingsManager.SetProperty("AYANEOFlipScreenEnabled", enabled);
        }
    }

    private void Slider_AYANEOFlipScreenBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = Slider_AYANEOFlipScreenBrightness.Value;
        if (double.IsNaN(value))
            return;

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("AYANEOFlipScreenBrightness", value);
    }
}