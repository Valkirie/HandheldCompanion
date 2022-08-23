using ControllerCommon;
using ControllerCommon.Managers;
using HandheldCompanion.Views;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;

namespace HandheldCompanion
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static MainWindow m_MainWindow;
        static PipeClient m_PipeClient;
        static StartupEventArgs m_Arguments;

        static Mutex mutex = new Mutex(true, "HandheldCompanion");
        static AutoResetEvent autoEvent = new AutoResetEvent(false);

        [STAThread]
        private void Main(object sender, StartupEventArgs Arguments)
        {
            string CurrentCulture = HandheldCompanion.Properties.Settings.Default.CurrentCulture;
            CultureInfo culture = CultureInfo.CurrentCulture;

            switch (CurrentCulture)
            {
                default:
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

            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

                // initialize log manager
                LogManager.Initialize("HandheldCompanion");

                MainWindow wnd = new MainWindow(Arguments);
                wnd.Show();

                mutex.ReleaseMutex();
            }
            else
            {
                m_Arguments = Arguments;

                m_PipeClient = new PipeClient("HandheldCompanion");
                m_PipeClient.Connected += OnServerConnected;
                m_PipeClient.ServerMessage += OnServerMessage;
                m_PipeClient.Open();

                // Wait for work method to signal and kill after 4seconds
                autoEvent.WaitOne(4000);
                Application.Current.Shutdown();
            }
        }

        private static void OnServerMessage(object sender, PipeMessage e)
        {
            switch (e.code)
            {
                case PipeCode.FORCE_SHUTDOWN:
                    autoEvent.Set();
                    break;
            }
        }

        private static void OnServerConnected(object sender)
        {
            m_PipeClient.SendMessage(new PipeConsoleArgs() { args = m_Arguments.Args });
        }
    }
}
