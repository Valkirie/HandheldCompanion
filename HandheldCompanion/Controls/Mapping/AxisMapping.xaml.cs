using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Interaction logic for AxisMapping.xaml
    /// </summary>
    public partial class AxisMapping : IMapping
    {
        public AxisMapping()
        {
            InitializeComponent();
        }

        public AxisMapping(AxisLayoutFlags axis) : this()
        {
            this.Value = axis;
            this.prevValue = axis;

            this.Icon.Glyph = axis.ToString();
        }

        public void UpdateIcon(FontIcon newIcon, string newLabel)
        {
            this.Name.Text = newLabel;

            this.Icon.Glyph = newIcon.Glyph;
            this.Icon.FontFamily = newIcon.FontFamily;
            this.Icon.FontSize = newIcon.FontSize;

            if (newIcon.Foreground is not null)
                this.Icon.Foreground = newIcon.Foreground;
            else
                this.Icon.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

            this.Update();
        }

        internal void SetIActions(IActions actions)
        {
            // reset and update mapping IActions
            Reset();
            base.SetIActions(actions);

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

            // we're busy
            if (!Monitor.TryEnter(updateLock))
                return;

            // clear current dropdown values
            TargetComboBox.Items.Clear();
            TargetComboBox.IsEnabled = ActionComboBox.SelectedIndex != 0;

            // get current controller
            IController controller = ControllerManager.GetEmulatedController();

            // populate target dropdown based on action type
            ActionType type = (ActionType)ActionComboBox.SelectedIndex;

            if (type == ActionType.Disabled)
            {
                if (this.Actions is not null)
                    base.Delete();
                return;
            }

            if (type == ActionType.Joystick)
            {
                if (this.Actions is null || this.Actions is not AxisActions)
                    this.Actions = new AxisActions();

                // we need a controller to get compatible buttons
                if (controller is null)
                    return;

                foreach (AxisLayoutFlags axis in controller.GetAxis())
                {
                    // create a label, store ButtonFlags as Tag and Label as controller specific string
                    Label buttonLabel = new Label() { Tag = axis, Content = controller.GetAxisName(axis) };
                    TargetComboBox.Items.Add(buttonLabel);

                    if (axis.Equals(((AxisActions)this.Actions).Axis))
                        TargetComboBox.SelectedItem = buttonLabel;
                }

                // settings
                Axis2AxisImproveCircularity.IsOn = ((AxisActions)this.Actions).ImproveCircularity;
                Axis2AxisAutoRotate.IsOn = ((AxisActions)this.Actions).AutoRotate;
                Axis2AxisRotation.Value = (((AxisActions)this.Actions).AxisInverted ? 180 : 0) + (((AxisActions)this.Actions).AxisRotated ? 90 : 0);
                Axis2AxisRotation.IsEnabled = !Axis2AxisAutoRotate.IsOn;
                Axis2AxisInnerDeadzone.Value = ((AxisActions)this.Actions).AxisDeadZoneInner;
                Axis2AxisOuterDeadzone.Value = ((AxisActions)this.Actions).AxisDeadZoneOuter;
                Axis2AxisAntiDeadzone.Value = ((AxisActions)this.Actions).AxisAntiDeadZone;
            }
            else if (type == ActionType.Mouse)
            {
                if (this.Actions is null || this.Actions is not MouseActions)
                    this.Actions = new MouseActions();

                foreach (MouseActionsType mouseType in Enum.GetValues(typeof(MouseActionsType)))
                {
                    // skip specific scenarios
                    switch (mouseType)
                    {
                        case MouseActionsType.LeftButton:
                        case MouseActionsType.RightButton:
                        case MouseActionsType.MiddleButton:
                        case MouseActionsType.ScrollUp:
                        case MouseActionsType.ScrollDown:
                            continue;
                    }

                    // create a label, store MouseActionsType as Tag and Label as controller specific string
                    Label buttonLabel = new Label() { Tag = mouseType, Content = EnumUtils.GetDescriptionFromEnumValue(mouseType) };
                    TargetComboBox.Items.Add(buttonLabel);

                    if (mouseType.Equals(((MouseActions)this.Actions).MouseType))
                        TargetComboBox.SelectedItem = buttonLabel;
                }

                // settings
                Axis2MousePointerSpeed.Value = ((MouseActions)this.Actions).Sensivity;
                Axis2MouseAutoRotate.IsOn = ((MouseActions)this.Actions).AutoRotate;
                Axis2MouseRotation.Value = (((MouseActions)this.Actions).AxisInverted ? 180 : 0) + (((MouseActions)this.Actions).AxisRotated ? 90 : 0);
                Axis2MouseRotation.IsEnabled = !Axis2MouseAutoRotate.IsOn;
                Axis2MouseDeadzone.Value = ((MouseActions)this.Actions).Deadzone;
            }

            base.Update();
        }

        private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Actions is null)
                return;

            if (TargetComboBox.SelectedItem is null)
                return;

            // we're busy
            if (!Monitor.TryEnter(updateLock))
                return;

            // generate IActions based on settings
            switch (this.Actions.ActionType)
            {
                case ActionType.Joystick:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((AxisActions)this.Actions).Axis = (AxisLayoutFlags)buttonLabel.Tag;
                    }
                    break;

                case ActionType.Mouse:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((MouseActions)this.Actions).MouseType = (MouseActionsType)buttonLabel.Tag;
                    }
                    break;
            }

            base.Update();
        }

        private void Update()
        {
            // force full update
            Action_SelectionChanged(null, null);
            Target_SelectionChanged(null, null);
        }

        public void Reset()
        {
            if (Monitor.TryEnter(updateLock))
            {
                ActionComboBox.SelectedIndex = 0;
                TargetComboBox.SelectedItem = null;
                Monitor.Exit(updateLock);
            }
        }

        private void Axis2AxisAutoRotate_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.Actions is null)
                return;

            Axis2AxisRotation.IsEnabled = !Axis2AxisAutoRotate.IsOn;

            switch (this.Actions.ActionType)
            {
                case ActionType.Joystick:
                    ((AxisActions)this.Actions).AutoRotate = Axis2AxisAutoRotate.IsOn;
                    break;
            }

            base.Update();
        }

        private void Axis_Rotation_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Joystick:
                    ((AxisActions)this.Actions).AxisInverted = (((int)Axis2AxisRotation.Value / 90) & 2) == 2;
                    ((AxisActions)this.Actions).AxisRotated = (((int)Axis2AxisRotation.Value / 90) & 1) == 1;
                    break;
            }

            base.Update();
        }

        private void Axis_InnerDeadzone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Joystick:
                    ((AxisActions)this.Actions).AxisDeadZoneInner = (int)Axis2AxisInnerDeadzone.Value;
                    break;
            }

            base.Update();
        }

        private void Axis_OuterDeadzone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Joystick:
                    ((AxisActions)this.Actions).AxisDeadZoneOuter = (int)Axis2AxisOuterDeadzone.Value;
                    break;
            }

            base.Update();
        }

        private void Axis_AntiDeadZone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Joystick:
                    ((AxisActions)this.Actions).AxisAntiDeadZone = (int)Axis2AxisAntiDeadzone.Value;
                    break;
            }

            base.Update();
        }

        private void Axis2AxisImproveCircularity_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Joystick:
                    ((AxisActions)this.Actions).ImproveCircularity = Axis2AxisImproveCircularity.IsOn;
                    break;
            }

            base.Update();
        }

        private void Axis2MousePointerSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Mouse:
                    ((MouseActions)this.Actions).Sensivity = (int)Axis2MousePointerSpeed.Value;
                    break;
            }

            base.Update();
        }

        private void Axis2MouseAutoRotate_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.Actions is null)
                return;

            Axis2MouseRotation.IsEnabled = !Axis2MouseAutoRotate.IsOn;

            switch (this.Actions.ActionType)
            {
                case ActionType.Mouse:
                    ((MouseActions)this.Actions).AutoRotate = Axis2MouseAutoRotate.IsOn;
                    break;
            }

            base.Update();
        }

        private void Axis2MouseRotation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Mouse:
                    ((MouseActions)this.Actions).AxisInverted = (((int)Axis2MouseRotation.Value / 90) & 2) == 2;
                    ((MouseActions)this.Actions).AxisRotated = (((int)Axis2MouseRotation.Value / 90) & 1) == 1;
                    break;
            }

            base.Update();
        }

        private void Axis2MouseDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Mouse:
                    ((MouseActions)this.Actions).Deadzone = (int)Axis2MouseDeadzone.Value;
                    break;
            }

            base.Update();
        }
    }
}
