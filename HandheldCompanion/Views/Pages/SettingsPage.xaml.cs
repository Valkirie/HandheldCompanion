using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ControllerCommon.Devices;
using ControllerCommon.Platforms;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using Inkore.UI.WPF.Modern;
using Inkore.UI.WPF.Modern.Controls;
using Inkore.UI.WPF.Modern.Controls.Primitives;
using Nefarius.Utilities.DeviceManagement.PnP;
using static ControllerCommon.Utils.DeviceUtils;
using static HandheldCompanion.Managers.UpdateManager;
using Page = System.Windows.Controls.Page;
using ServiceControllerStatus = ControllerCommon.Managers.ServiceControllerStatus;

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
        foreach (var mode in ((ServiceStartMode[])Enum.GetValues(typeof(ServiceStartMode))).Where(mode =>
                     mode >= ServiceStartMode.Automatic))
        {
            RadioButton radio = new() { Content = EnumUtils.GetDescriptionFromEnumValue(mode) };
            switch (mode)
            {
                case ServiceStartMode.Disabled:
                    radio.IsEnabled = false;
                    break;
            }

            cB_StartupType.Items.Add(radio);
        }

        cB_Language.Items.Add(new CultureInfo("en-US"));
        cB_Language.Items.Add(new CultureInfo("fr-FR"));
        cB_Language.Items.Add(new CultureInfo("de-DE"));
        cB_Language.Items.Add(new CultureInfo("it-IT"));
        cB_Language.Items.Add(new CultureInfo("ja-JP"));
        cB_Language.Items.Add(new CultureInfo("pt-BR"));
        cB_Language.Items.Add(new CultureInfo("zh-CN"));
        cB_Language.Items.Add(new CultureInfo("zh-Hant"));

        // call function
        UpdateDevice();

        // initialize manager(s)
        MainWindow.serviceManager.Updated += OnServiceUpdate;
        MainWindow.updateManager.Updated += UpdateManager_Updated;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        PlatformManager.RTSS.Updated += RTSS_Updated;
        PlatformManager.HWiNFO.Updated += HWiNFO_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
        HWiNFO_Updated(PlatformManager.HWiNFO.Status);
    }

    public SettingsPage(string? Tag) : this()
    {
        this.Tag = Tag;
    }

    private void HWiNFO_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    Toggle_HWiNFO.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    Toggle_HWiNFO.IsOn = false;
                    break;
            }
        });
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
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
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
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
                case "SensorSelection":
                {
                    var idx = Convert.ToInt32(value);

                    // default value
                    if (idx == -1)
                    {
                        if (MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ControllerSensor))
                            SettingsManager.SetProperty("SensorSelection",
                                cB_SensorSelection.Items.IndexOf(SensorController));
                        else if (MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.InternalSensor))
                            SettingsManager.SetProperty("SensorSelection",
                                cB_SensorSelection.Items.IndexOf(SensorInternal));
                        else if (MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ExternalSensor))
                            SettingsManager.SetProperty("SensorSelection",
                                cB_SensorSelection.Items.IndexOf(SensorExternal));
                        else
                            SettingsManager.SetProperty("SensorSelection",
                                cB_SensorSelection.Items.IndexOf(SensorNone));

                        return;
                    }

                    cB_SensorSelection.SelectedIndex = idx;

                    cB_SensorSelection.SelectedIndex = idx;

                    // bug: SelectionChanged not triggered when control isn't loaded
                    if (!IsLoaded)
                        cB_SensorSelection_SelectionChanged(this, null);
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
                case "VirtualControllerForceOrder":
                    Toggle_ForceVirtualControllerOrder.IsOn = Convert.ToBoolean(value);
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
                case "StartServiceWithCompanion":
                    Toggle_ServiceStartup.IsOn = Convert.ToBoolean(value);
                    break;
                case "HaltServiceWithCompanion":
                    Toggle_ServiceShutdown.IsOn = Convert.ToBoolean(value);
                    break;
                case "SensorPlacementUpsideDown":
                    Toggle_SensorPlacementUpsideDown.IsOn = Convert.ToBoolean(value);
                    break;
                case "ConfigurableTDPOverride":
                    Toggle_cTDP.IsOn = Convert.ToBoolean(value);
                    break;
                case "ConfigurableTDPOverrideDown":
                    NumberBox_TDPMin.Value = Convert.ToDouble(value);
                    break;
                case "ConfigurableTDPOverrideUp":
                    NumberBox_TDPMax.Value = Convert.ToDouble(value);
                    break;
                case "CurrentCulture":
                    cB_Language.SelectedItem = new CultureInfo((string)value);

                    // bug: SelectionChanged not triggered when control isn't loaded
                    if (!IsLoaded)
                        cB_Language_SelectionChanged(this, null);
                    break;
                case "SensorPlacement":
                    UpdateUI_SensorPlacement(Convert.ToInt32(value));
                    break;
                case "ServiceStartMode":
                    cB_StartupType.SelectedIndex = Convert.ToInt32(value);

                    // bug: SelectionChanged not triggered when control isn't loaded
                    if (!IsLoaded)
                        cB_StartupType_SelectionChanged(this, null);
                    break;

                case "PlatformRTSSEnabled":
                    Toggle_RTSS.IsOn = Convert.ToBoolean(value);
                    break;
                case "PlatformHWiNFOEnabled":
                    Toggle_HWiNFO.IsOn = Convert.ToBoolean(value);
                    break;

                case "QuickToolsLocation":
                    cB_QuicktoolsPosition.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "QuickToolsAutoHide":
                    Toggle_QuicktoolsAutoHide.IsOn = Convert.ToBoolean(value);
                    break;
            }
        });
    }

    public void UpdateDevice(PnPDevice device = null)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SensorInternal.IsEnabled = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.InternalSensor);
            SensorExternal.IsEnabled = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ExternalSensor);
            SensorController.IsEnabled = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ControllerSensor);
        });
    }

    private void Page_Loaded(object? sender, RoutedEventArgs? e)
    {
        MainWindow.updateManager.Start();
    }

    public void Page_Closed()
    {
        MainWindow.serviceManager.Updated -= OnServiceUpdate;
    }

    private async void Toggle_AutoStart_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        if (!Toggle_AutoStart.IsOn && SettingsManager.GetBoolean("VirtualControllerForceOrder"))
        {
            var result = Dialog.ShowAsync(Properties.Resources.SettingsPage_AutoStartTitle,
                Properties.Resources.SettingsPage_AutoStartText,
                ContentDialogButton.Primary, null,
                Properties.Resources.SettingsPage_AutoStartPrimary,
                Properties.Resources.SettingsPage_AutoStartSecondary);

            await result;

            switch(result.Result)
            {
                case ContentDialogResult.Primary:
                    SettingsManager.SetProperty("VirtualControllerForceOrder", false);
                    break;
                case ContentDialogResult.Secondary:
                    Toggle_AutoStart.IsOn = true;
                    break;
            }
        }

        SettingsManager.SetProperty("RunAtStartup", Toggle_AutoStart.IsOn);
    }

    private void Toggle_Background_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("StartMinimized", Toggle_Background.IsOn);
    }

    private void cB_StartupType_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_StartupType.SelectedIndex == -1)
            return;

        ServiceStartMode mode;
        switch (cB_StartupType.SelectedIndex)
        {
            case 0:
                mode = ServiceStartMode.Automatic;
                break;
            default:
            case 1:
                mode = ServiceStartMode.Manual;
                break;
            case 2:
                mode = ServiceStartMode.Disabled;
                break;
        }

        MainWindow.serviceManager.SetStartType(mode);

        // service was not found
        if (!cB_StartupType.IsEnabled)
            return;

        SettingsManager.SetProperty("ServiceStartMode", cB_StartupType.SelectedIndex);
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

    private async void Toggle_ForceVirtualControllerOrder_Toggled(object sender, RoutedEventArgs e)
    {
        var ForceVirtualControllerOrder = SettingsManager.GetBoolean("VirtualControllerForceOrder");

        if (Toggle_ForceVirtualControllerOrder.IsOn && !ForceVirtualControllerOrder)
        {
            var physicalControllerInstanceIds = new StringCollection();
            var physicalControllers = ControllerManager.GetPhysicalControllers();

            foreach (var physicalController in physicalControllers)
            {
                physicalControllerInstanceIds.Add(physicalController.Details.baseContainerDeviceInstanceId);
            }

            SettingsManager.SetProperty("PhysicalControllerInstanceIds", physicalControllerInstanceIds);

            var result = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_ForceVirtualControllerOrderTitle}",
                $"{Properties.Resources.SettingsPage_ForceVirtualControllerOrderText}",
                ContentDialogButton.Primary, null,
                $"{Properties.Resources.SettingsPage_ForceVirtualControllerOrderPrimary}",
                $"{Properties.Resources.SettingsPage_ForceVirtualControllerOrderSecondary}");

            await result;

            switch (result.Result)
            {
                case ContentDialogResult.Primary:
                    using (Process shutdown = new())
                    {
                        shutdown.StartInfo.FileName = "shutdown.exe";
                        shutdown.StartInfo.Arguments = "-r -t 3";

                        shutdown.StartInfo.UseShellExecute = false;
                        shutdown.StartInfo.CreateNoWindow = true;
                        shutdown.Start();
                    }
                    break;
                case ContentDialogResult.Secondary:
                    break;
            }
        }

        // force auto start to on when using this feature
        if (Toggle_ForceVirtualControllerOrder.IsOn)
            SettingsManager.SetProperty("RunAtStartup", true);

        SettingsManager.SetProperty("VirtualControllerForceOrder", Toggle_ForceVirtualControllerOrder.IsOn);
    }

    private void Button_DetectNativeDisplayOrientation_Click(object sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        var rotation = SystemManager.GetScreenOrientation();
        rotation = new ScreenRotation(rotation.rotationUnnormalized, ScreenRotation.Rotations.UNSET);
        SettingsManager.SetProperty("NativeDisplayOrientation", (int)rotation.rotationNativeBase);
    }

    private void UpdateManager_Updated(UpdateStatus status, UpdateFile updateFile, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
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
                                               MainWindow.updateManager.GetTime();

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
                            MainWindow.updateManager.DownloadUpdateFile(update);
                        };

                        // Set button action
                        update.updateInstall.Click += (sender, e) =>
                        {
                            MainWindow.updateManager.InstallUpdate(update);
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
        new Thread(() => { MainWindow.updateManager.StartProcess(); }).Start();
    }

    private void Toggle_ServiceShutdown_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("HaltServiceWithCompanion", Toggle_ServiceShutdown.IsOn);
    }

    private void Toggle_ServiceStartup_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("StartServiceWithCompanion", Toggle_ServiceStartup.IsOn);
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

        _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_AppLanguageWarning}",
            Properties.Resources.SettingsPage_AppLanguageWarningDesc,
            ContentDialogButton.Primary, string.Empty, $"{Properties.Resources.ProfilesPage_OK}");
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

    private async void Toggle_cTDP_Toggled(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded)
            return;

        if (Toggle_cTDP.IsOn)
        {
            // todo: localize me !
            var result = Dialog.ShowAsync(
                "Warning",
                "Altering minimum and maximum CPU power values might cause instabilities. Product warranties may not apply if the processor is operated beyond its specifications. Use at your own risk.",
                ContentDialogButton.Primary, "Cancel", Properties.Resources.ProfilesPage_OK);

            await result; // sync call

            switch (result.Result)
            {
                case ContentDialogResult.Primary:
                    break;
                default:
                case ContentDialogResult.None:
                    // restore previous state
                    Toggle_cTDP.IsOn = false;
                    return;
            }
        }

        SettingsManager.SetProperty("ConfigurableTDPOverride", Toggle_cTDP.IsOn);
        SettingsManager.SetProperty("ConfigurableTDPOverrideUp", NumberBox_TDPMax.Value);
        SettingsManager.SetProperty("ConfigurableTDPOverrideDown", NumberBox_TDPMin.Value);
    }

    private void NumberBox_TDPMax_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
    {
        var value = NumberBox_TDPMax.Value;
        if (double.IsNaN(value))
            return;

        NumberBox_TDPMin.Maximum = value;

        if (!IsLoaded)
            return;

        // update current device cTDP
        MainWindow.CurrentDevice.cTDP[1] = value;

        SettingsManager.SetProperty("ConfigurableTDPOverrideUp", value);
    }

    private void NumberBox_TDPMin_ValueChanged(NumberBox? sender, NumberBoxValueChangedEventArgs? args)
    {
        var value = NumberBox_TDPMin.Value;
        if (double.IsNaN(value))
            return;

        NumberBox_TDPMax.Minimum = value;

        if (!IsLoaded)
            return;

        // update current device cTDP
        MainWindow.CurrentDevice.cTDP[0] = value;

        SettingsManager.SetProperty("ConfigurableTDPOverrideDown", value);
    }

    private void cB_SensorSelection_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (cB_SensorSelection.SelectedIndex == -1)
            return;

        // update dependencies
        Toggle_SensorPlacementUpsideDown.IsEnabled =
            cB_SensorSelection.SelectedIndex == (int)SensorFamily.SerialUSBIMU ? true : false;
        Grid_SensorPlacementVisualisation.IsEnabled =
            cB_SensorSelection.SelectedIndex == (int)SensorFamily.SerialUSBIMU ? true : false;

        if (IsLoaded)
            SettingsManager.SetProperty("SensorSelection", cB_SensorSelection.SelectedIndex);
    }

    private void SensorPlacement_Click(object sender, RoutedEventArgs? e)
    {
        var Tag = int.Parse((string)((Button)sender).Tag);

        UpdateUI_SensorPlacement(Tag);

        if (IsLoaded)
            SettingsManager.SetProperty("SensorPlacement", Tag);
    }

    private void UpdateUI_SensorPlacement(int? SensorPlacement)
    {
        foreach (SimpleStackPanel panel in Grid_SensorPlacementVisualisation.Children)
        foreach (Button button in panel.Children)
            if (int.Parse((string)button.Tag) == SensorPlacement)
                button.Background = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
            else
                button.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltBaseLowBrush"];
    }

    private void Toggle_SensorPlacementUpsideDown_Toggled(object? sender, RoutedEventArgs? e)
    {
        var isUpsideDown = Toggle_SensorPlacementUpsideDown.IsOn;

        if (IsLoaded)
            SettingsManager.SetProperty("SensorPlacementUpsideDown", isUpsideDown);
    }

    #region serviceManager

    private void OnServiceUpdate(ServiceControllerStatus status, int mode)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case ServiceControllerStatus.Paused:
                case ServiceControllerStatus.Stopped:
                case ServiceControllerStatus.Running:
                case ServiceControllerStatus.ContinuePending:
                case ServiceControllerStatus.PausePending:
                case ServiceControllerStatus.StartPending:
                case ServiceControllerStatus.StopPending:
                    cB_StartupType.IsEnabled = true;
                    break;
                default:
                    cB_StartupType.IsEnabled = false;
                    break;
            }

            if (mode != -1)
            {
                var serviceMode = (ServiceStartMode)mode;
                switch (serviceMode)
                {
                    case ServiceStartMode.Automatic:
                        cB_StartupType.SelectedIndex = 0;
                        break;
                    default:
                    case ServiceStartMode.Manual:
                        cB_StartupType.SelectedIndex = 1;
                        break;
                    case ServiceStartMode.Disabled:
                        cB_StartupType.SelectedIndex = 2;
                        break;
                }

                // only allow users to set those options when service mode is set to Manual
                Toggle_ServiceStartup.IsEnabled = serviceMode != ServiceStartMode.Automatic;
                Toggle_ServiceShutdown.IsEnabled = serviceMode != ServiceStartMode.Automatic;
            }
        });
    }

    #endregion

    private void Toggle_RTSS_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("PlatformRTSSEnabled", Toggle_RTSS.IsOn);
    }

    private void Toggle_HWiNFO_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("PlatformHWiNFOEnabled", Toggle_HWiNFO.IsOn);
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
}