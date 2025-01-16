using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
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

        // manage events
        ManagerFactory.multimediaManager.VolumeNotification += SystemManager_VolumeNotification;
        ManagerFactory.multimediaManager.BrightnessNotification += SystemManager_BrightnessNotification;
        ManagerFactory.multimediaManager.Initialized += SystemManager_Initialized;
    }

    public void Close()
    {
        // manage events
        ManagerFactory.multimediaManager.VolumeNotification -= SystemManager_VolumeNotification;
        ManagerFactory.multimediaManager.BrightnessNotification -= SystemManager_BrightnessNotification;
        ManagerFactory.multimediaManager.Initialized -= SystemManager_Initialized;
    }

    public QuickHomePage()
    {
        DataContext = new QuickHomePageViewModel();
        InitializeComponent();
    }

    private void QuickButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        OverlayQuickTools.GetCurrent().NavigateToPage(button.Name);
    }

    private void SystemManager_Initialized()
    {
        if (ManagerFactory.multimediaManager.HasBrightnessSupport())
        {
            lock (brightnessLock)
            {
                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    SliderBrightness.IsEnabled = true;
                    SliderBrightness.Value = ManagerFactory.multimediaManager.GetBrightness();
                });
            }
        }

        if (ManagerFactory.multimediaManager.HasVolumeSupport())
        {
            lock (volumeLock)
            {
                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    SliderVolume.IsEnabled = true;
                    SliderVolume.Value = ManagerFactory.multimediaManager.GetVolume();
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
                UIHelper.TryInvoke(() =>
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
                int value = Convert.ToInt32(volume);

                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    UpdateVolumeIcon(value);

                    if (SliderVolume.Value != value)
                        SliderVolume.Value = value;
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
            ManagerFactory.multimediaManager.SetBrightness(SliderBrightness.Value);
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // prevent update loop
        if (volumeLock.IsEntered())
            return;

        lock (volumeLock)
            ManagerFactory.multimediaManager.SetVolume(SliderVolume.Value);
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
