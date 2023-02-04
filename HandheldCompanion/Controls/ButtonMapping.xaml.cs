using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
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
                if (ProfilesPage.currentProfile is not null)
                    ProfilesPage.currentProfile.ButtonMapping.Remove(Button);
            }
            else if (type == ActionType.Button)
            {
                if (this.Actions is null || this.Actions is not ButtonActions)
                    this.Actions = new ButtonActions();

                // we need a controller to get compatible buttons
                if (controller is null)
                    return;

                foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
                {
                    if (controller.IsButtonSupported(button))
                    {
                        // create a label, store ButtonFlags as Tag and Label as controller specific string
                        Label buttonLabel = new Label() { Tag = button, Content = controller.GetButtonName(button) };
                        TargetComboBox.Items.Add(buttonLabel);

                        if (button.Equals(((ButtonActions)this.Actions).Button))
                            TargetComboBox.SelectedItem = buttonLabel;
                    }
                }

                // settings
                Toggle_Turbo.IsOn = ((ButtonActions)this.Actions).Turbo;
                Turbo_Slider.Value = ((ButtonActions)this.Actions).TurboDelay;
                Toggle_Toggle.IsOn = ((ButtonActions)this.Actions).Toggle;
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
                Axis_Slider.Value = ((AxisActions)this.Actions).AxisPercentage;
                AxisPolarity.SelectedIndex = ((AxisActions)this.Actions).AxisPolarity;
            }
            else if (type == ActionType.Keyboard)
            {
                if (this.Actions is null || this.Actions is not KeyboardActions)
                    this.Actions = new KeyboardActions();

                // localize me ?
                foreach (VirtualKeyCode key in Enum.GetValues(typeof(VirtualKeyCode)))
                    TargetComboBox.Items.Add(key);

                TargetComboBox.SelectedItem = ((KeyboardActions)this.Actions).Key;

                // settings
                Toggle_Turbo.IsOn = ((KeyboardActions)this.Actions).Turbo;
                Turbo_Slider.Value = ((KeyboardActions)this.Actions).TurboDelay;
                Toggle_Toggle.IsOn = ((KeyboardActions)this.Actions).Toggle;
            }
            else if (type == ActionType.Mouse)
            {
                if (this.Actions is null || this.Actions is not MouseActions)
                    this.Actions = new MouseActions();

                foreach (MouseActionsType mouseType in Enum.GetValues(typeof(MouseActionsType)))
                {
                    // skip axis related actions
                    if (mouseType > MouseActionsType.MiddleButton)
                        continue;

                    // localize me ?
                    TargetComboBox.Items.Add(mouseType);
                }

                TargetComboBox.SelectedItem = ((MouseActions)this.Actions).MouseType;

                // settings
                Toggle_Turbo.IsOn = ((MouseActions)this.Actions).Turbo;
                Turbo_Slider.Value = ((MouseActions)this.Actions).TurboDelay;
                Toggle_Toggle.IsOn = ((MouseActions)this.Actions).Toggle;
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

            // update profile button mapping
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.ButtonMapping[Button] = this.Actions;
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

        #region Button2Button
        private void Toggle_Turbo_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.Actions is null)
                return;

            switch(this.Actions.ActionType)
            {
                case ActionType.Button:
                    ((ButtonActions)this.Actions).Turbo = Toggle_Turbo.IsOn;
                    break;
                case ActionType.Keyboard:
                    ((KeyboardActions)this.Actions).Turbo = Toggle_Turbo.IsOn;
                    break;
                case ActionType.Mouse:
                    ((MouseActions)this.Actions).Turbo = Toggle_Turbo.IsOn;
                    break;
            }
        }

        private void Turbo_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Button:
                    ((ButtonActions)this.Actions).TurboDelay = (byte)Turbo_Slider.Value;
                    break;
                case ActionType.Keyboard:
                    ((KeyboardActions)this.Actions).TurboDelay = (byte)Turbo_Slider.Value;
                    break;
                case ActionType.Mouse:
                    ((MouseActions)this.Actions).TurboDelay = (byte)Turbo_Slider.Value;
                    break;
            }
        }

        private void Toggle_Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.Actions is null)
                return;

            switch (this.Actions.ActionType)
            {
                case ActionType.Button:
                    ((ButtonActions)this.Actions).Toggle = Toggle_Toggle.IsOn;
                    break;
                case ActionType.Keyboard:
                    ((KeyboardActions)this.Actions).Toggle = Toggle_Toggle.IsOn;
                    break;
                case ActionType.Mouse:
                    ((MouseActions)this.Actions).Toggle = Toggle_Toggle.IsOn;
                    break;
            }
        }
        #endregion

        #region Button2Axis
        private void Axis_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.Actions is null || this.Actions.ActionType != ActionType.Axis)
                return;

            ((AxisActions)this.Actions).AxisPercentage = Axis_Slider.Value;
        }

        private void AxisPolarity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Actions is null || this.Actions.ActionType != ActionType.Axis)
                return;

            ((AxisActions)this.Actions).AxisPolarity = (byte)AxisPolarity.SelectedIndex;
        }
        #endregion
    }
}
