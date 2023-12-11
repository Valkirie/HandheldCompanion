﻿using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
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
    private LockObject brightnessLock = new();
    private LockObject volumeLock = new();

    public QuickHomePage(string Tag) : this()
    {
        this.Tag = Tag;

        SystemManager.VolumeNotification += SystemManager_VolumeNotification;
        SystemManager.BrightnessNotification += SystemManager_BrightnessNotification;
        SystemManager.Initialized += SystemManager_Initialized;
    }

    public QuickHomePage()
    {
        InitializeComponent();
    }

    private void QuickButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        MainWindow.overlayquickTools.NavView_Navigate(button.Name);
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

    private void SystemManager_BrightnessNotification(int brightness)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            // wait until lock is released
            if (brightnessLock)
                SliderBrightness.Value = brightness;
        });
    }

    private void SystemManager_VolumeNotification(float volume)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            // todo: update volume icon on update

            // wait until lock is released
            if (volumeLock)
                SliderVolume.Value = Math.Round(volume);
        });
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        using (new ScopedLock(brightnessLock))
            SystemManager.SetBrightness(SliderBrightness.Value);
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        using (new ScopedLock(volumeLock))
            SystemManager.SetVolume(SliderVolume.Value);
    }
}