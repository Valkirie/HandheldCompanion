using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Controllers;
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

        public ButtonMapping()
        {
            InitializeComponent();

            ActionType type = (ActionType)Action.SelectedIndex;

            // clear current dropdown values
            Target.Items.Clear();

            // populate target dropdown based on action type
            switch (type)
            {
                case ActionType.Button:
                    break;
            }
        }

        public ButtonMapping(ButtonFlags button) : this()
        {
            this.Button = button;
        }

        internal void SetController(IController controller)
        {
            // update Icon on controller changes
            var newIcon = controller.GetFontIcon(Button);

            // unsupported button
            if (newIcon is null)
            {
                this.Visibility = Visibility.Collapsed;
                return;
            }

            this.Icon.Glyph = newIcon.Glyph;
            this.Icon.FontFamily = newIcon.FontFamily;
            this.Icon.Foreground = newIcon.Foreground;
        }

        private void Action_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}
