using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Sentry;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace HandheldCompanion;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static bool IsMultiThreaded { get; } = false;
    public static string InstallPath = string.Empty;
    public static string SettingsPath = string.Empty;

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();

    /// <summary>
    ///     Initializes the singleton application object.  This is the first line of authored code
    ///     executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeSentry();
        InitializeComponent();

#if DEBUG
        AllocConsole();
#endif

        // initialize path(s)
        InstallPath = AppDomain.CurrentDomain.BaseDirectory;
        SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion");
    }

    /// <summary>
    ///     Invoked when the application is launched normally by the end user.  Other entry points
    ///     will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnStartup(StartupEventArgs args)
    {
        // get current assembly
        var CurrentAssembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);

        // Get the MyDocuments folder path
        string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string logDirectory = System.IO.Path.Combine(myDocumentsPath, "HandheldCompanion", "logs");

        // Set environment variables
        Environment.SetEnvironmentVariable("LOG_PATH", logDirectory);
        Environment.SetEnvironmentVariable("COMPlus_legacyCorruptedStateExceptionsPolicy", "1");

        // initialize log
        LogManager.Initialize("HandheldCompanion");
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

        // define culture settings
        var CurrentCulture = ManagerFactory.settingsManager.GetString("CurrentCulture");
        var culture = CultureInfo.CurrentCulture;

        switch (CurrentCulture)
        {
            default:
                culture = new CultureInfo("en-US");
                break;
            case "fr-FR":
            case "en-US":
            case "zh-Hans":
            case "zh-Hant":
            case "de-DE":
            case "it-IT":
            case "pt-BR":
            case "es-ES":
            case "ja-JP":
            case "ru-RU":
            case "ko-KR":
                culture = new CultureInfo(CurrentCulture);
                break;
            case "zh-CN": // fallback change locale name from zh-CN to zh-Hans
                ManagerFactory.settingsManager.SetProperty("CurrentCulture", "zh-Hans", true);
                CurrentCulture = "zh-Hans";
                culture = new CultureInfo(CurrentCulture);
                break;
        }

        Localization.TranslationSource.Instance.CurrentCulture = culture;

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
        string url = SentryConfig.DSN_URL;

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
