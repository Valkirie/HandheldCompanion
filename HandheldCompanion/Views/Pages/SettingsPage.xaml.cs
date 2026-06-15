using HandheldCompanion.Helpers;
using HandheldCompanion.Localization;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Helpers.Styles;
using Sentry;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static HandheldCompanion.WinAPI;
using Page = System.Windows.Controls.Page;
using WindowHelper = iNKORE.UI.WPF.Modern.Controls.Helpers.WindowHelper;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage : Page
{
    private SettingsPageViewModel ViewModel;

    public SettingsPage()
    {
        ViewModel = new SettingsPageViewModel();
        DataContext = ViewModel;
        InitializeComponent();

        // Move culture loading to background thread
        CultureInfo[] cultures = TranslationSource.ValidCultures;
        cB_Language.ItemsSource = cultures;

        // manage events
        ManagerFactory.multimediaManager.ScreenConnected += MultimediaManager_ScreenConnected;
        ManagerFactory.multimediaManager.ScreenDisconnected += MultimediaManager_ScreenDisconnected;
        ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;

        // raise events
        switch (ManagerFactory.platformManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.platformManager.Initialized += PlatformManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPlatforms();
                break;
        }

        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("MainWindowTheme", ManagerFactory.settingsManager.GetString("MainWindowTheme"), false, false);
        SettingsManager_SettingValueChanged("MainWindowBackdrop", ManagerFactory.settingsManager.GetString("MainWindowBackdrop"), false, false);
        SettingsManager_SettingValueChanged("MainWindowApplyNoise", ManagerFactory.settingsManager.GetString("MainWindowApplyNoise"), false, false);
        SettingsManager_SettingValueChanged("QuicktoolsBackdrop", ManagerFactory.settingsManager.GetString("QuicktoolsBackdrop"), false, false);
        SettingsManager_SettingValueChanged("QuickToolsApplyNoise", ManagerFactory.settingsManager.GetString("QuickToolsApplyNoise"), false, false);
        SettingsManager_SettingValueChanged("RunAtStartup", ManagerFactory.settingsManager.GetString("RunAtStartup"), false, false);
        SettingsManager_SettingValueChanged("StartMinimized", ManagerFactory.settingsManager.GetString("StartMinimized"), false, false);
        SettingsManager_SettingValueChanged("StartMaximized", ManagerFactory.settingsManager.GetString("StartMaximized"), false, false);
        SettingsManager_SettingValueChanged("CloseMinimises", ManagerFactory.settingsManager.GetString("CloseMinimises"), false, false);
        SettingsManager_SettingValueChanged("DesktopLayoutOnStart", ManagerFactory.settingsManager.GetString("DesktopLayoutOnStart"), false, false);
        SettingsManager_SettingValueChanged("ToastEnable", ManagerFactory.settingsManager.GetString("ToastEnable"), false, false);
        SettingsManager_SettingValueChanged("CurrentCulture", ManagerFactory.settingsManager.GetString("CurrentCulture"), false, false);
        SettingsManager_SettingValueChanged("PlatformRTSSEnabled", ManagerFactory.settingsManager.GetString("PlatformRTSSEnabled"), false, false);
        SettingsManager_SettingValueChanged("QuickToolsLocation", ManagerFactory.settingsManager.GetString("QuickToolsLocation"), false, false);
        SettingsManager_SettingValueChanged("QuickToolsAutoHide", ManagerFactory.settingsManager.GetString("QuickToolsAutoHide"), false, false);
        SettingsManager_SettingValueChanged("UISounds", ManagerFactory.settingsManager.GetString("UISounds"), false, false);
        SettingsManager_SettingValueChanged("TelemetryEnabled", ManagerFactory.settingsManager.GetString("TelemetryEnabled"), false, false);
        SettingsManager_SettingValueChanged("ProcessPriority", ManagerFactory.settingsManager.GetString("ProcessPriority"), false, false);
        SettingsManager_SettingValueChanged("QuickKeyboardVisibility", ManagerFactory.settingsManager.GetString("QuickKeyboardVisibility"), false, false);
        SettingsManager_SettingValueChanged("QuickTrackpadVisibility", ManagerFactory.settingsManager.GetString("QuickTrackpadVisibility"), false, false);
        SettingsManager_SettingValueChanged("QuickToolsSlideAnimation", ManagerFactory.settingsManager.GetString("QuickToolsSlideAnimation"), false, false);
        SettingsManager_SettingValueChanged("PerformanceManagerEnabled", ManagerFactory.settingsManager.GetString("PerformanceManagerEnabled"), false, false);
        SettingsManager_SettingValueChanged("GPUManagementEnabled", ManagerFactory.settingsManager.GetString("GPUManagementEnabled"), false, false);
        SettingsManager_SettingValueChanged("LibraryPageEnabled", ManagerFactory.settingsManager.GetString("LibraryPageEnabled"), false, false);
        SettingsManager_SettingValueChanged("ShowSplashScreen", ManagerFactory.settingsManager.GetString("ShowSplashScreen"), false, false);
        SettingsManager_SettingValueChanged("DSUEnabled", ManagerFactory.settingsManager.GetString("DSUEnabled"), false, false);
        SettingsManager_SettingValueChanged("DSUport", ManagerFactory.settingsManager.GetString("DSUport"), false, false);
        SettingsManager_SettingValueChanged("VIIPEREnabled", ManagerFactory.settingsManager.GetString("VIIPEREnabled"), false, false);
        SettingsManager_SettingValueChanged("VIIPERPort", ManagerFactory.settingsManager.GetString("VIIPERPort"), false, false);
    }

    private void QueryPlatforms()
    {
        // manage events
        PlatformManager.RTSS.Updated += RTSS_Updated;

        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    private void PlatformManager_Initialized()
    {
        QueryPlatforms();
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
                .FirstOrDefault(screen => screen.DevicePath.Equals(DevicePath) || screen.FriendlyName.Equals(DeviceName));

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

    private void SettingsManager_SettingValueChanged(string? name, object? value, bool temporary, bool initializing)
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
                    {
                        bool enabled = Convert.ToBoolean(value);
                        Toggle_StartMinimized.IsOn = enabled;

                        if (enabled)
                            Toggle_StartMaximized.IsOn = !enabled;
                    }
                    break;
                case "StartMaximized":
                    {
                        bool enabled = Convert.ToBoolean(value);
                        Toggle_StartMaximized.IsOn = enabled;

                        if (enabled)
                            Toggle_StartMinimized.IsOn = !enabled;
                    }
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
                    string cultureName = value as string ?? string.Empty;
                    cB_Language.SelectedItem = cultureName switch
                    {
                        "" => TranslationSource.Instance.CurrentCulture,
                        _ => new CultureInfo(cultureName)
                    };
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
                    cB_Priority.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "QuickKeyboardVisibility":
                    VirtualKeyboardToggle.IsOn = Convert.ToBoolean(value);
                    break;
                case "QuickTrackpadVisibility":
                    VirtualTrackpadToggle.IsOn = Convert.ToBoolean(value);
                    break;
                case "QuickToolsSlideAnimation":
                    QuicktoolsSlideAnimationToggle.IsOn = Convert.ToBoolean(value);
                    break;
                case "PerformanceManagerEnabled":
                    Toggle_PerformanceManager.IsOn = Convert.ToBoolean(value);
                    break;
                case "GPUManagementEnabled":
                    Toggle_GPUManagement.IsOn = Convert.ToBoolean(value);
                    break;
                case "LibraryPageEnabled":
                    Toggle_LibraryPage.IsOn = Convert.ToBoolean(value);
                    break;
                case "QuickToolsApplyNoise":
                    QuickToolsNoiseToggle.IsOn = Convert.ToBoolean(value);
                    break;
                case "MainWindowApplyNoise":
                    MainWindowNoiseToggle.IsOn = Convert.ToBoolean(value);
                    break;
                case "ShowSplashScreen":
                    Toggle_SplashScreen.IsOn = Convert.ToBoolean(value);
                    break;
                case "DSUEnabled":
                    Toggle_DSUEnabled.IsOn = Convert.ToBoolean(value);
                    break;
                case "DSUport":
                    nB_DSUport.Value = Convert.ToInt32(value);
                    break;
                case "VIIPEREnabled":
                    Toggle_VIIPEREnabled.IsOn = Convert.ToBoolean(value);
                    break;
                case "VIIPERPort":
                    nB_VIIPERPort.Value = Convert.ToInt32(value);
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
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.multimediaManager.ScreenConnected -= MultimediaManager_ScreenConnected;
        ManagerFactory.multimediaManager.ScreenDisconnected -= MultimediaManager_ScreenDisconnected;
        ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;
        ViewModel.Dispose();
    }

    private async void Toggle_AutoStart_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("RunAtStartup", Toggle_AutoStart.IsOn);
    }

    private void Toggle_StartMinimized_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        bool startMinimized = Toggle_StartMinimized.IsOn;
        ManagerFactory.settingsManager.SetProperty("StartMinimized", startMinimized);

        // Enforce mutual exclusivity with StartMaximized
        if (startMinimized && Toggle_StartMaximized.IsOn)
        {
            Toggle_StartMaximized.IsOn = false;
            ManagerFactory.settingsManager.SetProperty("StartMaximized", false);
        }
    }

    private void Toggle_StartMaximized_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        bool StartMaximized = Toggle_StartMaximized.IsOn;
        ManagerFactory.settingsManager.SetProperty("StartMaximized", StartMaximized);

        // Enforce mutual exclusivity with StartMinimized
        if (StartMaximized && Toggle_StartMinimized.IsOn)
        {
            Toggle_StartMinimized.IsOn = false;
            ManagerFactory.settingsManager.SetProperty("StartMinimized", false);
        }
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
            ManagerFactory.settingsManager.SetProperty("QuickToolsDeviceName", desktopScreen.FriendlyName);
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

    private void VirtualKeyboardToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("QuickKeyboardVisibility", VirtualKeyboardToggle.IsOn);
    }

    private void VirtualTrackpadToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("QuickTrackpadVisibility", VirtualTrackpadToggle.IsOn);
    }

    private void QuicktoolsSlideAnimationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("QuickToolsSlideAnimation", QuicktoolsSlideAnimationToggle.IsOn);
    }

    private void Toggle_PerformanceManager_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("PerformanceManagerEnabled", Toggle_PerformanceManager.IsOn);
    }

    private void Toggle_GPUManagement_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("GPUManagementEnabled", Toggle_GPUManagement.IsOn);
    }

    private void Toggle_LibraryPage_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("LibraryPageEnabled", Toggle_LibraryPage.IsOn);
    }

    private void QuickToolsNoiseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("QuickToolsApplyNoise", QuickToolsNoiseToggle.IsOn);
    }

    private void MainWindowNoiseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("MainWindowApplyNoise", MainWindowNoiseToggle.IsOn);
    }

    private void Toggle_SplashScreen_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("ShowSplashScreen", Toggle_SplashScreen.IsOn);
    }

    private void Toggle_DSUEnabled_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("DSUEnabled", Toggle_DSUEnabled.IsOn);
    }

    private void nB_DSUport_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!IsLoaded || double.IsNaN(e.NewValue))
            return;

        ManagerFactory.settingsManager.SetProperty("DSUport", (int)e.NewValue);
    }

    private void Toggle_VIIPEREnabled_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("VIIPEREnabled", Toggle_VIIPEREnabled.IsOn);
    }

    private void nB_VIIPERPort_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!IsLoaded || double.IsNaN(e.NewValue))
            return;

        ManagerFactory.settingsManager.SetProperty("VIIPERPort", (int)e.NewValue);
    }

}
