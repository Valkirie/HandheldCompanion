using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers;
using HandheldCompanion.Simulators;
using ModernWpf.Controls;

namespace HandheldCompanion.Controls;

/// <summary>
///     Interaction logic for ButtonMapping.xaml
/// </summary>
public partial class ButtonMapping : IMapping
{
    public ButtonMapping()
    {
        InitializeComponent();
    }

    public ButtonMapping(ButtonFlags button) : this()
    {
        Value = button;
        prevValue = button;

        Icon.Glyph = button.ToString();
    }

    public void UpdateIcon(FontIcon newIcon, string newLabel)
    {
        Name.Text = newLabel;

        Icon.Glyph = newIcon.Glyph;
        Icon.FontFamily = newIcon.FontFamily;
        Icon.FontSize = newIcon.FontSize;

        if (newIcon.Foreground is not null)
            Icon.Foreground = newIcon.Foreground;
        else
            Icon.SetResourceReference(ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

        Update();
    }

    internal void SetIActions(IActions actions)
    {
        // reset and update mapping IActions
        Reset();
        base.SetIActions(actions);

        // update UI
        ActionComboBox.SelectedIndex = (int)actions.ActionType;
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
        var controller = ControllerManager.GetEmulatedController();

        // populate target dropdown based on action type
        var type = (ActionType)ActionComboBox.SelectedIndex;

        if (type == ActionType.Disabled)
        {
            if (Actions is not null)
                Delete();
            return;
        }

        if (type == ActionType.Button)
        {
            if (Actions is null || Actions is not ButtonActions)
                Actions = new ButtonActions();

            // we need a controller to get compatible buttons
            if (controller is null)
                return;

            foreach (var button in IController.GetTargetButtons())
            {
                // create a label, store ButtonFlags as Tag and Label as controller specific string
                var buttonLabel = new Label { Tag = button, Content = controller.GetButtonName(button) };
                TargetComboBox.Items.Add(buttonLabel);

                if (button.Equals(((ButtonActions)Actions).Button))
                    TargetComboBox.SelectedItem = buttonLabel;
            }

            // settings
            Toggle_Turbo.IsOn = ((ButtonActions)Actions).Turbo;
            Turbo_Slider.Value = ((ButtonActions)Actions).TurboDelay;
            Toggle_Toggle.IsOn = ((ButtonActions)Actions).Toggle;
        }
        else if (type == ActionType.Keyboard)
        {
            if (Actions is null || Actions is not KeyboardActions)
                Actions = new KeyboardActions();

            foreach (VirtualKeyCode key in Enum.GetValues(typeof(VirtualKeyCode)))
            {
                // create a label, store VirtualKeyCode as Tag and Label as controller specific string
                var buttonLabel = new Label { Tag = key, Content = KeyboardSimulator.GetVirtualKey(key) };
                TargetComboBox.Items.Add(buttonLabel);

                if (key.Equals(((KeyboardActions)Actions).Key))
                    TargetComboBox.SelectedItem = buttonLabel;
            }

            // settings
            Toggle_Turbo.IsOn = ((KeyboardActions)Actions).Turbo;
            Turbo_Slider.Value = ((KeyboardActions)Actions).TurboDelay;
            Toggle_Toggle.IsOn = ((KeyboardActions)Actions).Toggle;
        }
        else if (type == ActionType.Mouse)
        {
            if (Actions is null || Actions is not MouseActions)
                Actions = new MouseActions();

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
                var buttonLabel = new Label
                    { Tag = mouseType, Content = EnumUtils.GetDescriptionFromEnumValue(mouseType) };
                TargetComboBox.Items.Add(buttonLabel);

                if (mouseType.Equals(((MouseActions)Actions).MouseType))
                    TargetComboBox.SelectedItem = buttonLabel;
            }

            // settings
            Toggle_Turbo.IsOn = ((MouseActions)Actions).Turbo;
            Turbo_Slider.Value = ((MouseActions)Actions).TurboDelay;
            Toggle_Toggle.IsOn = ((MouseActions)Actions).Toggle;
        }

        base.Update();
    }

    private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Actions is null)
            return;

        if (TargetComboBox.SelectedItem is null)
            return;

        // we're busy
        if (!Monitor.TryEnter(updateLock))
            return;

        // generate IActions based on settings
        switch (Actions.ActionType)
        {
            case ActionType.Button:
            {
                var buttonLabel = TargetComboBox.SelectedItem as Label;
                ((ButtonActions)Actions).Button = (ButtonFlags)buttonLabel.Tag;
            }
                break;

            case ActionType.Keyboard:
            {
                var buttonLabel = TargetComboBox.SelectedItem as Label;
                ((KeyboardActions)Actions).Key = (VirtualKeyCode)buttonLabel.Tag;
            }
                break;

            case ActionType.Mouse:
            {
                var buttonLabel = TargetComboBox.SelectedItem as Label;
                ((MouseActions)Actions).MouseType = (MouseActionsType)buttonLabel.Tag;
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
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Button:
                ((ButtonActions)Actions).Turbo = Toggle_Turbo.IsOn;
                break;
            case ActionType.Keyboard:
                ((KeyboardActions)Actions).Turbo = Toggle_Turbo.IsOn;
                break;
            case ActionType.Mouse:
                ((MouseActions)Actions).Turbo = Toggle_Turbo.IsOn;
                break;
        }

        base.Update();
    }

    private void Turbo_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Button:
                ((ButtonActions)Actions).TurboDelay = (byte)Turbo_Slider.Value;
                break;
            case ActionType.Keyboard:
                ((KeyboardActions)Actions).TurboDelay = (byte)Turbo_Slider.Value;
                break;
            case ActionType.Mouse:
                ((MouseActions)Actions).TurboDelay = (byte)Turbo_Slider.Value;
                break;
        }

        base.Update();
    }

    private void Toggle_Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Button:
                ((ButtonActions)Actions).Toggle = Toggle_Toggle.IsOn;
                break;
            case ActionType.Keyboard:
                ((KeyboardActions)Actions).Toggle = Toggle_Toggle.IsOn;
                break;
            case ActionType.Mouse:
                ((MouseActions)Actions).Toggle = Toggle_Toggle.IsOn;
                break;
        }

        base.Update();
    }

    #endregion
}