using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickHomePage.xaml
/// </summary>
public partial class QuickHomePage : Page
{
    private CrossThreadLock brightnessLock = new();
    private CrossThreadLock volumeLock = new();

    public QuickHomePage(string Tag) : this()
    {
        this.Tag = Tag;

        MultimediaManager.VolumeNotification += SystemManager_VolumeNotification;
        MultimediaManager.BrightnessNotification += SystemManager_BrightnessNotification;
        MultimediaManager.Initialized += SystemManager_Initialized;

        ProfileManager.Applied += ProfileManager_Applied;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        GPUManager.Hooked += GPUManager_Hooked;
    }

    private void GPUManager_Hooked(GraphicsProcessingUnit.GPU GPU)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            // deprecated
            // t_CurrentDeviceName.Text = GPU.adapterInformation.Details.Description;
        });
    }

    public QuickHomePage()
    {
        DataContext = new QuickHomePageViewModel();
        InitializeComponent();
    }

    private void QuickButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        MainWindow.overlayquickTools.NavView_Navigate(button.Name);
    }

    private void SystemManager_Initialized()
    {
        if (MultimediaManager.HasBrightnessSupport())
        {
            lock (brightnessLock)
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SliderBrightness.IsEnabled = true;
                    SliderBrightness.Value = MultimediaManager.GetBrightness();
                });
            }
        }

        if (MultimediaManager.HasVolumeSupport())
        {
            lock (volumeLock)
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SliderVolume.IsEnabled = true;
                    SliderVolume.Value = MultimediaManager.GetVolume();
                    UpdateVolumeIcon((float)SliderVolume.Value);
                });
            }
        }
    }

    private void SystemManager_BrightnessNotification(int brightness)
    {
        if (brightnessLock.TryEnter())
        {
            try
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (SliderBrightness.Value != brightness)
                        SliderBrightness.Value = brightness;
                });
            }
            finally
            {
                brightnessLock.Exit();
            }
        }
    }

    private void SystemManager_VolumeNotification(float volume)
    {
        if (volumeLock.TryEnter())
        {
            try
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateVolumeIcon(volume);

                    if (SliderVolume.Value != (int)volume)
                        SliderVolume.Value = (int)volume;
                });
            }
            finally
            {
                volumeLock.Exit();
            }
        }
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // prevent update loop
        if (brightnessLock.IsEntered())
            return;

        lock (brightnessLock)
            MultimediaManager.SetBrightness(SliderBrightness.Value);
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // prevent update loop
        if (volumeLock.IsEntered())
            return;

        lock (volumeLock)
            MultimediaManager.SetVolume(SliderVolume.Value);
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            // deprecated
            // t_CurrentProfile.Text = profile.ToString();
        });
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        string[] onScreenDisplayLevels = {
            Properties.Resources.OverlayPage_OverlayDisplayLevel_Disabled,
            Properties.Resources.OverlayPage_OverlayDisplayLevel_Minimal,
            Properties.Resources.OverlayPage_OverlayDisplayLevel_Extended,
            Properties.Resources.OverlayPage_OverlayDisplayLevel_Full,
            Properties.Resources.OverlayPage_OverlayDisplayLevel_Custom,
            Properties.Resources.OverlayPage_OverlayDisplayLevel_External,
        };

        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "OnScreenDisplayLevel":
                    {
                        var overlayLevel = Convert.ToInt16(value);

                        // deprecated
                        // t_CurrentOverlayLevel.Text = onScreenDisplayLevels[overlayLevel];
                    }
                    break;
            }
        });
    }

    private void UpdateVolumeIcon(float volume)
    {
        string glyph;

        if (volume == 0)
        {
            glyph = "\uE992"; // Mute icon
        }
        else if (volume <= 33)
        {
            glyph = "\uE993"; // Low volume icon
        }
        else if (volume <= 65)
        {
            glyph = "\uE994"; // Medium volume icon
        }
        else
        {
            glyph = "\uE995"; // High volume icon (default)
        }

        VolumeIcon.Glyph = glyph;
    }
}
