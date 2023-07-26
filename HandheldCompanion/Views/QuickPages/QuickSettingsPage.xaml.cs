using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HandheldCompanion.Managers;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickSettingsPage.xaml
/// </summary>
public partial class QuickSettingsPage : Page
{
    private readonly object brightnessLock = new();
    private readonly object volumeLock = new();

    public QuickSettingsPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickSettingsPage()
    {
        InitializeComponent();

        HotkeysManager.HotkeyCreated += HotkeysManager_HotkeyCreated;
        HotkeysManager.HotkeyUpdated += HotkeysManager_HotkeyUpdated;

        SystemManager.VolumeNotification += SystemManager_VolumeNotification;
        SystemManager.BrightnessNotification += SystemManager_BrightnessNotification;
        SystemManager.Initialized += SystemManager_Initialized;
    }

    private void SystemManager_Initialized()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (SystemManager.HasBrightnessSupport())
            {
                SliderBrightness.IsEnabled = true;
                SliderBrightness.Value = SystemManager.GetBrightness();
            }

            if (SystemManager.HasVolumeSupport())
            {
                SliderVolume.IsEnabled = true;
                SliderVolume.Value = SystemManager.GetVolume();
            }
        });
    }

    private void HotkeysManager_HotkeyUpdated(Hotkey hotkey)
    {
        UpdatePins();
    }

    private void HotkeysManager_HotkeyCreated(Hotkey hotkey)
    {
        UpdatePins();
    }

    private void UpdatePins()
    {
        // todo, implement quick hotkey order
        QuickHotkeys.Children.Clear();

        foreach (var hotkey in HotkeysManager.Hotkeys.Values.Where(item => item.IsPinned))
            QuickHotkeys.Children.Add(hotkey.GetPin());
    }

    private void SystemManager_BrightnessNotification(int brightness)
    {
        if (Monitor.TryEnter(brightnessLock))
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() => { SliderBrightness.Value = brightness; });

            Monitor.Exit(brightnessLock);
        }
    }

    private void SystemManager_VolumeNotification(float volume)
    {
        if (Monitor.TryEnter(volumeLock))
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // todo: update volume icon on update
                SliderVolume.Value = Math.Round(volume);
            });

            Monitor.Exit(volumeLock);
        }
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        if (Monitor.TryEnter(brightnessLock))
        {
            SystemManager.SetBrightness(SliderBrightness.Value);

            Monitor.Exit(brightnessLock);
        }
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        if (Monitor.TryEnter(volumeLock))
        {
            // update volume
            SystemManager.SetVolume(SliderVolume.Value);

            Monitor.Exit(volumeLock);
        }
    }
}