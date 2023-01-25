using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Views.Pages;
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

        internal void SetController(IController controller)
        {
            // update Icon on controller changes
            var newIcon = controller.GetFontIcon(Button);

            // unsupported button
            if (newIcon is null)
                return;

            // supported button
            this.Visibility = Visibility.Visible;

            this.Icon.Glyph = newIcon.Glyph;
            this.Icon.FontFamily = newIcon.FontFamily;
            this.Icon.Foreground = newIcon.Foreground;
        }

        internal void SetIActions(IActions actions)
        {
            this.Actions = actions;
            this.ActionComboBox.SelectedIndex = (int)actions.ActionType;
        }

        private void Action_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // clear current dropdown values
            TargetComboBox.Items.Clear();

            // populate target dropdown based on action type
            ActionType type = (ActionType)ActionComboBox.SelectedIndex;
            switch (type)
            {
                case ActionType.Button:
                    {
                        if (this.Actions is null)
                            this.Actions = new ButtonActions();

                        foreach (ButtonFlags mode in Enum.GetValues(typeof(ButtonFlags)))
                            TargetComboBox.Items.Add(mode);

                        TargetComboBox.SelectedItem = ((ButtonActions)this.Actions).Button;
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
                case ActionType.Button:
                    {
                        ButtonFlags button = (ButtonFlags)TargetComboBox.SelectedIndex;
                        ((ButtonActions)this.Actions).Button = button;

                        ProfilesPage.currentProfile.ButtonMapping[Button] = this.Actions;
                    }
                    break;
            }
        }
    }
}
