using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using static HandheldCompanion.Simulators.MouseSimulator;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Interaction logic for AxisMapping.xaml
    /// </summary>
    public partial class AxisMapping : UserControl
    {
        private AxisLayoutFlags Axis;
        private IActions Actions;

        #region events
        public event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(AxisLayoutFlags axis);
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(AxisLayoutFlags axis, IActions action);
        #endregion

        public AxisMapping()
        {
            InitializeComponent();
        }

        public AxisMapping(AxisLayoutFlags axis) : this()
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

                foreach (AxisLayoutFlags axis in Enum.GetValues(typeof(AxisLayoutFlags)))
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
                Axis_InnerDeadzone_Slider.Value = ((AxisActions)this.Actions).AxisDeadZoneInner;
                Axis_OuterDeadzone_Slider.Value = ((AxisActions)this.Actions).AxisDeadZoneOuter;
                Axis_AntiDeadZone_Slider.Value = ((AxisActions)this.Actions).AxisAntiDeadZone;
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
                    // skip button related actions
                    if (mouseType <= MouseActionsType.MiddleButton)
                        continue;

                    // localize me ?
                    TargetComboBox.Items.Add(mouseType);
                }

                TargetComboBox.SelectedItem = ((MouseActions)this.Actions).MouseType;
            }

            // update button mapping
            Updated?.Invoke(Axis, Actions);
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
                        ((AxisActions)this.Actions).Axis = (AxisLayoutFlags)buttonLabel.Tag;
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

        private void Axis_InnerDeadzone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Axis:
                    ((AxisActions)this.Actions).AxisDeadZoneInner = (int)Axis_InnerDeadzone_Slider.Value;
                    break;
            }
        }

        private void Axis_OuterDeadzone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Axis:
                    ((AxisActions)this.Actions).AxisDeadZoneOuter = (int)Axis_OuterDeadzone_Slider.Value;
                    break;
            }
        }

        private void Axis_AntiDeadZone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Axis:
                    ((AxisActions)this.Actions).AxisAntiDeadZone = (int)Axis_AntiDeadZone_Slider.Value;
                    break;
            }
        }
    }
}
