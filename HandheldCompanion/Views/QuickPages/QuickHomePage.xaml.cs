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

public partial class QuickHomePage : Page
{
    private readonly CrossThreadLock brightnessLock = new();
    private readonly CrossThreadLock volumeLock = new();

    public QuickHomePage()
    {
        DataContext = new QuickHomePageViewModel();
        InitializeComponent();
    }

    public QuickHomePage(string Tag) : this()
    {
        this.Tag = Tag;

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
        ManagerFactory.multimediaManager.VolumeNotification -= MultimediaManager_VolumeNotification;
        ManagerFactory.multimediaManager.BrightnessNotification -= MultimediaManager_BrightnessNotification;

        if (ManagerFactory.multimediaManager.HasBrightnessSupport())
        {
            short brightnessValue = 0;
            brightnessLock.Enter();
            try
            {
                brightnessValue = ManagerFactory.multimediaManager.GetBrightness();
            }
            finally
            {
                brightnessLock.Exit();
            }

            UIHelper.TryBeginInvoke(() =>
            {
                SliderBrightness.IsEnabled = true;
                SliderBrightness.Value = brightnessValue;
            });
        }

        if (ManagerFactory.multimediaManager.HasVolumeSupport())
        {
            double vol = 0;
            double rounded = 0;
            volumeLock.Enter();
            try
            {
                vol = ManagerFactory.multimediaManager.GetVolume();
                rounded = Math.Round(vol);
            }
            finally
            {
                volumeLock.Exit();
            }

            UIHelper.TryBeginInvoke(() =>
            {
                SliderVolume.IsEnabled = true;
                UpdateVolumeIcon(rounded);
                SliderVolume.Value = rounded;
            });
        }
    }

    private void MultimediaManager_Initialized()
    {
        QueryMedia();
    }

    private void Page_Loaded(object s, RoutedEventArgs e)
    {
        // do something
    }

    private void Page_Unloaded(object s, RoutedEventArgs e)
    {
        // do something
    }

    private void QuickButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        OverlayQuickTools.GetCurrent().NavigateToPage(button.Name);
    }

    private void MultimediaManager_BrightnessNotification(int brightness)
    {
        UIHelper.TryBeginInvoke(() =>
        {
            if (Math.Abs(SliderBrightness.Value - brightness) < double.Epsilon)
                return;

            brightnessLock.Enter();
            try
            {
                SliderBrightness.Value = brightness;
            }
            finally
            {
                brightnessLock.Exit();
            }
        });
    }

    private void MultimediaManager_VolumeNotification(float volume)
    {
        var rounded = Math.Round(Convert.ToDouble(volume));

        UIHelper.TryBeginInvoke(() =>
        {
            UpdateVolumeIcon(rounded);

            if (Math.Abs(SliderVolume.Value - rounded) < double.Epsilon)
                return;

            volumeLock.Enter();
            try
            {
                SliderVolume.Value = rounded;
            }
            finally
            {
                volumeLock.Exit();
            }
        });
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // If we're setting the value from a notification/init, don't feedback into SetBrightness
        if (brightnessLock.IsEntered())
            return;

        try
        {
            ManagerFactory.multimediaManager.SetBrightness(SliderBrightness.Value);
        }
        catch { }
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        if (volumeLock.IsEntered())
            return;

        try
        {
            ManagerFactory.multimediaManager.SetVolume(SliderVolume.Value);
        }
        catch { }
    }

    private void UpdateVolumeIcon(double volume)
    {
        string glyph;

        if (volume == 0) glyph = "\uE992";
        else if (volume <= 33) glyph = "\uE993";
        else if (volume <= 65) glyph = "\uE994";
        else glyph = "\uE995";

        VolumeIcon.Glyph = glyph;
    }
}