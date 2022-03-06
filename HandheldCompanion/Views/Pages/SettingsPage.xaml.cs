using ControllerCommon;
using Microsoft.Extensions.Logging;
using ModernWpf;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private MainWindow mainWindow;
        private ILogger microsoftLogger;
        private PipeClient pipeClient;

        // settings vars
        public bool s_ToastEnable, s_RunAtStartup, s_StartMinimized, s_CloseMinimises;
        public int s_ApplicationTheme;

        public event ToastChangedEventHandler ToastChanged;
        public delegate void ToastChangedEventHandler(bool value);

        public event AutoStartChangedEventHandler AutoStartChanged;
        public delegate void AutoStartChangedEventHandler(bool value);

        private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
        }

        public SettingsPage()
        {
            InitializeComponent();

            Toggle_AutoStart.IsOn = s_RunAtStartup = Properties.Settings.Default.RunAtStartup;
            Toggle_Background.IsOn = s_StartMinimized = Properties.Settings.Default.StartMinimized;
            Toggle_CloseMinimizes.IsOn = s_CloseMinimises = Properties.Settings.Default.CloseMinimises;

            cB_Theme.SelectedIndex = s_ApplicationTheme = Properties.Settings.Default.MainWindowTheme;

            Toggle_Notification.IsOn = s_ToastEnable = Properties.Settings.Default.ToastEnable;
        }

        public SettingsPage(string Tag, MainWindow mainWindow, ILogger microsoftLogger) : this()
        {
            this.Tag = Tag;
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = mainWindow.pipeClient;
        }

        private void Toggle_AutoStart_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.RunAtStartup = Toggle_AutoStart.IsOn;
            Properties.Settings.Default.Save();

            s_RunAtStartup = Toggle_AutoStart.IsOn;
            AutoStartChanged?.Invoke(s_RunAtStartup);
        }

        private void Toggle_Background_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.StartMinimized = Toggle_Background.IsOn;
            Properties.Settings.Default.Save();

            s_StartMinimized = Toggle_Background.IsOn;
        }

        private void Toggle_CloseMinimizes_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.CloseMinimises = Toggle_CloseMinimizes.IsOn;
            Properties.Settings.Default.Save();

            s_CloseMinimises = Toggle_CloseMinimizes.IsOn;
        }

        private void Toggle_Notification_Toggled(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.ToastEnable = Toggle_Notification.IsOn;
            Properties.Settings.Default.Save();

            s_ToastEnable = Toggle_Notification.IsOn;
            ToastChanged?.Invoke(s_ToastEnable);
        }

        private void cB_Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.MainWindowTheme = cB_Theme.SelectedIndex;
            Properties.Settings.Default.Save();

            ApplyTheme((ApplicationTheme)cB_Theme.SelectedIndex);
        }

        public void ApplyTheme(ApplicationTheme Theme)
        {
            ThemeManager.Current.ApplicationTheme = Theme;
        }
    }
}
