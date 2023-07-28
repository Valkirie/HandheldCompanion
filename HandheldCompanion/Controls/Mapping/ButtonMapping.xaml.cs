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
using Inkore.UI.WPF.Modern.Controls;

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
    }

    public void SetIActions(IActions actions)
    {
        // reset and update mapping IActions
        Reset();
        base.SetIActions(actions);

        // update UI
        ActionComboBox.SelectedIndex = (int)actions.ActionType;
    }

    public IActions GetIActions()
    {
        return Actions;
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

            foreach (var button in IController.GetTargetButtons())
            {
                // create a label, store ButtonFlags as Tag and Label as controller specific string
                var buttonLabel = new Label { Tag = button, Content = controller.GetButtonName(button) };
                TargetComboBox.Items.Add(buttonLabel);

                if (button.Equals(((ButtonActions)Actions).Button))
                    TargetComboBox.SelectedItem = buttonLabel;
            }

            // settings
            if (TargetComboBox.SelectedItem is not null)
                PressComboBox.SelectedIndex = (int)this.Actions.PressType;
            else
                this.Actions.PressType = (PressType)PressComboBox.SelectedIndex;
            PressComboBox.SelectedIndex = (int)this.Actions.PressType;
            Toggle_Turbo.IsOn = this.Actions.Turbo;
            Turbo_Slider.Value = this.Actions.TurboDelay;
            Toggle_Toggle.IsOn = this.Actions.Toggle;
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
            if (TargetComboBox.SelectedItem is not null)
                PressComboBox.SelectedIndex = (int)this.Actions.PressType;
            else
                this.Actions.PressType = (PressType)PressComboBox.SelectedIndex;
            PressComboBox.SelectedIndex = (int)this.Actions.PressType;
            Toggle_Turbo.IsOn = this.Actions.Turbo;
            Turbo_Slider.Value = this.Actions.TurboDelay;
            Toggle_Toggle.IsOn = this.Actions.Toggle;
            ModifierComboBox.SelectedIndex = (int)((KeyboardActions)this.Actions).Modifiers;
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
            if (TargetComboBox.SelectedItem is not null)
                PressComboBox.SelectedIndex = (int)this.Actions.PressType;
            else
                this.Actions.PressType = (PressType)PressComboBox.SelectedIndex;
            PressComboBox.SelectedIndex = (int)this.Actions.PressType;
            Toggle_Turbo.IsOn = this.Actions.Turbo;
            Turbo_Slider.Value = this.Actions.TurboDelay;
            Toggle_Toggle.IsOn = this.Actions.Toggle;
            ModifierComboBox.SelectedIndex = (int)((MouseActions)this.Actions).Modifiers;
        }

        base.Update();
    }

    private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Actions is null)
            return;

        if (TargetComboBox.SelectedItem is null)
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

    public void Reset()
    {
        ActionComboBox.SelectedIndex = 0;
        TargetComboBox.SelectedItem = null;
    }

    private void Press_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.Actions is null)
            return;

        this.Actions.PressType = (PressType)PressComboBox.SelectedIndex;

        base.Update();
    }

    private void Modifier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.Actions is null)
            return;

        ModifierSet mods = (ModifierSet)ModifierComboBox.SelectedIndex;

        switch (this.Actions.ActionType)
        {
            case ActionType.Keyboard:
                ((KeyboardActions)this.Actions).Modifiers = mods;
                break;
            case ActionType.Mouse:
                ((MouseActions)this.Actions).Modifiers = mods;
                break;
        }

        base.Update();
    }

    private void Toggle_Turbo_Toggled(object sender, RoutedEventArgs e)
    {
        if (Actions is null)
            return;

        this.Actions.Turbo = Toggle_Turbo.IsOn;

        base.Update();
    }

    private void Turbo_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        this.Actions.TurboDelay = (int)Turbo_Slider.Value;

        base.Update();
    }

    private void Toggle_Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (Actions is null)
            return;

        this.Actions.Toggle = Toggle_Toggle.IsOn;

        base.Update();
    }
}