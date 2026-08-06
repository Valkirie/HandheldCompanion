using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Localization;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using HandheldCompanion.Watchers;
using iNKORE.UI.WPF.Modern.Common;
using iNKORE.UI.WPF.Modern.Controls;
using Sentry;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.UI.ViewManagement;
using ApplicationSettings = HandheldCompanion.Properties.Settings;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace HandheldCompanion;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static bool IsMultiThreaded { get; } = true;

    public static string InstallPath = string.Empty;
    public static string SettingsPath = string.Empty;
    public static string LogsPath = string.Empty;
    public static string GameControllerDbPath = string.Empty;

    // App path/version state
    public static string CurrentExe => Environment.ProcessPath ?? string.Empty;
    public static string CurrentPath => AppDomain.CurrentDomain.BaseDirectory;
    private static FileVersionInfo? fileVersionInfo;
    public static Version LastVersion { get; private set; }
    public static Version CurrentVersion => Version.Parse(fileVersionInfo?.FileVersion ?? "0.0.0.0");
    public static bool IsFirstStart { get; private set; }
    private static Version WelcomeVersion = new Version("0.31.3.1");

    // Shared UI state
    public static UISettings uiSettings = null!;
    public static OverlayModel overlayModel = null!;
    public static OverlayTrackpad overlayTrackpad = null!;
    public static OverlayQuickTools overlayquickTools = null!;

    private const string UninstallRestoreArgument = "--uninstall-restore";
    private bool restartRequiredAfterMsiClawMigration;

    public static string ApplicationName
    {
        get => Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
        set { /* noop */ }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();

    static readonly string[] ReservedFileNames = new[] { "Certificate.pfx", "SecretKeys.cs" };

    /// <summary>
    ///     Initializes the singleton application object.  This is the first line of authored code
    ///     executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeSentry();

        ShadowAssist.UseBitmapCache = false;

        InjectResource();

        InitializeComponent();

        // initialize path(s)
        InstallPath = AppDomain.CurrentDomain.BaseDirectory;
        SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationName);
        LogsPath = Path.Combine(SettingsPath, "logs");
        GameControllerDbPath = Path.Combine(InstallPath, "gamecontrollerdb.txt");

        // Initialize LogManager before accessing ManagerFactory to prevent initialization order issues
        Environment.SetEnvironmentVariable("LOG_PATH", LogsPath);
#if DEBUG
        AllocConsole();
