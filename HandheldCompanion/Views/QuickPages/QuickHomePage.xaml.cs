using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickHomePage.xaml
/// </summary>
public partial class QuickHomePage : Page
{
    private LockObject brightnessLock = new();
    private LockObject volumeLock = new();

    public QuickHomePage(string Tag) : this()
    {
        this.Tag = Tag;

        HotkeysManager.HotkeyCreated += HotkeysManager_HotkeyCreated;
        HotkeysManager.HotkeyUpdated += HotkeysManager_HotkeyUpdated;

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
            t_CurrentDeviceName.Text = GPU.adapterInformation.Details.Description;
        });
    }

    public QuickHomePage()
    {
        InitializeComponent();
    }

    private void HotkeysManager_HotkeyUpdated(Hotkey hotkey)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() => { UpdatePins(); });
    }

    private void HotkeysManager_HotkeyCreated(Hotkey hotkey)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() => { UpdatePins(); });
    }

    private void UpdatePins()
    {
        // todo, implement quick hotkey order
        QuickHotkeys.Children.Clear();

        foreach (var hotkey in HotkeysManager.Hotkeys.Values.Where(item => item.IsPinned))
            QuickHotkeys.Children.Add(hotkey.GetPin());
    }

    private void QuickButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        MainWindow.overlayquickTools.NavView_Navigate(button.Name);
    }

    private void SystemManager_Initialized()
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (MultimediaManager.HasBrightnessSupport())
            {
                SliderBrightness.IsEnabled = true;
                SliderBrightness.Value = MultimediaManager.GetBrightness();
            }

            if (MultimediaManager.HasVolumeSupport())
            {
                SliderVolume.IsEnabled = true;
                SliderVolume.Value = MultimediaManager.GetVolume();
                UpdateVolumeIcon((float)SliderVolume.Value);
            }
        });
    }

    private void SystemManager_BrightnessNotification(int brightness)
    {
        using (new ScopedLock(brightnessLock))
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                SliderBrightness.Value = brightness;
            });
        }
    }

    private void SystemManager_VolumeNotification(float volume)
    {
        using (new ScopedLock(volumeLock))
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateVolumeIcon(volume);
                SliderVolume.Value = Math.Round(volume);
            });
        }
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // wait until lock is released
        if (brightnessLock)
            return;

       MultimediaManager.SetBrightness(SliderBrightness.Value);
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        // wait until lock is released
        if (volumeLock)
            return;

        MultimediaManager.SetVolume(SliderVolume.Value);
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            t_CurrentProfile.Text = profile.ToString();
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
                    t_CurrentOverlayLevel.Text = onScreenDisplayLevels[overlayLevel];
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
