using ControllerCommon.Actions;
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

        private void Action_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
