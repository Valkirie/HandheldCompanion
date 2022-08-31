using ControllerCommon;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
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
        private bool Initialized;

        public HotkeysPage()
        {
            InitializeComponent();
            Initialized = true;
        }

        public HotkeysPage(string Tag) : this()
        {
            this.Tag = Tag;

            MainWindow.inputsManager.TriggerUpdated += TriggerUpdated;

            // trigger(s)
            TriggerUpdated("overlayGamepad", new TriggerInputs(
                (TriggerInputsType)SettingsManager.GetInt("OverlayControllerTriggerType"),
                SettingsManager.GetString("OverlayControllerTriggerValue")));
            
            TriggerUpdated("overlayTrackpads", new TriggerInputs(
                (TriggerInputsType)SettingsManager.GetInt("OverlayTrackpadsTriggerType"),
                SettingsManager.GetString("OverlayTrackpadsTriggerValue")));
            
            TriggerUpdated("quickTools", new TriggerInputs(
                (TriggerInputsType)SettingsManager.GetInt("QuickToolsTriggerType"),
                SettingsManager.GetString("QuickToolsTriggerValue")));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        private void ControllerTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            TriggerButton_Click("overlayGamepad", sender);
        }

        private void TrackpadsTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            TriggerButton_Click("overlayTrackpads", sender);
        }

        private void QuickToolsTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            TriggerButton_Click("quickTools", sender);
        }

        private void TriggerButton_Click(string listener, object sender)
        {
            MainWindow.inputsManager.StartListening(listener);

            Button button = (Button)sender;
            SimpleStackPanel stackpanel = (SimpleStackPanel)button.Content;
            TextBlock text = (TextBlock)stackpanel.Children[1];

            text.Text = Properties.Resources.OverlayPage_Listening;
            button.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
        }

        private void TriggerUpdated(string listener, TriggerInputs input)
        {
            this.Dispatcher.Invoke(() =>
            {
                string text = string.Empty;
                string glyph = InputUtils.TriggerTypeToGlyph(input.type);

                switch (input.type)
                {
                    default:
                    case TriggerInputsType.Gamepad:
                        text = EnumUtils.GetDescriptionFromEnumValue(input.buttons);
                        break;
                    case TriggerInputsType.Keyboard:
                        // todo, display custom button name instead
                        text = string.Join(", ", input.name);
                        break;
                }

                switch (listener)
                {
                    case "overlayGamepad":
                        ControllerTriggerText.Text = text;
                        ControllerTriggerIcon.Glyph = glyph;
                        ControllerTriggerButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;

                        SettingsManager.SetProperty("OverlayControllerTriggerValue", input.GetValue());
                        SettingsManager.SetProperty("OverlayControllerTriggerType", (int)input.type);
                        break;
                    case "overlayTrackpads":
                        TrackpadsTriggerText.Text = text;
                        TrackpadsTriggerIcon.Glyph = glyph;
                        TrackpadsTriggerButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;

                        SettingsManager.SetProperty("OverlayTrackpadsTriggerValue", input.GetValue());
                        SettingsManager.SetProperty("OverlayTrackpadsTriggerType", (int)input.type);
                        break;
                    case "quickTools":
                        QuickToolsTriggerText.Text = text;
                        QuickToolsTriggerIcon.Glyph = glyph;
                        QuicktoolsTriggerButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;

                        SettingsManager.SetProperty("QuickToolsTriggerValue", input.GetValue());
                        SettingsManager.SetProperty("QuickToolsTriggerType", (int)input.type);
                        break;
                }
            });
        }
    }
}
