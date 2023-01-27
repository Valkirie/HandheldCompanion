using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages;
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

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Interaction logic for ButtonMapping.xaml
    /// </summary>
    public partial class ButtonMapping : UserControl
    {
        private ButtonFlags Button;
        private IActions Actions;

        public ButtonMapping()
        {
            InitializeComponent();
        }

        public ButtonMapping(ButtonFlags button) : this()
        {
            this.Button = button;
            this.Icon.Glyph = button.ToString();
        }

        public void UpdateIcon(FontIcon newIcon)
        {
            this.Icon.Glyph = newIcon.Glyph;
            this.Icon.FontFamily = newIcon.FontFamily;
            this.Icon.FontSize = newIcon.FontSize;
            this.Icon.Foreground = newIcon.Foreground;
        }

        internal void SetIActions(IActions actions)
        {
            // update mapping IActions
            this.Actions = actions;

            // update UI
            this.ActionComboBox.SelectedIndex = (int)actions.ActionType;
        }

        private void Action_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // clear current dropdown values
            TargetComboBox.Items.Clear();

            // get current controller
            IController controller = ControllerManager.GetTargetController();

            // populate target dropdown based on action type
            ActionType type = (ActionType)ActionComboBox.SelectedIndex;
            switch (type)
            {
                case ActionType.None:
                    ProfilesPage.currentProfile.ButtonMapping.Remove(Button);
                    return;

                case ActionType.Button:
                    {
                        if (this.Actions is null)
                            this.Actions = new ButtonActions();

                        // we need a controller to get compatible buttons
                        if (controller is null)
                            return;

                        foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
                        {
                            if (controller.IsButtonSupported(button))
                            {
                                // create a label, store ButtonFlags as Tag and Label as controller specific string
                                Label buttonLabel = new Label() { Tag =  button, Content = controller.GetButtonName(button) };
                                TargetComboBox.Items.Add(buttonLabel);

                                if (button.Equals(((ButtonActions)this.Actions).Button))
                                    TargetComboBox.SelectedItem = buttonLabel;
                            }
                        }
                    }
                    break;
            }
        }

        private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // generate IActions based on settings
            ActionType type = (ActionType)ActionComboBox.SelectedIndex;
            switch (type)
            {
                case ActionType.None:
                    break;

                case ActionType.Button:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((ButtonActions)this.Actions).Button = (ButtonFlags)buttonLabel.Tag;
                    }
                    break;
            }

            // update profile button mapping
            ProfilesPage.currentProfile.ButtonMapping[Button] = this.Actions;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
