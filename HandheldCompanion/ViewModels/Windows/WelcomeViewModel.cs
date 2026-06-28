using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Watchers;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Helpers.Styles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WindowHelper = iNKORE.UI.WPF.Modern.Controls.Helpers.WindowHelper;

namespace HandheldCompanion.ViewModels.Windows;

public class WelcomeViewModel : BaseViewModel
{
    private readonly IDevice? device;
    private readonly ISpaceWatcher? oemStackWatcher;

    private bool startWithWindows;
    private bool startMinimized;
    private int processPriority;
    private int mainWindowTheme;
    private int mainWindowBackdrop;
    private bool enhancedSleep;
    private bool toastNotifications;
    private bool telemetryEnabled;
    private bool disableOemStack;
    private bool oemStackChanged;
    private bool coreIsolation;
    private bool coreIsolationChanged;
    private bool performanceManagerEnabled;
    private bool gpuManagementEnabled;
    private bool libraryPageEnabled;
    private bool isFirstPage;
    private bool isLastPage;
    private bool welcomeWindowApplyNoise;

    private readonly CoreIsolationWatcher? coreIsolationWatcher;
    private readonly BitmapImage _deviceImage;
    private readonly Notifications.Notification? oemNotification;

    public WelcomeViewModel()
    {
        startWithWindows = true;
        startMinimized = true;
        processPriority = 1;
        mainWindowTheme = 2;
        mainWindowBackdrop = 1;
        enhancedSleep = true;
        toastNotifications = true;
        telemetryEnabled = true;
        coreIsolation = false;
        performanceManagerEnabled = true;
        gpuManagementEnabled = true;
        libraryPageEnabled = true;
        welcomeWindowApplyNoise = false;

        HasOemStack = true;
        _deviceImage = CreateDeviceImage();
    }

    public WelcomeViewModel(IDevice device)
    {
        this.device = device;
        _deviceImage = CreateDeviceImage();

        startWithWindows = ManagerFactory.settingsManager.GetBoolean("RunAtStartup");
        startMinimized = ManagerFactory.settingsManager.GetBoolean("StartMinimized");
        processPriority = ManagerFactory.settingsManager.GetInt("ProcessPriority");
        enhancedSleep = ManagerFactory.settingsManager.GetBoolean("EnhancedSleep");
        toastNotifications = ManagerFactory.settingsManager.GetBoolean("ToastEnable");
        telemetryEnabled = ManagerFactory.settingsManager.GetBoolean("TelemetryEnabled");

        // device
        coreIsolationWatcher = new CoreIsolationWatcher();
        coreIsolation = coreIsolationWatcher.VulnerableDriverBlocklistEnable || coreIsolationWatcher.HypervisorEnforcedCodeIntegrityEnabled || coreIsolationWatcher.SmartAppControlEnabled;

        // Pull the current OEM stack state on startup
        oemStackWatcher = ISpaceWatcher.Create(device);
        if (oemStackWatcher is not null)
        {
            // set flag
            HasOemStack = true;

            // Store the notification for MVVM binding
            oemNotification = oemStackWatcher.notification;

            // Initialize with synchronous check to avoid binding race condition
            disableOemStack = !oemStackWatcher.IsRunning;
        }

        // components
        performanceManagerEnabled = ManagerFactory.settingsManager.GetBoolean("PerformanceManagerEnabled");
        gpuManagementEnabled = ManagerFactory.settingsManager.GetBoolean("GPUManagementEnabled");
        libraryPageEnabled = ManagerFactory.settingsManager.GetBoolean("LibraryPageEnabled");

        // theme
        mainWindowTheme = ManagerFactory.settingsManager.GetInt("MainWindowTheme");
        mainWindowBackdrop = ManagerFactory.settingsManager.GetInt("MainWindowBackdrop");
        welcomeWindowApplyNoise = ManagerFactory.settingsManager.GetBoolean("MainWindowApplyNoise");
    }

    public string DeviceName => string.IsNullOrEmpty(MotherboardInfo.Product) ? "Generic device" : MotherboardInfo.Product;

    public string DeviceDetails => device is null ? "ASUS • Handheld (RC72LA)" : string.Join(" • ", GetDeviceDetails());

    public string Processor => device is null ? "AMD Ryzen Z1 Extreme" : string.IsNullOrWhiteSpace(device.Processor) ? MotherboardInfo.ProcessorName : device.Processor;

    public string TdpRange => device is null ? "5-30W TDP" : $"{device.cTDP[0]:0}-{device.cTDP[1]:0}W TDP";

