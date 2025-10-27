using HandheldCompanion.Localization;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Sentry;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
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
    public static string ApplicationName
    {
        get => Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
        set { /* noop */ }
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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

        InjectResource();

        InitializeComponent();

        // initialize path(s)
        InstallPath = AppDomain.CurrentDomain.BaseDirectory;
        SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationName);
        LogsPath = Path.Combine(SettingsPath, "logs");

#if DEBUG
        if (!ManagerFactory.settingsManager.GetBoolean("MuteConsole"))
            AllocConsole();
#endif
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
        var customManager = new ResilientResourceManager(resourcesType.FullName, resourcesType.Assembly);
        FieldInfo? field = resourcesType.GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null) return;
        field.SetValue(null, customManager);
    }

    /// <summary>
    ///     Invoked when the application is launched normally by the end user.  Other entry points
    ///     will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnStartup(StartupEventArgs args)
    {
        // get current assembly
        Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
        FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);

        // Set environment variables
        Environment.SetEnvironmentVariable("LOG_PATH", LogsPath);
        Environment.SetEnvironmentVariable("COMPlus_legacyCorruptedStateExceptionsPolicy", "1");

        // initialize log
        LogManager.Initialize(ApplicationName);
        LogManager.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

        using (Process process = Process.GetCurrentProcess())
        {
            Process[] processes = Process.GetProcessesByName(process.ProcessName);
            if (processes.Length > 1)
            {
                using (Process prevProcess = processes[0])
                {
                    // Find the window by its title
                    IntPtr hWnd = FindWindow(null, $"Handheld Companion ({fileVersionInfo.FileVersion})");
                    if (hWnd == IntPtr.Zero || !prevProcess.Responding)
                    {
                        MessageBox.Show("Another instance of Handheld Companion is already running.\n\nPlease close the other instance and try again.", "Error");
                        process.Kill();
                        return;
                    }

                    // Bring previous window to foreground, kill self
                    ProcessUtils.ShowWindow(hWnd, (int)ProcessUtils.ShowWindowCommands.Normal);
                    ProcessUtils.SetForegroundWindow(hWnd);
                    process.Kill();
                    return;
                }
            }
        }

        MigrateSettings();

        // define culture settings
        string currentCultureString = ManagerFactory.settingsManager.GetString("CurrentCulture");
        CultureInfo culture;
        if (string.IsNullOrEmpty(currentCultureString))
        {
            culture = CultureInfo.CurrentCulture;
        }
        else
        {
            culture = new CultureInfo(currentCultureString);
        }

        while (culture is not null)
        {
            if (TranslationSource.ValidCultures.Contains(culture)) break;

            // if we're already at the top of the chain, bail out
            if (culture.Equals(CultureInfo.InvariantCulture) || culture.Equals(culture.Parent))
                break;

            culture = culture.Parent;
        }

        if (culture is null || !TranslationSource.ValidCultures.Contains(culture))
            culture = new CultureInfo("en-US");

        TranslationSource.Instance.CurrentCulture = culture;

        // handle exceptions nicely
        var currentDomain = default(AppDomain);
        currentDomain = AppDomain.CurrentDomain;
        // Handler for unhandled exceptions.
        currentDomain.UnhandledException += CurrentDomain_UnhandledException;
        // Handler for exceptions in threads behind forms.
        System.Windows.Forms.Application.ThreadException += Application_ThreadException;
        // Handler for exceptions in dispatcher.
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        MainWindow = new MainWindow(fileVersionInfo, CurrentAssembly);
        MainWindow.Show();
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
