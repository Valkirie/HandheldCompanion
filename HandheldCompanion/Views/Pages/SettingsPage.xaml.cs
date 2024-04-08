using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using iNKORE.UI.WPF.Modern.Helpers.Styles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using static HandheldCompanion.Managers.UpdateManager;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        // initialize components
        cB_Language.Items.Add(new CultureInfo("en-US"));
        cB_Language.Items.Add(new CultureInfo("fr-FR"));
        cB_Language.Items.Add(new CultureInfo("de-DE"));
        cB_Language.Items.Add(new CultureInfo("it-IT"));
        cB_Language.Items.Add(new CultureInfo("ja-JP"));
        cB_Language.Items.Add(new CultureInfo("pt-BR"));
        cB_Language.Items.Add(new CultureInfo("es-ES"));
        cB_Language.Items.Add(new CultureInfo("zh-CN"));
        cB_Language.Items.Add(new CultureInfo("zh-Hant"));
        cB_Language.Items.Add(new CultureInfo("ru-RU"));

        // initialize manager(s)
        UpdateManager.Updated += UpdateManager_Updated;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        PlatformManager.RTSS.Updated += RTSS_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    public SettingsPage(string? Tag) : this()
    {
        this.Tag = Tag;
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    Toggle_RTSS.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    Toggle_RTSS.IsOn = false;
                    break;
            }
        });
    }

    private void SettingsManager_SettingValueChanged(string? name, object value)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "MainWindowTheme":
                    {
                        cB_Theme.SelectedIndex = Convert.ToInt32(value);

                        // bug: SelectionChanged not triggered when control isn't loaded
                        if (!IsLoaded)
                            cB_Theme_SelectionChanged(this, null);
                    }
                    break;
                case "MainWindowBackdrop":
                    {
                        cB_Backdrop.SelectedIndex = Convert.ToInt32(value);

                        // bug: SelectionChanged not triggered when control isn't loaded
                        if (!IsLoaded)
                            cB_Backdrop_SelectionChanged(this, null);
                    }
                    break;
                case "QuicktoolsBackdrop":
                    {
                        cB_QuickToolsBackdrop.SelectedIndex = Convert.ToInt32(value);

                        // bug: SelectionChanged not triggered when control isn't loaded
                        if (!IsLoaded)
                            cB_QuickToolsBackdrop_SelectionChanged(this, null);
                    }
                    break;
                case "RunAtStartup":
                    Toggle_AutoStart.IsOn = Convert.ToBoolean(value);
                    break;
                case "StartMinimized":
                    Toggle_Background.IsOn = Convert.ToBoolean(value);
                    break;
                case "CloseMinimises":
                    Toggle_CloseMinimizes.IsOn = Convert.ToBoolean(value);
                    break;
                case "DesktopProfileOnStart":
                    Toggle_DesktopProfileOnStart.IsOn = Convert.ToBoolean(value);
                    break;
                case "NativeDisplayOrientation":
                    var nativeOrientation = (ScreenRotation.Rotations)Convert.ToInt32(value);

                    switch (nativeOrientation)
                    {
                        case ScreenRotation.Rotations.DEFAULT:
                            Text_NativeDisplayOrientation.Text = "Landscape";
                            break;
                        case ScreenRotation.Rotations.D90:
                            Text_NativeDisplayOrientation.Text = "Portrait";
                            break;
                        case ScreenRotation.Rotations.D180:
                            Text_NativeDisplayOrientation.Text = "Flipped Landscape";
                            break;
                        case ScreenRotation.Rotations.D270:
                            Text_NativeDisplayOrientation.Text = "Flipped Portrait";
                            break;
                        default:
                            Text_NativeDisplayOrientation.Text = "Not set";
                            break;
                    }

                    break;
                case "ToastEnable":
                    Toggle_Notification.IsOn = Convert.ToBoolean(value);
                    break;
                case "CurrentCulture":
                    cB_Language.SelectedItem = new CultureInfo((string)value);

                    // bug: SelectionChanged not triggered when control isn't loaded
                    if (!IsLoaded)
                        cB_Language_SelectionChanged(this, null);
                    break;
                case "PlatformRTSSEnabled":
                    Toggle_RTSS.IsOn = Convert.ToBoolean(value);
                    break;
                case "QuickToolsLocation":
                    cB_QuicktoolsPosition.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "QuickToolsAutoHide":
                    Toggle_QuicktoolsAutoHide.IsOn = Convert.ToBoolean(value);
                    break;
                case "UISounds":
                    Toggle_UISounds.IsOn = Convert.ToBoolean(value);
                    break;
            }
        });
    }

    private void Page_Loaded(object? sender, RoutedEventArgs? e)
    {
        UpdateManager.Start();
    }

    public void Page_Closed()
    {
    }

    private async void Toggle_AutoStart_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("RunAtStartup", Toggle_AutoStart.IsOn);
    }

    private void Toggle_Background_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("StartMinimized", Toggle_Background.IsOn);
    }

    private void Toggle_CloseMinimizes_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("CloseMinimises", Toggle_CloseMinimizes.IsOn);
    }

    private void Toggle_DesktopProfileOnStart_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("DesktopProfileOnStart", Toggle_DesktopProfileOnStart.IsOn);
    }

    private void Button_DetectNativeDisplayOrientation_Click(object sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        var rotation = MultimediaManager.GetScreenOrientation();
        rotation = new ScreenRotation(rotation.rotationUnnormalized, ScreenRotation.Rotations.UNSET);
        SettingsManager.SetProperty("NativeDisplayOrientation", (int)rotation.rotationNativeBase);
    }

    private void UpdateManager_Updated(UpdateStatus status, UpdateFile updateFile, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case UpdateStatus.Failed: // lazy ?
                case UpdateStatus.Updated:
                case UpdateStatus.Initialized:
                    {
                        if (updateFile is not null)
                        {
                            updateFile.updateDownload.Visibility = Visibility.Visible;

                            updateFile.updatePercentage.Visibility = Visibility.Collapsed;
                            updateFile.updateInstall.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            LabelUpdate.Text = Properties.Resources.SettingsPage_UpToDate;
                            LabelUpdateDate.Text = Properties.Resources.SettingsPage_LastChecked +
                                                   UpdateManager.GetTime();

                            LabelUpdateDate.Visibility = Visibility.Visible;
                            GridUpdateSymbol.Visibility = Visibility.Visible;
                            ProgressBarUpdate.Visibility = Visibility.Collapsed;
                            B_CheckUpdate.IsEnabled = true;
                        }
                    }
                    break;

                case UpdateStatus.Checking:
                    {
                        LabelUpdate.Text = Properties.Resources.SettingsPage_UpdateCheck;

                        GridUpdateSymbol.Visibility = Visibility.Collapsed;
                        LabelUpdateDate.Visibility = Visibility.Collapsed;
                        ProgressBarUpdate.Visibility = Visibility.Visible;
                        B_CheckUpdate.IsEnabled = false;
                    }
                    break;

                case UpdateStatus.Ready:
                    {
                        ProgressBarUpdate.Visibility = Visibility.Collapsed;

                        var updateFiles = (Dictionary<string, UpdateFile>)value;
                        LabelUpdate.Text = Properties.Resources.SettingsPage_UpdateAvailable;

                        foreach (var update in updateFiles.Values)
                        {
                            var border = update.Draw();

                            // Set download button action
                            update.updateDownload.Click += (sender, e) =>
                            {
                                UpdateManager.DownloadUpdateFile(update);
                            };

                            // Set button action
                            update.updateInstall.Click += (sender, e) =>
                            {
                                UpdateManager.InstallUpdate(update);
                            };

                            CurrentUpdates.Children.Add(border);
                        }
                    }
                    break;

                case UpdateStatus.Changelog:
                    {
                        CurrentChangelog.Visibility = Visibility.Visible;
                        CurrentChangelog.AppendText((string)value);
                    }
                    break;

                case UpdateStatus.Download:
                    {
                        updateFile.updateDownload.Visibility = Visibility.Collapsed;
                        updateFile.updatePercentage.Visibility = Visibility.Visible;
                    }
                    break;

                case UpdateStatus.Downloading:
                    {
                        var progress = (int)value;
                        updateFile.updatePercentage.Text =
                            Properties.Resources.SettingsPage_DownloadingPercentage + $"{value} %";
                    }
                    break;

                case UpdateStatus.Downloaded:
                    {
                        updateFile.updateInstall.Visibility = Visibility.Visible;

                        updateFile.updateDownload.Visibility = Visibility.Collapsed;
                        updateFile.updatePercentage.Visibility = Visibility.Collapsed;
                    }
                    break;
            }
        });
    }

    private void B_CheckUpdate_Click(object? sender, RoutedEventArgs? e)
    {
        new Thread(() => { UpdateManager.StartProcess(); }).Start();
    }

    private void cB_Language_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        var culture = (CultureInfo)cB_Language.SelectedItem;

        if (culture is null)
            return;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("CurrentCulture", culture.Name);

        // prevent message from being displayed again...
        if (culture.Name == CultureInfo.CurrentCulture.Name)
            return;

        _ = new Dialog(MainWindow.GetCurrent())
        {
            Title = Properties.Resources.SettingsPage_AppLanguageWarning,
            Content = Properties.Resources.SettingsPage_AppLanguageWarningDesc,
            PrimaryButtonText = Properties.Resources.ProfilesPage_OK
        }.ShowAsync();
    }

    private void Toggle_Notification_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("ToastEnable", Toggle_Notification.IsOn);
    }

    private void cB_Theme_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_Theme.SelectedIndex == -1)
            return;

        ElementTheme theme = (ElementTheme)cB_Theme.SelectedIndex;
        MainWindow mainWindow = MainWindow.GetCurrent();
        ThemeManager.SetRequestedTheme(mainWindow, theme);
        ThemeManager.SetRequestedTheme(MainWindow.overlayquickTools, theme);

        // update default style
        MainWindow.GetCurrent().UpdateDefaultStyle();
        MainWindow.overlayquickTools.UpdateDefaultStyle();

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("MainWindowTheme", cB_Theme.SelectedIndex);
    }

    private void cB_QuickToolsBackdrop_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_QuickToolsBackdrop.SelectedIndex == -1)
            return;

        var targetWindow = MainWindow.overlayquickTools;
        SwitchBackdrop(targetWindow, cB_QuickToolsBackdrop.SelectedIndex);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("QuicktoolsBackdrop", cB_QuickToolsBackdrop.SelectedIndex);
    }

    private void cB_Backdrop_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_Backdrop.SelectedIndex == -1)
            return;

        var targetWindow = MainWindow.GetCurrent();
        SwitchBackdrop(targetWindow, cB_Backdrop.SelectedIndex);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("MainWindowBackdrop", cB_Backdrop.SelectedIndex);
    }

    private void SwitchBackdrop(Window targetWindow, int idx)
    {
        targetWindow.ApplyTemplate();
        targetWindow.UpdateLayout();

        try
        {
            switch (idx)
            {
                case 0: // "None":
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.None);
                    WindowHelper.SetUseAcrylicBackdrop(targetWindow, false);
                    WindowHelper.SetUseAeroBackdrop(targetWindow, false);
                    break;
                case 1: // "Mica":
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Mica);
                    WindowHelper.SetUseAcrylicBackdrop(targetWindow, false);
                    WindowHelper.SetUseAeroBackdrop(targetWindow, false);
                    break;
                case 2: // "Tabbed":
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Tabbed);
                    WindowHelper.SetUseAcrylicBackdrop(targetWindow, false);
                    WindowHelper.SetUseAeroBackdrop(targetWindow, false);
                    break;
                case 3: // "Acrylic":
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Acrylic);
                    WindowHelper.SetUseAcrylicBackdrop(targetWindow, true);
                    WindowHelper.SetUseAeroBackdrop(MainWindow.GetCurrent(), true);
                    break;
            }
        }
        catch
        {
        }
    }

    private void Toggle_RTSS_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("PlatformRTSSEnabled", Toggle_RTSS.IsOn);
    }

    private void cB_QuicktoolsPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("QuickToolsLocation", cB_QuicktoolsPosition.SelectedIndex);
    }

    private void Toggle_QuicktoolsAutoHide_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("QuickToolsAutoHide", Toggle_QuicktoolsAutoHide.IsOn);
    }

    private void Toggle_UISounds_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("UISounds", Toggle_UISounds.IsOn);
    }
}