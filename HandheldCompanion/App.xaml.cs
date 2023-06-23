using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using static ControllerCommon.WinAPI;

namespace HandheldCompanion;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    ///     Initializes the singleton application object.  This is the first line of authored code
    ///     executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
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

        // initialize log
        LogManager.Initialize("HandheldCompanion");
        LogManager.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

        using (var process = Process.GetCurrentProcess())
        {
            // force high priority
            SetPriorityClass(process.Handle, (int)PriorityClass.HIGH_PRIORITY_CLASS);

            var processes = Process.GetProcessesByName(process.ProcessName);
            if (processes.Length > 1)
                using (var prevProcess = processes[0])
                {
                    var handle = prevProcess.MainWindowHandle;
                    if (ProcessUtils.IsIconic(handle))
                        ProcessUtils.ShowWindow(handle, (int)ProcessUtils.ShowWindowCommands.Restored);

                    ProcessUtils.SetForegroundWindow(handle);

                    // force close this iteration
                    process.Kill();
                    return;
                }
        }

        // define culture settings
        var CurrentCulture = SettingsManager.GetString("CurrentCulture");
        var culture = CultureInfo.CurrentCulture;

        switch (CurrentCulture)
        {
            default:
                culture = new CultureInfo("en-US");
                break;
            case "fr-FR":
            case "en-US":
            case "zh-CN":
            case "zh-Hant":
            case "de-DE":
            case "ja-JP":
                culture = new CultureInfo(CurrentCulture);
                break;
        }

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // handle exceptions nicely
        var currentDomain = default(AppDomain);
        currentDomain = AppDomain.CurrentDomain;
        // Handler for unhandled exceptions.
        currentDomain.UnhandledException += CurrentDomain_UnhandledException;
        // Handler for exceptions in threads behind forms.
        System.Windows.Forms.Application.ThreadException += Application_ThreadException;

        MainWindow = new MainWindow(fileVersionInfo, CurrentAssembly);
        MainWindow.Show();
        MainWindow.Activate();
    }

    private void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        var ex = default(Exception);
        ex = (Exception)e.Exception;
        LogManager.LogCritical(ex.Message + "\t" + ex.StackTrace);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = default(Exception);
        ex = (Exception)e.ExceptionObject;
        LogManager.LogCritical(ex.Message + "\t" + ex.StackTrace);
    }
}