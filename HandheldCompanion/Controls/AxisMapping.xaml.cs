using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Pages;
using LiveCharts.Wpf;
using ModernWpf.Controls;
using SharpDX.DirectInput;
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
using static HandheldCompanion.Simulators.MouseSimulator;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Interaction logic for AxisMapping.xaml
    /// </summary>
    public partial class AxisMapping : UserControl
    {
        private AxisFlags Axis;
        private IActions Actions;

        #region events
        public event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(AxisFlags axis);
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(AxisFlags axis, IActions action);
        #endregion

        public AxisMapping()
        {
            InitializeComponent();
        }

        public AxisMapping(AxisFlags axis) : this()
        {
            this.Axis = axis;
            this.Icon.Glyph = axis.ToString();
        }

        public void UpdateIcon(FontIcon newIcon)
        {
            this.Icon.Glyph = newIcon.Glyph;
            this.Icon.FontFamily = newIcon.FontFamily;
            this.Icon.FontSize = newIcon.FontSize;

            if (newIcon.Foreground is not null)
                this.Icon.Foreground = newIcon.Foreground;
            else
                this.Icon.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
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
            if (ActionComboBox.SelectedItem is null)
                return;

            // we're not ready yet
            if (TargetComboBox is null)
                return;

            // clear current dropdown values
            TargetComboBox.Items.Clear();
            TargetComboBox.IsEnabled = ActionComboBox.SelectedIndex != 0;

            // get current controller
            IController controller = ControllerManager.GetTargetController();

            // populate target dropdown based on action type
            ActionType type = (ActionType)ActionComboBox.SelectedIndex;

            if (type == ActionType.None)
            {
                Deleted?.Invoke(Axis);
            }
            else if (type == ActionType.Button)
            {
            }
            else if (type == ActionType.Axis)
            {
                if (this.Actions is null || this.Actions is not AxisActions)
                    this.Actions = new AxisActions();

                // we need a controller to get compatible buttons
                if (controller is null)
                    return;

                foreach (AxisFlags axis in Enum.GetValues(typeof(AxisFlags)))
                {
                    if (controller.IsAxisSupported(axis))
                    {
                        // create a label, store ButtonFlags as Tag and Label as controller specific string
                        Label buttonLabel = new Label() { Tag = axis, Content = controller.GetAxisName(axis) };
                        TargetComboBox.Items.Add(buttonLabel);

                        if (axis.Equals(((AxisActions)this.Actions).Axis))
                            TargetComboBox.SelectedItem = buttonLabel;
                    }
                }

                // settings
                Axis_Invert.IsOn = ((AxisActions)this.Actions).AxisInverted;
            }
            else if (type == ActionType.Keyboard)
            {
            }
            else if (type == ActionType.Mouse)
            {
                if (this.Actions is null || this.Actions is not MouseActions)
                    this.Actions = new MouseActions();

                foreach (MouseActionsType mouseType in Enum.GetValues(typeof(MouseActionsType)))
                {
                    // skip axis related actions
                    if (mouseType <= MouseActionsType.MiddleButton)
                        continue;

                    // localize me ?
                    TargetComboBox.Items.Add(mouseType);
                }

                TargetComboBox.SelectedItem = ((MouseActions)this.Actions).MouseType;
            }
        }

        private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetComboBox.SelectedItem is null)
                return;

            // generate IActions based on settings
            switch (this.Actions.ActionType)
            {
                case ActionType.None:
                    break;

                case ActionType.Button:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((ButtonActions)this.Actions).Button = (ButtonFlags)buttonLabel.Tag;
                    }
                    break;

                case ActionType.Axis:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((AxisActions)this.Actions).Axis = (AxisFlags)buttonLabel.Tag;
                    }
                    break;

                case ActionType.Keyboard:
                    {
                        ((KeyboardActions)this.Actions).Key = (VirtualKeyCode)TargetComboBox.SelectedItem;
                    }
                    break;

                case ActionType.Mouse:
                    {
                        ((MouseActions)this.Actions).MouseType = (MouseActionsType)TargetComboBox.SelectedItem;
                    }
                    break;
            }

            // update button mapping
            Updated?.Invoke(Axis, Actions);
        }

        public void Update()
        {
            // force full update
            Action_SelectionChanged(null, null);
            Target_SelectionChanged(null, null);
        }

        public void Reset()
        {
            ActionComboBox.SelectedItem = null;
            TargetComboBox.SelectedItem = null;
        }

        private void Axis_Invert_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Axis:
                    ((AxisActions)this.Actions).AxisInverted = Axis_Invert.IsOn;
                    break;
            }
        }
    }
}