#endif
        LogManager.Initialize(ApplicationName);
    }

    static DateTime GetLatestUserConfigWriteUtc(string root)
    {
        try
        {
            if (!Directory.Exists(root))
                return DateTime.MinValue;

            var userConfigs = Directory.GetFiles(root, "user.config", SearchOption.AllDirectories);
            if (userConfigs.Length == 0)
                return DateTime.MinValue;

            // latest write time among all user.config files found
            return userConfigs
                .Select(path => File.GetLastWriteTimeUtc(path))
                .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(root))
                .Max();
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    static bool IsReservedFile(string destRoot, string fullPath)
    {
        // Reserved files are expected at the root of SettingsPath
        var fileName = Path.GetFileName(fullPath);
        var parent = Path.GetFullPath(Path.GetDirectoryName(fullPath) ?? "");
        var root = Path.GetFullPath(destRoot);

        return StringComparer.OrdinalIgnoreCase.Equals(parent, root) &&
               ReservedFileNames.Any(r => StringComparer.OrdinalIgnoreCase.Equals(r, fileName));
    }

    /// <summary>
    /// Copies all content from source into dest, overwriting existing files
    /// except the reserved files (Certificate.pfx, SecretKeys.cs) at dest root.
    /// Does NOT delete anything in dest; it’s a merge that preserves reserved files.
    /// </summary>
    static void MergeCopyPreservingReserved(string source, string dest, bool tryDeleteSource, bool backupSource)
    {
        Directory.CreateDirectory(dest);

        // Create all directories first
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            string targetDir = dir.Replace(source, dest);
            Directory.CreateDirectory(targetDir);
        }

        // Copy files with preservation rule
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string targetFile = file.Replace(source, dest);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            // if target is a reserved root file, skip overwrite
            if (File.Exists(targetFile) && IsReservedFile(dest, targetFile))
                continue;

            try
            {
                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);

                File.Copy(file, targetFile, overwrite: true);
            }
            catch
            {
                // best-effort copy; ignore blocked files
            }
        }

        if (backupSource)
        {
            using (FileStream zipFile = File.Open("HandheldCompanion.zip", FileMode.Create))
                ZipFile.CreateFromDirectory(source, zipFile);
        }


        if (tryDeleteSource)
        {
            try { Directory.Delete(source, true); } catch { /* ignore (OneDrive/CFA/etc.) */ }
        }
    }

    /// <summary>
    /// Replaces the default ResourceManager instance in the auto-generated Resources class
    /// with a custom ResilientResourceManager that supports fallback logic.
    /// </summary>
    private void InjectResource()
    {
        Type resourcesType = typeof(Resources);
        var customManager = new ResilientResourceManager(resourcesType.FullName ?? resourcesType.Name, resourcesType.Assembly);
        FieldInfo? field = resourcesType.GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null) return;
        field.SetValue(null, customManager);
    }

    /// <summary>
    /// <summary>
    /// Invoked when the application is launched normally by the end user.
    /// </summary>
    protected override void OnStartup(StartupEventArgs args)
    {
        try
        {
            if (!InitializePreamble(args))
                return;

            SetupEnvironment();

            // Cache version info early before anything changes it
            LastVersion = Version.Parse(ManagerFactory.settingsManager.GetString("LastVersion"));
            IsFirstStart = LastVersion == Version.Parse("0.0.0.0");
            bool newUpdate = LastVersion != CurrentVersion;
            string exePath = CurrentExe;

            // Show the splash on the main UI thread — required by iNKORE's ThemeManager.
            SplashScreenHost splashScreen = new();

#if !DEBUG
            if (ManagerFactory.settingsManager.GetBoolean("ShowSplashScreen"))
                splashScreen.Show();
#endif

            IDevice device = InitializeDevice(splashScreen);

            // if first start or first time using this version, show the welcome screen
            if (IsFirstStart || LastVersion < WelcomeVersion)
            {
                ShutdownMode previousShutdownMode = ShutdownMode;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                splashScreen.SetStatus("Preparing welcome...");
                WelcomeWindow welcomeWindow = new(device);
                welcomeWindow.ShowDialog();

                ShutdownMode = previousShutdownMode;

                if (welcomeWindow.Completed && welcomeWindow.RestartRequired)
                {
                    // Update LastVersion to prevent showing WelcomeWindow again on next start
                    ManagerFactory.settingsManager.SetProperty("LastVersion", CurrentVersion.ToString());

                    splashScreen.SetStatus("Restarting device...");
                    splashScreen.Close();
                    PowerActions.Restart(force: false);
                    Shutdown(0);
                    return;
                }
            }

            // Initialize overlay windows after any WelcomeWindow has been shown and closed.
            // These windows use iNKORE modern window style which registers shared static XAML
            // state (DependencySource) during InitializeComponent; creating them before another
            // themed window (WelcomeWindow) causes "Must create DependencySource on same Thread"
            // during that window's template load.
            splashScreen.SetStatus("Initializing overlay windows...");
            overlayModel = new OverlayModel();
            overlayTrackpad = new OverlayTrackpad();
            overlayquickTools = new OverlayQuickTools();

            MainWindow = new MainWindow(splashScreen, device);

            // Pages are guaranteed to exist because MainWindow construction (loadPages()) is complete.
            ManagerFactory.settingsManager.SetProperty("LastVersion", fileVersionInfo?.FileVersion);
            Task.Run(() => StartNonUIInit(exePath, IsFirstStart, newUpdate, splashScreen.SetStatus));

            MainWindow.Show();

            if (restartRequiredAfterMsiClawMigration)
                RequestRestartConfirmation();
        }
        catch (Exception ex)
        {
            try
            {
                LogManager.LogCritical("Fatal startup exception: {0}\t{1}", ex.Message, ex.StackTrace ?? string.Empty);
                if (ex.InnerException != null)
                    LogManager.LogCritical("Inner exception: {0}\t{1}", ex.InnerException.Message, ex.InnerException.StackTrace ?? string.Empty);
            }
            catch { }

            try
            {
                MessageBox.Show(
                    $"A fatal error occurred during startup:\n\n{ex.Message}\n\nPlease check the logs for more details.",
                    "Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }

            throw;
        }
    }

    /// <summary>
    /// Initializes the assembly version info, sets environment variables, logs the startup,
    /// and checks for duplicate instances. Returns <c>false</c> if startup should be aborted.
    /// </summary>
    private bool InitializePreamble(StartupEventArgs args)
    {
        if (args.Args.Any(arg => string.Equals(arg, UninstallRestoreArgument, StringComparison.OrdinalIgnoreCase)))
        {
            RunUninstallRestoreMode();
            return false;
        }

        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        fileVersionInfo = FileVersionInfo.GetVersionInfo(currentAssembly.Location);

        Environment.SetEnvironmentVariable("COMPlus_legacyCorruptedStateExceptionsPolicy", "1");

        LogManager.LogInformation("{0} ({1})", currentAssembly.GetName().ToString(), fileVersionInfo.FileVersion ?? string.Empty);

        using Process process = Process.GetCurrentProcess();
        Process[] processes = Process.GetProcessesByName(process.ProcessName);
        if (processes.Length > 1)
        {
            using Process prevProcess = processes[0];

            if (!prevProcess.Responding)
            {
                MessageBox.Show("Another instance of Handheld Companion is already running.\n\nPlease close the other instance and try again.", "Error");
                process.Kill();
                return false;
            }

            IntPtr hWnd = WinAPI.FindWindowByProcessId(prevProcess.Id, HandheldCompanion.Properties.Resources.MainWindow_HandheldCompanion);
            if (hWnd != IntPtr.Zero)
            {
                ProcessUtils.ShowWindow(hWnd, (int)ProcessUtils.ShowWindowCommands.Restored);
                ProcessUtils.SetForegroundWindow(hWnd);
            }

            process.Kill();
            return false;
        }

        MigrateSettings();
        _ = RestoreLegacyMsiClawKeyboardSetting(null, out restartRequiredAfterMsiClawMigration);
        return true;
    }

    /// <summary>
    /// Resolves the application culture, applies it to <see cref="TranslationSource"/>,
    /// and registers all unhandled-exception handlers.
    /// </summary>
    private void SetupEnvironment()
    {
        string currentCultureString = ManagerFactory.settingsManager.GetString("CurrentCulture");
        CultureInfo culture = string.IsNullOrEmpty(currentCultureString)
            ? CultureInfo.CurrentCulture
            : new CultureInfo(currentCultureString);

        while (culture is not null)
        {
            if (TranslationSource.ValidCultures.Contains(culture)) break;
            if (culture.Equals(CultureInfo.InvariantCulture) || culture.Equals(culture.Parent)) break;
            culture = culture.Parent;
        }

        if (culture is null || !TranslationSource.ValidCultures.Contains(culture))
            culture = new CultureInfo("en-US");

        TranslationSource.Instance.CurrentCulture = culture;

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        System.Windows.Forms.Application.ThreadException += Application_ThreadException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    /// <summary>
    /// Detects the device, pulls sensors, and starts the core managers that must be ready
    /// before <see cref="WelcomeWindow"/> or <see cref="Views.MainWindow"/> are constructed.
    /// Must be called from the main UI thread.
    /// </summary>
    private static IDevice InitializeDevice(SplashScreenHost splash)
    {
        // UISettings is a Windows Runtime object — must be created on the UI thread.
        uiSettings = new UISettings();

        splash.SetStatus("Loading device information...");
        MotherboardInfo.Collect();

        splash.SetStatus("Detecting device...");
        IDevice device = IDevice.GetCurrent();

        splash.SetStatus("Detecting sensors...");
        device.PullSensors();

        // InputsManager installs WH_KEYBOARD_LL / WH_MOUSE_LL via Hook.GlobalEvents().
        // Low-level hooks require a Win32 message loop on the calling thread — UI thread only.
        splash.SetStatus("Starting core managers...");
        InputsManager.Start();
        TimerManager.Start();
        MotionManager.Start();
        ManagerFactory.settingsManager.Start();

        return device;
    }

    /// <summary>
    /// Runs on a background thread: device hardware init and all manager starts.
    /// The device singleton and pages are already constructed before this is called.
    /// </summary>
    public static void StartNonUIInit(string exePath, bool firstStart, bool newUpdate, Action<string>? reportStatus = null)
    {
        reportStatus?.Invoke("Initializing hardware...");
        IDevice.GetCurrent().Initialize(firstStart, newUpdate);

        // start non-static managers
        // todo: make them non-static
        reportStatus?.Invoke("Loading managers...");
        foreach (IManager manager in ManagerFactory.Managers)
            Task.Run(() => manager.Start());

        // start static managers
        Task.Run(() => OSDManager.Start());
        Task.Run(() => SystemManager.Start());
        Task.Run(() => DynamicLightingManager.Start());
        Task.Run(() => VirtualManager.Start());
        Task.Run(() => SensorsManager.Start());
        Task.Run(() => ControllerManager.Start());
        Task.Run(() => TaskManager.Start(exePath));
        Task.Run(() => PerformanceManager.Start());
        Task.Run(() => UpdateManager.Start());
    }

    private void RunUninstallRestoreMode()
    {
        ShutdownMode previousShutdownMode = ShutdownMode;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        using SplashScreenHost splashScreen = new();

        try
        {
            LogManager.LogInformation("Starting uninstall restore mode");

            splashScreen.Show();
            splashScreen.SetStatus("Preparing uninstall restore...");

            bool success = RestoreLegacyMsiClawKeyboardSetting(splashScreen.SetStatus, out _);
            success &= ControllerManager.RestoreAllControllersForUninstall(splashScreen.SetStatus);
            success &= RestoreOemSoftwareStack(splashScreen.SetStatus);
            success &= RestoreControllerMode(splashScreen.SetStatus);

            Shutdown(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            LogManager.LogCritical("Uninstall restore failed: {0}\t{1}", ex.Message, ex.StackTrace ?? string.Empty);
            splashScreen.SetStatus("Restore failed.");
            Shutdown(1);
        }
        finally
        {
            splashScreen.Close();
            ShutdownMode = previousShutdownMode;
        }
    }

    private static bool RestoreOemSoftwareStack(Action<string>? reportStatus)
    {
        reportStatus?.Invoke("Restoring OEM software stack...");

        try
        {
            using ISpaceWatcher? manufacturerWatcher = ISpaceWatcher.CreateCurrent();
            if (manufacturerWatcher is null)
            {
                LogManager.LogInformation("No OEM software watcher available for the current device");
                return true;
            }

            manufacturerWatcher.Enable();
            LogManager.LogInformation("OEM software stack restored through {0}", manufacturerWatcher.GetType().Name);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to restore OEM software stack: {0}", ex.Message);
            return false;
        }
    }

    private static bool RestoreControllerMode(Action<string>? reportStatus)
    {
        reportStatus?.Invoke("Restoring controller mode...");

        try
        {
            IDevice device = IDevice.GetCurrent();
            if (device is ClawA1M claw)
            {
                bool success = claw.SwitchToXInput();
                if (success)
                {
                    LogManager.LogInformation("Controller mode switched back to XInput");
                    return true;
                }
                else
                {
                    LogManager.LogError("Failed to switch controller mode back to XInput");
                    return false;
                }
            }

            LogManager.LogInformation("No controller mode available for the current device");
            return true;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to restore controller mode: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Restores the PS/2 keyboard driver disabled by the legacy MSI Claw setting and migrates
    /// that setting to the Win+G firmware workaround.
    /// </summary>
    /// <param name="reportStatus">Optional callback used to report migration progress.</param>
    /// <param name="restartRequired">True when restoring the driver requires Windows to restart.</param>
    /// <returns>True when no migration is needed or the driver and settings were restored successfully.</returns>
    private static bool RestoreLegacyMsiClawKeyboardSetting(Action<string>? reportStatus, out bool restartRequired)
    {
        restartRequired = false;

        // Uninstall restore intentionally avoids constructing the full ManagerFactory graph.
        if (!ApplicationSettings.Default.DisableMsiClawPS2Service)
            return true;

        reportStatus?.Invoke("Restoring MSI Claw keyboard driver...");

        try
        {
            using ServiceController service = new("i8042prt");
            if (!ServiceUtils.ChangeStartMode(service, ServiceStartMode.System, out string error))
            {
                // Keep the legacy setting enabled so normal startup or uninstall can retry.
                LogManager.LogError("Failed to restore {0} startup mode while migrating the MSI Claw hotkey setting: {1}",
                    service.ServiceName, error);
                return false;
            }

            ApplicationSettings.Default.BlockMsiClawWinGHotkey = true;
            ApplicationSettings.Default.DisableMsiClawPS2Service = false;
            ApplicationSettings.Default.Save();

            restartRequired = true;
            LogManager.LogInformation("Restored {0} startup mode and migrated the MSI Claw hotkey setting", service.ServiceName);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to restore {0} startup mode while migrating the MSI Claw hotkey setting: {1}",
                "i8042prt", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Schedules the migration restart prompt after the main window has finished loading.
    /// </summary>
    private static void RequestRestartConfirmation()
    {
        Current.Dispatcher.BeginInvoke(RequestRestartConfirmationCore, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Displays the migration restart prompt and restarts Windows when the user confirms.
    /// </summary>
    private static void RequestRestartConfirmationCore()
    {
        _ = UIHelper.TryInvoke(async () =>
        {
            ContentDialogResult result = await new Dialog(HandheldCompanion.Views.MainWindow.GetCurrent())
            {
                Title = HandheldCompanion.Properties.Resources.Dialog_ForceRestartTitle,
                Content = HandheldCompanion.Properties.Resources.Dialog_ForceRestartDesc,
                DefaultButton = ContentDialogButton.Close,
                CloseButtonText = HandheldCompanion.Properties.Resources.Dialog_No,
                PrimaryButtonText = HandheldCompanion.Properties.Resources.Dialog_Yes
            }.ShowAsync();

            if (result == ContentDialogResult.Primary)
                DeviceUtils.RestartComputer();
        });
    }

    private void MigrateSettings()
    {
        // one-time migration
        string myDocumentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ApplicationName);
        bool settingsExists = Directory.Exists(SettingsPath);
        bool docsExists = Directory.Exists(myDocumentsPath);

        // both exist, compare by latest user.config write time (fallback to dir time)
        if (settingsExists && docsExists)
        {
            DateTime settingsTime = GetLatestUserConfigWriteUtc(SettingsPath);
            DateTime docsTime = GetLatestUserConfigWriteUtc(myDocumentsPath);

            if (settingsTime < docsTime)
            {
                MessageBoxResult messageResult = MessageBox.Show(
                    $"Newer settings were found in {myDocumentsPath}.\n" +
                    $"Merge them into the current settings?",
                    ApplicationName, MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (messageResult == MessageBoxResult.Yes)
                    MergeCopyPreservingReserved(myDocumentsPath, SettingsPath, true, true);
            }
        }
        // SettingsPath missing, docs exist, move if possible, else copy
        else if (!settingsExists && docsExists)
        {
            try
            {
                // Fast path: if settings folder doesn't exist, we can safely move everything
                Directory.Move(myDocumentsPath, SettingsPath);
            }
            catch
            {
                // Fallback to copy; no reserved files exist yet in a new SettingsPath anyway
                MergeCopyPreservingReserved(myDocumentsPath, SettingsPath, true, true);
            }
        }

        // none exist or failed to move, ensure SettingsPath exists
        if (!Directory.Exists(SettingsPath))
        {
            try { Directory.CreateDirectory(SettingsPath); } catch { /* ignore */ }
        }
    }

    private void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        Exception ex = e.Exception;

        // send to sentry
        bool IsSentryEnabled = ManagerFactory.settingsManager.GetBoolean("TelemetryEnabled");
        if (SentrySdk.IsEnabled && IsSentryEnabled)
            SentrySdk.CaptureException(ex);

        if (ex.InnerException != null)
            LogManager.LogCritical(ex.InnerException.Message + "\t" + ex.InnerException.StackTrace);

        LogManager.LogCritical(ex.Message + "\t" + ex.StackTrace);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = (Exception)e.ExceptionObject;

        // send to sentry
        bool IsSentryEnabled = ManagerFactory.settingsManager.GetBoolean("TelemetryEnabled");
        if (SentrySdk.IsEnabled && IsSentryEnabled)
            SentrySdk.CaptureException(ex);

        if (ex.InnerException != null)
            LogManager.LogCritical(ex.InnerException.Message + "\t" + ex.InnerException.StackTrace);

        LogManager.LogCritical(ex.Message + "\t" + ex.StackTrace);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Exception ex = e.Exception;

        // dirty: filter ItemsRepeater DesiredSize is NaN
        if (ex.Message.Contains("ItemsRepeater"))
            goto Handled;

        // send to sentry
        bool IsSentryEnabled = ManagerFactory.settingsManager.GetBoolean("TelemetryEnabled");
        if (SentrySdk.IsEnabled && IsSentryEnabled)
            SentrySdk.CaptureException(ex);

        if (ex.InnerException != null)
            LogManager.LogCritical(ex.InnerException.Message + "\t" + ex.InnerException.StackTrace);
        else
            LogManager.LogCritical(ex.Message + "\t" + ex.StackTrace);

        // If you want to avoid the application from crashing:
    Handled:
        e.Handled = true;
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Exception ex = e.Exception;

        // send to sentry
        bool IsSentryEnabled = ManagerFactory.settingsManager.GetBoolean("TelemetryEnabled");
        if (SentrySdk.IsEnabled && IsSentryEnabled)
            SentrySdk.CaptureException(ex);

        // Log all inner exceptions from the AggregateException
        if (ex is AggregateException aggEx)
        {
            foreach (var innerEx in aggEx.InnerExceptions)
            {
                LogManager.LogCritical("Unobserved Task Exception: {0}\t{1}", innerEx.Message, innerEx.StackTrace ?? string.Empty);
            }
        }
        else
        {
            LogManager.LogCritical("Unobserved Task Exception: {0}\t{1}", ex.Message, ex.StackTrace ?? string.Empty);
        }

        // Mark the exception as observed to prevent app crash
        e.SetObserved();
    }

    private void InitializeSentry()
    {
        string url = SecretKeys.DSN_URL;

        if (!string.IsNullOrEmpty(url))
        {
            SentrySdk.Init(options =>
            {
                // Tells which project in Sentry to send events to:
                options.Dsn = url;

#if DEBUG
                // When configuring for the first time, to see what the SDK is doing:
                options.Debug = true;
#else
                options.Debug = false;
#endif

                // Enable Global Mode since this is a client app
                options.IsGlobalModeEnabled = true;
            });
        }
    }
}
