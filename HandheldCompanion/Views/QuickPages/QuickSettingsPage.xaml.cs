using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using NAudio.CoreAudioApi;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickSettingsPage.xaml
    /// </summary>
    public partial class QuickSettingsPage : Page
    {
        private object volumeLock = new();
        private object brightnessLock = new();

        public QuickSettingsPage()
        {
            InitializeComponent();

            HotkeysManager.HotkeyCreated += HotkeysManager_HotkeyCreated;
            HotkeysManager.HotkeyUpdated += HotkeysManager_HotkeyUpdated;

            DesktopManager.VolumeNotification += DeviceManager_VolumeNotification;
            DesktopManager.BrightnessNotification += DesktopManager_BrightnessNotification;
            DesktopManager.Initialized += DesktopManager_Initialized;
        }

        private void DesktopManager_Initialized()
        {
            // get current system brightness
            switch (DesktopManager.HasBrightnessSupport())
            {
                case true:
                    SliderBrightness.IsEnabled = true;
                    SliderBrightness.Value = DesktopManager.GetBrightness();
                    break;
            }

            // get current system volume
            switch (DesktopManager.HasVolumeSupport())
            {
                case true:
                    SliderVolume.IsEnabled = true;
                    SliderVolume.Value = DesktopManager.GetVolume();
                    break;
            }
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

            foreach (Hotkey hotkey in HotkeysManager.Hotkeys.Values.Where(item => item.IsPinned))
                QuickHotkeys.Children.Add(hotkey.GetPin());
        }

        private void DesktopManager_BrightnessNotification(int brightness)
        {
            if (Monitor.TryEnter(brightnessLock))
            {
                // UI thread
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    SliderBrightness.Value = brightness;
                });

                Monitor.Exit(brightnessLock);
            }
        }

        private void DeviceManager_VolumeNotification(float volume)
        {
            if (Monitor.TryEnter(volumeLock))
            {
                // UI thread
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    // todo: update volume icon on update
                    SliderVolume.Value = volume;
                });

                Monitor.Exit(volumeLock);
            }
        }

        private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Monitor.TryEnter(brightnessLock))
            {
                DesktopManager.SetBrightness(SliderBrightness.Value);

                Monitor.Exit(brightnessLock);
            }
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Monitor.TryEnter(volumeLock))
            {
                // update volume
                DesktopManager.SetVolume(SliderVolume.Value);

                Monitor.Exit(volumeLock);
            }
        }
    }
}
