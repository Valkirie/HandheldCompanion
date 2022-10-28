using CoreAudio;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Classes;
using HandheldCompanion.Views.Windows;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickSettingsPage.xaml
    /// </summary>
    public partial class QuickSettingsPage : Page
    {
        private MMDeviceEnumerator DevEnum;
        private MMDevice multimediaDevice;

        private bool IgnoreMe;      // used to avoid infinite loop

        public QuickSettingsPage()
        {
            InitializeComponent();

            OverlayQuickTools.brightnessControl.BrightnessChanged += BrightnessControl_BrightnessChanged;
            HotkeysManager.HotkeyCreated += HotkeysManager_HotkeyCreated;
            HotkeysManager.HotkeyUpdated += HotkeysManager_HotkeyUpdated;

            // get current system brightness
            switch(OverlayQuickTools.brightnessControl.IsSupported)
            {
                case true:
                    SliderBrightness.IsEnabled = true;
                    SliderBrightness.Value = OverlayQuickTools.brightnessControl.GetBrightness();
                    break;
            }

            // get current system volume
            DevEnum = new MMDeviceEnumerator();
            multimediaDevice = DevEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);

            if (multimediaDevice != null && multimediaDevice.AudioEndpointVolume != null)
            {
                multimediaDevice.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
                SliderVolume.Value = multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0d;
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
            QuickHotkeys.Children.Clear();

            foreach (Hotkey hotkey in HotkeysManager.Hotkeys.Values.Where(item => item.IsPinned))
                QuickHotkeys.Children.Add(hotkey.GetPin());
        }

        private void BrightnessControl_BrightnessChanged(int brightness)
        {
            this.Dispatcher.Invoke(() =>
            {
                IgnoreMe = true;
                SliderBrightness.Value = brightness;
                IgnoreMe = false;
            });
        }

        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            this.Dispatcher.Invoke(() =>
            {
                IgnoreMe = true;
                SliderVolume.Value = Convert.ToDouble(data.MasterVolume * 100.0f);
                IgnoreMe = false;
            });
        }

        private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;

            // todo: change glyph based on volume percentage

            if (IgnoreMe)
                return;

            OverlayQuickTools.brightnessControl.SetBrightness(SliderBrightness.Value);
            //SetMonitorBrightness(mainMonitor, SliderBrightness.Value / 100);
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (multimediaDevice == null || multimediaDevice.AudioEndpointVolume == null)
                return;

            if (!IsLoaded)
                return;

            if (IgnoreMe)
                return;

            multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(SliderVolume.Value / 100.0d);
        }
    }
}
