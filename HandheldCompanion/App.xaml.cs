using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using System.Globalization;
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

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            MainWindow = new MainWindow();
            MainWindow.Show();
            MainWindow.Activate();
        }
    }
}
