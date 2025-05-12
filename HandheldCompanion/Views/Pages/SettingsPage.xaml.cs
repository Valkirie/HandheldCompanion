using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Platforms;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Helpers.Styles;
using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using static HandheldCompanion.Managers.UpdateManager;
using static HandheldCompanion.WinAPI;
using Page = System.Windows.Controls.Page;
using WindowHelper = iNKORE.UI.WPF.Modern.Controls.Helpers.WindowHelper;

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
        cB_Language.Items.Add(new CultureInfo("ko-KR"));
        cB_Language.Items.Add(new CultureInfo("pt-BR"));
        cB_Language.Items.Add(new CultureInfo("es-ES"));
        cB_Language.Items.Add(new CultureInfo("zh-Hans"));
        cB_Language.Items.Add(new CultureInfo("zh-Hant"));
        cB_Language.Items.Add(new CultureInfo("ru-RU"));

        // manage events
        UpdateManager.Updated += UpdateManager_Updated;
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ManagerFactory.multimediaManager.ScreenConnected += MultimediaManager_ScreenConnected;
        ManagerFactory.multimediaManager.ScreenDisconnected += MultimediaManager_ScreenDisconnected;
        ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;

        PlatformManager.RTSS.Updated += RTSS_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    private void MultimediaManager_ScreenConnected(DesktopScreen screen)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            int idx = -1;
            foreach (DesktopScreen desktopScreen in cB_QuickToolsDevicePath.Items.OfType<DesktopScreen>())
            {
                if (desktopScreen.DevicePath.Equals(screen.DevicePath))
                    idx = cB_QuickToolsDevicePath.Items.IndexOf(desktopScreen);
            }

            if (idx != -1)
                cB_QuickToolsDevicePath.Items[idx] = screen;
            else
                cB_QuickToolsDevicePath.Items.Add(screen);
        });
    }

    private void MultimediaManager_ScreenDisconnected(DesktopScreen screen)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // check if current target was disconnected
            if (cB_QuickToolsDevicePath.SelectedItem is DesktopScreen targetScreen)
                if (targetScreen.DevicePath.Equals(screen.DevicePath))
                    cB_QuickToolsDevicePath.SelectedIndex = 0;

            int idx = -1;
            foreach (DesktopScreen desktopScreen in cB_QuickToolsDevicePath.Items.OfType<DesktopScreen>())
            {
                if (desktopScreen.DevicePath.Equals(screen.DevicePath))
                    idx = cB_QuickToolsDevicePath.Items.IndexOf(desktopScreen);
            }

            if (idx != -1)
                cB_QuickToolsDevicePath.Items.RemoveAt(idx);
        });
    }

    private void MultimediaManager_Initialized()
    {
        string DevicePath = ManagerFactory.settingsManager.GetString("QuickToolsDevicePath");
        string DeviceName = ManagerFactory.settingsManager.GetString("QuickToolsDeviceName");

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            DesktopScreen? selectedScreen = cB_QuickToolsDevicePath.Items.OfType<DesktopScreen>()
                .FirstOrDefault(screen => screen.DevicePath.Equals(DevicePath) || screen.ToString().Equals(DeviceName));

            if (selectedScreen != null)
                cB_QuickToolsDevicePath.SelectedItem = selectedScreen;
            else
                cB_QuickToolsDevicePath.SelectedIndex = 0;
        });
    }

    public SettingsPage(string? Tag) : this()
    {
        this.Tag = Tag;
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
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

    private void SettingsManager_SettingValueChanged(string? name, object value, bool temporary)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            switch (name)
            {
                case "MainWindowTheme":
                    cB_Theme.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "MainWindowBackdrop":
                    cB_Backdrop.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "QuicktoolsBackdrop":
                    cB_QuickToolsBackdrop.SelectedIndex = Convert.ToInt32(value);
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
                case "DesktopLayoutOnStart":
                    Toggle_DesktopLayoutOnStart.IsOn = Convert.ToBoolean(value);
                    break;
                case "ToastEnable":
                    Toggle_Notification.IsOn = Convert.ToBoolean(value);
                    break;
                case "CurrentCulture":
                    cB_Language.SelectedItem = new CultureInfo((string)value);
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
                    Toggle_UISounds.IsEnabled = ManagerFactory.multimediaManager.HasVolumeSupport();
                    Toggle_UISounds.IsOn = Convert.ToBoolean(value);
                    break;
                case "TelemetryEnabled":
                    {
                        // send device details to sentry
                        bool IsSentryEnabled = Convert.ToBoolean(value);
                        Toggle_Telemetry.IsOn = IsSentryEnabled;

                        // ignore if initializing
                        if (ManagerFactory.settingsManager.Status.HasFlag(ManagerStatus.Initializing))
                            return;

                        if (SentrySdk.IsEnabled && IsSentryEnabled)
                            SentrySdk.CaptureMessage("Telemetry enabled on the device");
                    }
                    break;
                case "ProcessPriority":
                    {
                        cB_Priority.SelectedIndex = Convert.ToInt32(value);
                    }
                    break;
            }
        });
    }

    private void Page_Loaded(object? sender, RoutedEventArgs? e)
    {
    }

    public void Page_Closed()
    {
        // manage events
        UpdateManager.Updated -= UpdateManager_Updated;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.multimediaManager.ScreenConnected -= MultimediaManager_ScreenConnected;
        ManagerFactory.multimediaManager.ScreenDisconnected -= MultimediaManager_ScreenDisconnected;
        ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
    }

    private async void Toggle_AutoStart_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("RunAtStartup", Toggle_AutoStart.IsOn);
    }

    private void Toggle_Background_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("StartMinimized", Toggle_Background.IsOn);
    }

    private void Toggle_CloseMinimizes_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("CloseMinimises", Toggle_CloseMinimizes.IsOn);
    }

    private void Toggle_DesktopLayoutOnStart_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("DesktopLayoutOnStart", Toggle_DesktopLayoutOnStart.IsOn);
    }

    private void UpdateManager_Updated(UpdateStatus status, UpdateFile updateFile, object value)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
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
        new Thread(() =>
        {
            UpdateManager.StartProcess();
        }).Start();
    }

    private void cB_Language_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        var culture = (CultureInfo)cB_Language.SelectedItem;

        if (culture is null)
            return;

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("CurrentCulture", culture.Name);

        Localization.TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo(culture.Name);

        NavigationService?.Refresh();
    }

    private void Toggle_Notification_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("ToastEnable", Toggle_Notification.IsOn);
    }

    private void cB_Theme_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_Theme.SelectedIndex == -1)
            return;

        ElementTheme elementTheme = (ElementTheme)cB_Theme.SelectedIndex;

        // update default style
        ThemeManager.SetRequestedTheme(MainWindow.GetCurrent(), elementTheme);
        ThemeManager.SetRequestedTheme(OverlayQuickTools.GetCurrent(), elementTheme);

        switch (elementTheme)
        {
            case ElementTheme.Default:
                ThemeManager.Current.ApplicationTheme = null;
                break;
            case ElementTheme.Light:
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                break;
            case ElementTheme.Dark:
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                break;
        }

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("MainWindowTheme", cB_Theme.SelectedIndex);
    }

    private void cB_QuickToolsBackdrop_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_QuickToolsBackdrop.SelectedIndex == -1)
            return;

        var targetWindow = OverlayQuickTools.GetCurrent();
        SwitchBackdrop(targetWindow, cB_QuickToolsBackdrop.SelectedIndex);

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("QuicktoolsBackdrop", cB_QuickToolsBackdrop.SelectedIndex);
    }

    private void cB_Backdrop_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_Backdrop.SelectedIndex == -1)
            return;

        var targetWindow = MainWindow.GetCurrent();
        SwitchBackdrop(targetWindow, cB_Backdrop.SelectedIndex);

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("MainWindowBackdrop", cB_Backdrop.SelectedIndex);
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
                    break;
                case 1: // "Mica":
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Mica);
                    break;
                case 2: // "Tabbed":
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Tabbed);
                    break;
                case 3: // "Acrylic":
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Acrylic);
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

        ManagerFactory.settingsManager.SetProperty("PlatformRTSSEnabled", Toggle_RTSS.IsOn);
    }

    private void cB_QuicktoolsPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("QuickToolsLocation", cB_QuicktoolsPosition.SelectedIndex);
    }

    private void Toggle_QuicktoolsAutoHide_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("QuickToolsAutoHide", Toggle_QuicktoolsAutoHide.IsOn);
    }

    private void Toggle_UISounds_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("UISounds", Toggle_UISounds.IsOn);
    }

    private void Toggle_Telemetry_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("TelemetryEnabled", Toggle_Telemetry.IsOn);
    }

    private void cB_QuickToolsDevicePath_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (cB_QuickToolsDevicePath.SelectedItem is DesktopScreen desktopScreen)
        {
            ManagerFactory.settingsManager.SetProperty("QuickToolsDeviceName", desktopScreen.ToString());
            ManagerFactory.settingsManager.SetProperty("QuickToolsDevicePath", desktopScreen.DevicePath);
        }
    }

    private void cB_Priority_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        using (Process process = Process.GetCurrentProcess())
        {
            switch (cB_Priority.SelectedIndex)
            {
                case 0: // Normal
                    SetPriorityClass(process.Handle, (int)PriorityClass.NORMAL_PRIORITY_CLASS);
                    break;
                case 1: // Above normal
                    SetPriorityClass(process.Handle, (int)PriorityClass.ABOVE_NORMAL_PRIORITY_CLASS);
                    break;
                case 2: // High
                    SetPriorityClass(process.Handle, (int)PriorityClass.HIGH_PRIORITY_CLASS);
                    break;
            }
        }

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("ProcessPriority", cB_Priority.SelectedIndex);
    }
}
