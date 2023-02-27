using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using LiveCharts.Wpf;
using ModernWpf.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
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

        #region events
        public event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(ButtonFlags button);
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(ButtonFlags button, IActions action);
        #endregion

        public ButtonMapping()
        {
            InitializeComponent();
        }

        public ButtonMapping(ButtonFlags button) : this()
        {
            this.Button = button;

            switch (button)
            {
                default:
                    this.Icon.Glyph = button.ToString();
                    break;
                case ButtonFlags.OEM1:
                case ButtonFlags.OEM2:
                case ButtonFlags.OEM3:
                case ButtonFlags.OEM4:
                case ButtonFlags.OEM5:
                case ButtonFlags.OEM6:
                case ButtonFlags.OEM7:
                case ButtonFlags.OEM8:
                case ButtonFlags.OEM9:
                case ButtonFlags.OEM10:
                    break;
            }

            this.Name.Text = MainWindow.CurrentDevice.GetButtonName(button);
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
                Deleted?.Invoke(Button);
                return;
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

            // update button mapping
            Updated?.Invoke(Button, Actions);
        }

        private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Actions is null)
                return;

            if (TargetComboBox.SelectedItem is null)
                return;

            // generate IActions based on settings
            switch (this.Actions.ActionType)
            {
                case ActionType.Button:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((ButtonActions)this.Actions).Button = (ButtonFlags)buttonLabel.Tag;
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
            Updated?.Invoke(Button, Actions);
        }

        private void Update()
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

            switch (this.Actions.ActionType)
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
    }
}
