using ControllerCommon;
using ControllerCommon.Utils;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
            TriggerUpdated("overlayGamepad", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.OverlayControllerTriggerType, Properties.Settings.Default.OverlayControllerTriggerValue));
            TriggerUpdated("overlayTrackpads", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.OverlayTrackpadsTriggerType, Properties.Settings.Default.OverlayTrackpadsTriggerValue));
            TriggerUpdated("suspender", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.SuspenderTriggerType, Properties.Settings.Default.SuspenderTriggerValue));
            TriggerUpdated("quickTools", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.QuickToolsTriggerType, Properties.Settings.Default.QuickToolsTriggerValue));
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

        private void SuspenderTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            TriggerButton_Click("suspender", sender);
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

                        Properties.Settings.Default.OverlayControllerTriggerValue = input.GetValue();
                        Properties.Settings.Default.OverlayControllerTriggerType = (int)input.type;
                        break;
                    case "overlayTrackpads":
                        TrackpadsTriggerText.Text = text;
                        TrackpadsTriggerIcon.Glyph = glyph;
                        TrackpadsTriggerButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;

                        Properties.Settings.Default.OverlayTrackpadsTriggerValue = input.GetValue();
                        Properties.Settings.Default.OverlayTrackpadsTriggerType = (int)input.type;
                        break;
                    case "suspender":
                        SuspenderTriggerText.Text = text;
                        SuspenderTriggerIcon.Glyph = glyph;
                        SuspenderTriggerButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;

                        Properties.Settings.Default.SuspenderTriggerValue = input.GetValue();
                        Properties.Settings.Default.SuspenderTriggerType = (int)input.type;
                        break;
                    case "quickTools":
                        QuickToolsTriggerText.Text = text;
                        QuickToolsTriggerIcon.Glyph = glyph;
                        QuicktoolsTriggerButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;

                        Properties.Settings.Default.QuickToolsTriggerValue = input.GetValue();
                        Properties.Settings.Default.QuickToolsTriggerType = (int)input.type;
                        break;
                }
            });

            Properties.Settings.Default.Save();
        }
    }
}
