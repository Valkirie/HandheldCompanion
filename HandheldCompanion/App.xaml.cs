using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace HandheldCompanion
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnStartup(StartupEventArgs args)
        {
            // get current assembly
            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);

            // initialize log
            LogManager.Initialize("HandheldCompanion");
            LogManager.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

            string proc = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(proc);

            if (processes.Length > 1)
            {
                Process process = processes[0];

                IntPtr handle = process.MainWindowHandle;
                if (ProcessUtils.IsIconic(handle))
                    ProcessUtils.ShowWindow(handle, ProcessUtils.SW_RESTORE);

                ProcessUtils.SetForegroundWindow(handle);

                // force close this iteration
                Process.GetCurrentProcess().Kill();
                return;
            }

            // define culture settings
            string CurrentCulture = SettingsManager.GetString("CurrentCulture");
            CultureInfo culture = CultureInfo.CurrentCulture;

            switch (CurrentCulture)
            {
                default:
                    culture = new CultureInfo("en-US");
                    break;
                case "fr-FR":
                case "en-US":
                case "zh-CN":
                case "zh-Hant":
                    culture = new CultureInfo(CurrentCulture);
                    break;
            }

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            MainWindow = new MainWindow(fileVersionInfo, CurrentAssembly);
            MainWindow.Show();
            MainWindow.Activate();
        }
    }
}
