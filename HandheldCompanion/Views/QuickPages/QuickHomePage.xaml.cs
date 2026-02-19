using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickHomePage.xaml
/// </summary>
public partial class QuickHomePage : Page
{
    // Guard against feedback loops between manager notifications and slider ValueChanged.
    private int _suppressBrightnessChanged;
    private int _suppressVolumeChanged;

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
            UIHelper.TryInvoke(() =>
            {
                SliderBrightness.IsEnabled = true;

                Interlocked.Exchange(ref _suppressBrightnessChanged, 1);
                try
                {
                    SliderBrightness.Value = ManagerFactory.multimediaManager.GetBrightness();
                }
                finally
                {
                    Interlocked.Exchange(ref _suppressBrightnessChanged, 0);
                }
            });
        }

        if (ManagerFactory.multimediaManager.HasVolumeSupport())
        {
            UIHelper.TryInvoke(() =>
            {
                SliderVolume.IsEnabled = true;

                Interlocked.Exchange(ref _suppressVolumeChanged, 1);
                try
                {
                    SliderVolume.Value = ManagerFactory.multimediaManager.GetVolume();
                    UpdateVolumeIcon((float)SliderVolume.Value);
                }
                finally
                {
                    Interlocked.Exchange(ref _suppressVolumeChanged, 0);
                }
            });
        }
    }

    private void SystemManager_BrightnessNotification(int brightness)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            Interlocked.Exchange(ref _suppressBrightnessChanged, 1);
            try
            {
                if (SliderBrightness.Value != brightness)
                    SliderBrightness.Value = brightness;
            }
            finally
            {
                Interlocked.Exchange(ref _suppressBrightnessChanged, 0);
            }
        });
    }

    private void SystemManager_VolumeNotification(float volume)
    {
        double value = Convert.ToDouble(volume);

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            Interlocked.Exchange(ref _suppressVolumeChanged, 1);
            try
            {
                UpdateVolumeIcon(value);

                if (SliderVolume.Value != value)
                    SliderVolume.Value = Math.Round(value);
            }
            finally
            {
                Interlocked.Exchange(ref _suppressVolumeChanged, 0);
            }
        });
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // Ignore programmatic updates.
        if (Interlocked.CompareExchange(ref _suppressBrightnessChanged, 0, 0) == 1)
            return;

        try { ManagerFactory.multimediaManager.SetBrightness(SliderBrightness.Value); }
        catch { }
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // Ignore programmatic updates.
        if (Interlocked.CompareExchange(ref _suppressVolumeChanged, 0, 0) == 1)
            return;

        try { ManagerFactory.multimediaManager.SetVolume(SliderVolume.Value); }
        catch { }
    }

    private void UpdateVolumeIcon(double volume)
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
