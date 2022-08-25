using CoreAudio;
using HandheldCompanion.Views.Windows;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickSettingsPage.xaml
    /// </summary>
    public partial class QuickSettingsPage : Page
    {
        private MMDeviceEnumerator DevEnum;
        private MMDevice multimediaDevice;

        private bool Initialized;
        private bool IgnoreMe;      // used to avoid infinite loop

        public QuickSettingsPage()
        {
            InitializeComponent();
            Initialized = true;

            QuickTools.brightnessControl.BrightnessChanged += BrightnessControl_BrightnessChanged;

            // get current system brightness
            SliderBrightness.Value = QuickTools.brightnessControl.GetBrightness();

            // get current system volume
            DevEnum = new MMDeviceEnumerator();
            multimediaDevice = DevEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);

            if (multimediaDevice != null && multimediaDevice.AudioEndpointVolume != null)
            {
                multimediaDevice.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
                SliderVolume.Value = multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0d;
            }
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
            if (!Initialized)
                return;

            // todo: change glyph based on volume percentage

            if (IgnoreMe)
                return;

            QuickTools.brightnessControl.SetBrightness(SliderBrightness.Value);
            //SetMonitorBrightness(mainMonitor, SliderBrightness.Value / 100);
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (multimediaDevice == null || multimediaDevice.AudioEndpointVolume == null)
                return;

            if (!Initialized)
                return;

            if (IgnoreMe)
                return;

            multimediaDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(SliderVolume.Value / 100.0d);
        }
    }
}