    public string SensorNames
    {
        get
        {
            List<string> sensors = [];

            if (device is null)
                return "STMicroelectronics SensorHub";

            if (!string.IsNullOrWhiteSpace(device.InternalSensorName))
                sensors.Add(device.InternalSensorName);

            if (!string.IsNullOrWhiteSpace(device.ExternalSensorName))
                sensors.Add(device.ExternalSensorName);

            return string.Join(" • ", sensors);
        }
    }

    public string DeviceImagePath => $"/Resources/DeviceImages/{(device?.ProductIllustration ?? "device_rog_ally_x")}.png";

    public BitmapImage DeviceImage => _deviceImage;

    private BitmapImage CreateDeviceImage()
    {
        string imagePath = DeviceImagePath;
        try
        {
            return new BitmapImage(new Uri(imagePath, UriKind.Relative));
        }
        catch
        {
            return new BitmapImage(new Uri("/Resources/DeviceImages/device_generic.png", UriKind.Relative));
        }
    }

    public bool HasFanControl => device is null || device.Capabilities.HasFlag(DeviceCapabilities.FanControl);
    public bool HasLighting => device is null || device.Capabilities.HasFlag(DeviceCapabilities.DynamicLighting);
    public bool HasBatteryControls => device is null || device.Capabilities.HasFlag(DeviceCapabilities.BatteryChargeLimit) || device.Capabilities.HasFlag(DeviceCapabilities.BatteryBypassCharging);
    public bool HasMotionSensor => device is null || device.Capabilities.HasFlag(DeviceCapabilities.InternalSensor) || device.Capabilities.HasFlag(DeviceCapabilities.ExternalSensor);
    public bool HasSensorName => !string.IsNullOrWhiteSpace(SensorNames);
    public bool HasMotionSensorWithoutName => HasMotionSensor && !HasSensorName;

    public bool HasOemStack { get; }
    public Visibility OemStackVisibility => HasOemStack ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoOemStackVisibility => HasOemStack ? Visibility.Collapsed : Visibility.Visible;
    public Visibility OemStackRunningVisibility => HasOemStack && oemStackWatcher.IsRunning ? Visibility.Visible : Visibility.Collapsed;

    public string OemInfoBarTitle => oemNotification?.Title ?? "Reversible choice";
    public string OemInfoBarMessage => oemNotification?.Message ?? "This can be restored later by HC restore logic or by enabling the OEM stack again.";

    public bool StartWithWindows
    {
        get => startWithWindows;
        set
        {
            if (SetProperty(ref startWithWindows, value))
                ApplySetting("RunAtStartup", value);
        }
    }

    public bool StartMinimized
    {
        get => startMinimized;
        set
        {
            if (SetProperty(ref startMinimized, value))
                ApplySetting("StartMinimized", value);
        }
    }

    public int ProcessPriority
    {
        get => processPriority;
        set
        {
            if (SetProperty(ref processPriority, value))
            {
                ApplyProcessPriority(value);
                ApplySetting("ProcessPriority", value);
            }
        }
    }

    public int MainWindowTheme
    {
        get => mainWindowTheme;
        set
        {
            if (SetProperty(ref mainWindowTheme, value))
            {
                ApplyTheme(value);
                ApplySetting("MainWindowTheme", value);
            }
        }
    }

    public int MainWindowBackdrop
    {
        get => mainWindowBackdrop;
        set
        {
            if (SetProperty(ref mainWindowBackdrop, value))
            {
                // Save the setting first — SettingsPage's SettingValueChanged handler
                // will apply the backdrop to MainWindow/QuickTools if they are loaded.
                // We then apply directly to WelcomeWindow itself, which SettingsPage doesn't cover.
                ApplySetting("MainWindowBackdrop", value);
                ApplyBackdrop(value);
            }
        }
    }

    public bool EnhancedSleep
    {
        get => enhancedSleep;
        set
        {
            if (SetProperty(ref enhancedSleep, value))
                ApplySetting("EnhancedSleep", value);
        }
    }

    public bool ToastNotifications
    {
        get => toastNotifications;
        set
        {
            if (SetProperty(ref toastNotifications, value))
                ApplySetting("ToastEnable", value);
        }
    }

    public bool TelemetryEnabled
    {
        get => telemetryEnabled;
        set
        {
            if (SetProperty(ref telemetryEnabled, value))
                ApplySetting("TelemetryEnabled", value);
        }
    }

    public bool DisableOemStack
    {
        get => disableOemStack;
        set
        {
            if (SetProperty(ref disableOemStack, value))
            {
                ApplyOemSoftwareStack(value);
                oemStackChanged = true;
                OnPropertyChanged(nameof(RestartRequired));
            }
        }
    }

