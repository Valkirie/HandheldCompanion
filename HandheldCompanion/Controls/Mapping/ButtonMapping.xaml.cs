using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using KeyboardSimulator = HandheldCompanion.Simulators.KeyboardSimulator;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Interaction logic for ButtonMapping.xaml
    /// </summary>
    public partial class ButtonMapping : IMapping
    {
        public ButtonMapping()
        {
            InitializeComponent();
        }

        public ButtonMapping(ButtonFlags button) : this()
        {
            this.Value = button;
            this.prevValue = button;

            this.Icon.Glyph = button.ToString();
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

            if (type == ActionType.None)
            {
                if (this.Actions is not null)
                    base.Delete();
                return;
            }

            if (type == ActionType.Button)
            {
                if (this.Actions is null || this.Actions is not ButtonActions)
                    this.Actions = new ButtonActions();

                // we need a controller to get compatible buttons
                if (controller is null)
                    return;

                foreach (ButtonFlags button in controller.GetButtons())
                {
                    // create a label, store ButtonFlags as Tag and Label as controller specific string
                    Label buttonLabel = new Label() { Tag = button, Content = controller.GetButtonName(button) };
                    TargetComboBox.Items.Add(buttonLabel);

                    if (button.Equals(((ButtonActions)this.Actions).Button))
                        TargetComboBox.SelectedItem = buttonLabel;
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

                foreach (VirtualKeyCode key in Enum.GetValues(typeof(VirtualKeyCode)))
                {
                    // create a label, store VirtualKeyCode as Tag and Label as controller specific string
                    Label buttonLabel = new Label() { Tag = key, Content = KeyboardSimulator.GetVirtualKey(key) };
                    TargetComboBox.Items.Add(buttonLabel);

                    if (key.Equals(((KeyboardActions)this.Actions).Key))
                        TargetComboBox.SelectedItem = buttonLabel;
                }

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
                    // skip specific scenarios
                    switch (mouseType)
                    {
                        case MouseActionsType.Move:
                        case MouseActionsType.Scroll:
                            continue;
                    }

                    // create a label, store MouseActionsType as Tag and Label as controller specific string
                    Label buttonLabel = new Label() { Tag = mouseType, Content = EnumUtils.GetDescriptionFromEnumValue(mouseType) };
                    TargetComboBox.Items.Add(buttonLabel);

                    if (mouseType.Equals(((MouseActions)this.Actions).MouseType))
                        TargetComboBox.SelectedItem = buttonLabel;
                }

                // settings
                Toggle_Turbo.IsOn = ((MouseActions)this.Actions).Turbo;
                Turbo_Slider.Value = ((MouseActions)this.Actions).TurboDelay;
                Toggle_Toggle.IsOn = ((MouseActions)this.Actions).Toggle;
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
                case ActionType.Button:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((ButtonActions)this.Actions).Button = (ButtonFlags)buttonLabel.Tag;
                    }
                    break;

                case ActionType.Keyboard:
                    {
                        Label buttonLabel = TargetComboBox.SelectedItem as Label;
                        ((KeyboardActions)this.Actions).Key = (VirtualKeyCode)buttonLabel.Tag;
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

            base.Update();
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

            base.Update();
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

            base.Update();
        }
        #endregion
    }
}
