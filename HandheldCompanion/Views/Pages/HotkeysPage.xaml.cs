using ControllerCommon;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Classes;
using ModernWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for HotkeysPage.xaml
    /// </summary>
    public partial class HotkeysPage : Page
    {
        public HotkeysPage()
        {
            InitializeComponent();

            HotkeysManager.HotkeyCreated += TriggerCreated;
        }

        public HotkeysPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        private void TriggerButton_Click(string listener, Button sender)
        {
            InputsManager.StartListening(listener);

            // update button text
            sender.Content = Properties.Resources.OverlayPage_Listening;

            // update buton style
            sender.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
        }

        private void TriggerCreated(Hotkey hotkey)
        {
            this.Dispatcher.Invoke(() =>
            {
                string listener = hotkey.hotkey.GetListener();

                hotkey.DrawControl();
                var element = hotkey.GetBorder();

                Button button = hotkey.GetButton();
                button.Click += (sender, e) => TriggerButton_Click(listener, button);

                HotkeysPanel.Children.Add(element);
            });
        }
    }
}