    public bool CoreIsolation
    {
        get => coreIsolation;
        set
        {
            if (SetProperty(ref coreIsolation, value))
            {
                ApplyCoreIsolation(value);
                coreIsolationChanged = true;
                OnPropertyChanged(nameof(RestartRequired));
            }
        }
    }

    public bool RestartRequired => oemStackChanged || coreIsolationChanged;

    public bool IsFirstPage
    {
        get => isFirstPage;
        set => SetProperty(ref isFirstPage, value);
    }

    public bool IsLastPage
    {
        get => isLastPage;
        set => SetProperty(ref isLastPage, value);
    }

    public bool PerformanceManagerEnabled
    {
        get => performanceManagerEnabled;
        set
        {
            if (SetProperty(ref performanceManagerEnabled, value))
                ApplySetting("PerformanceManagerEnabled", value);
        }
    }

    public bool GpuManagementEnabled
    {
        get => gpuManagementEnabled;
        set
        {
            if (SetProperty(ref gpuManagementEnabled, value))
                ApplySetting("GPUManagementEnabled", value);
        }
    }

    public bool LibraryPageEnabled
    {
        get => libraryPageEnabled;
        set
        {
            if (SetProperty(ref libraryPageEnabled, value))
                ApplySetting("LibraryPageEnabled", value);
        }
    }

    public bool WelcomeWindowApplyNoise
    {
        get => welcomeWindowApplyNoise;
        set
        {
            if (SetProperty(ref welcomeWindowApplyNoise, value))
                ApplySetting("WelcomeWindowApplyNoise", value);
        }
    }

    public void Commit(bool completed)
    {
    }

    private void ApplySetting(string name, object value)
    {
        if (device is null)
            return;

        ManagerFactory.settingsManager.SetProperty(name, value);
    }

    private void ApplyProcessPriority(int value)
    {
        if (device is null)
            return;

        using Process process = Process.GetCurrentProcess();
        process.PriorityClass = value switch
        {
            1 => ProcessPriorityClass.AboveNormal,
            2 => ProcessPriorityClass.High,
            _ => ProcessPriorityClass.Normal
        };
    }

    private void ApplyTheme(int value)
    {
        if (device is null)
            return;

        ElementTheme elementTheme = (ElementTheme)value;
        foreach (Window window in Application.Current.Windows.OfType<Window>())
            ThemeManager.SetRequestedTheme(window, elementTheme);
    }

    private void ApplyBackdrop(int value)
    {
        if (device is null)
            return;

        // SettingsPage's SettingValueChanged handler already covers MainWindow and OverlayQuickTools
        // when they are loaded. Apply only to windows not handled there (e.g. WelcomeWindow itself).
        foreach (Window window in Application.Current.Windows.OfType<Window>())
        {
            if (window is not HandheldCompanion.Views.Windows.WelcomeWindow)
                continue;

            SwitchBackdrop(window, value);
        }
    }

    private static void SwitchBackdrop(Window targetWindow, int idx)
    {
        targetWindow.ApplyTemplate();
        targetWindow.UpdateLayout();

        try
        {
            switch (idx)
            {
                case 0:
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.None);
                    break;
                case 1:
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Mica);
                    break;
                case 2:
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Tabbed);
                    break;
                case 3:
                    WindowHelper.SetSystemBackdropType(targetWindow, BackdropType.Acrylic);
                    break;
            }
        }
        catch
        {
        }
    }

    private void ApplyOemSoftwareStack(bool disable)
    {
        try
        {
            if (device is null)
                return;

            if (disable)
                oemStackWatcher?.Disable();
            else
                oemStackWatcher?.Enable();
        }
        catch (Exception ex)
        {
            HandheldCompanion.Shared.LogManager.LogError("Failed to update OEM software stack during onboarding: {0}", ex.Message);
        }
    }

    private void ApplyCoreIsolation(bool enabled)
    {
        if (device is null || coreIsolationWatcher is null)
            return;

        if (enabled != (coreIsolationWatcher.VulnerableDriverBlocklistEnable || coreIsolationWatcher.HypervisorEnforcedCodeIntegrityEnabled || coreIsolationWatcher.SmartAppControlEnabled))
            coreIsolationWatcher.SetSettings(enabled);
    }

    private IEnumerable<string> GetDeviceDetails()
    {
        if (device is null)
            yield break;

        if (!string.IsNullOrWhiteSpace(device.ManufacturerName))
            yield return device.ManufacturerName;

        if (!string.IsNullOrWhiteSpace(device.ProductName))
            yield return device.ProductName;

        if (!string.IsNullOrWhiteSpace(device.SystemModel))
            yield return device.SystemModel;
    }
}
