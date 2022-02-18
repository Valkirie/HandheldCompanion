using Microsoft.Extensions.Logging;
using System.Windows.Controls;

namespace ControllerHelperWPF.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private string Tag;
        private MainWindow mainWindow;
        private ILogger microsoftLogger;

        // settings vars
        public bool s_ToastEnable, s_RunAtStartup, s_StartMinimized, s_CloseMinimises;

        public event ToastEventHandler ToastChanged;
        public delegate void ToastEventHandler(object sender);

        public SettingsPage()
        {
            InitializeComponent();

            s_ToastEnable = Properties.Settings.Default.ToastEnable;
            s_RunAtStartup = Properties.Settings.Default.RunAtStartup;
            s_StartMinimized = Properties.Settings.Default.StartMinimized;
            s_CloseMinimises = Properties.Settings.Default.CloseMinimises;
        }

        public SettingsPage(string Tag, MainWindow mainWindow, ILogger microsoftLogger) : this()
        {
            this.Tag = Tag;
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;
        }
    }
}
