using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace HandheldCompanion.Controls;

/// <summary>
///     Interaction logic for ButtonMapping.xaml
/// </summary>
public partial class ButtonMapping : IMapping
{
    private static List<Label> keyList = null;

    public ButtonMapping()
    {
        // lazilly initialize
        if (keyList is null)
        {
            keyList = new();
            foreach (KeyFlags key in KeyFlagsOrder.arr)
            {
                // create a label, store VirtualKeyCode as Tag and Label as controller specific string
                Label buttonLabel = new Label() { Tag = (VirtualKeyCode)key, Content = EnumUtils.GetDescriptionFromEnumValue(key) };
                keyList.Add(buttonLabel);
            }
        }

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

    public void UpdateSelections()
    {
        Action_SelectionChanged(null, null);
    }

    public void SetIActions(IActions actions)
    {
        // reset and update mapping IActions
        Reset();
        base.SetIActions(actions);

        // update UI
        ActionComboBox.SelectedIndex = (int)actions.actionType;
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
        TargetComboBox.ItemsSource = null;
        TargetComboBox.Items.Clear();
        TargetComboBox.IsEnabled = ActionComboBox.SelectedIndex != 0;

        // get current controller
        IController controller = ControllerManager.GetEmulatedController();

        // populate target dropdown based on action type
        ActionType type = (ActionType)ActionComboBox.SelectedIndex;

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

            foreach (ButtonFlags button in IController.GetTargetButtons())
            {
                // create a label, store ButtonFlags as Tag and Label as controller specific string
                Label buttonLabel = new Label { Tag = button, Content = controller.GetButtonName(button) };
                TargetComboBox.Items.Add(buttonLabel);

                if (button.Equals(((ButtonActions)Actions).Button))
                    TargetComboBox.SelectedItem = buttonLabel;
            }

            // button specific settings
        }
        else if (type == ActionType.Keyboard)
        {
            if (Actions is null || Actions is not KeyboardActions)
                Actions = new KeyboardActions();

            // use optimized lazily created list
            TargetComboBox.ItemsSource = keyList;
            foreach (var keyLabel in keyList.Where(keyLabel => keyLabel.Tag.Equals(((KeyboardActions)this.Actions).Key)))
                TargetComboBox.SelectedItem = keyLabel;

            // keyboard specific settings
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
                Label buttonLabel = new Label { Tag = mouseType, Content = EnumUtils.GetDescriptionFromEnumValue(mouseType) };
                TargetComboBox.Items.Add(buttonLabel);

                if (mouseType.Equals(((MouseActions)Actions).MouseType))
                    TargetComboBox.SelectedItem = buttonLabel;
            }

            // mouse specific settings
            ModifierComboBox.SelectedIndex = (int)((MouseActions)this.Actions).Modifiers;
        }

        // settings
        if (TargetComboBox.SelectedItem is not null)
            PressComboBox.SelectedIndex = (int)this.Actions.pressType;
        else
            this.Actions.pressType = (PressType)PressComboBox.SelectedIndex;

        // if no target element was selected, pick the first one
        if (TargetComboBox.SelectedItem is null)
            TargetComboBox.SelectedIndex = 0;
        
        Button2ButtonPressDelay.Visibility = Actions.pressType != PressType.Short ? Visibility.Visible : Visibility.Collapsed;

        // settings
        LongPressDelaySlider.Value = (int)this.Actions.ActionTimer;
        Toggle_Turbo.IsOn = this.Actions.Turbo;
        Turbo_Slider.Value = this.Actions.TurboDelay;
        Toggle_Interruptable.IsOn = this.Actions.Interruptable;
        Toggle_Toggle.IsOn = this.Actions.Toggle;
        HapticModeComboBox.SelectedIndex = (int)this.Actions.HapticMode;
        HapticStrengthComboBox.SelectedIndex = (int)this.Actions.HapticStrength;

        base.Update();
    }

    private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Actions is null)
            return;

        if (TargetComboBox.SelectedItem is null)
            return;

        // generate IActions based on settings
        switch (Actions.actionType)
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
        PressComboBox.SelectedIndex = 0;
        TargetComboBox.SelectedItem = null;
    }

    private void Press_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.Actions is null)
            return;

        this.Actions.pressType = (PressType)PressComboBox.SelectedIndex;

        Button2ButtonPressDelay.Visibility = Actions.pressType != PressType.Short ? Visibility.Visible : Visibility.Collapsed;

        base.Update();
    }

    private void LongPressDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (this.Actions is null)
            return;

        this.Actions.ActionTimer = (int)LongPressDelaySlider.Value;

        base.Update();
    }

    private void Modifier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.Actions is null)
            return;

        ModifierSet mods = (ModifierSet)ModifierComboBox.SelectedIndex;

        switch (this.Actions.actionType)
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
        if (this.Actions is null)
            return;

        this.Actions.Turbo = Toggle_Turbo.IsOn;

        base.Update();
    }

    private void Turbo_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (this.Actions is null)
            return;

        this.Actions.TurboDelay = (int)Turbo_Slider.Value;

        base.Update();
    }

    private void Toggle_Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (this.Actions is null)
            return;

        this.Actions.Toggle = Toggle_Toggle.IsOn;

        base.Update();
    }

    private void HapticMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.Actions is null)
            return;

        this.Actions.HapticMode = (HapticMode)HapticModeComboBox.SelectedIndex;
        this.HapticStrengthComboBox.IsEnabled = Actions.HapticMode == HapticMode.Off ? false : true;

        base.Update();
    }

    private void HapticStrength_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.Actions is null)
            return;

        this.Actions.HapticStrength = (HapticStrength)HapticStrengthComboBox.SelectedIndex;

        base.Update();
    }

    private void Toggle_Interruptable_Toggled(object sender, RoutedEventArgs e)
    {
        if (this.Actions is null)
            return;

        this.Actions.Interruptable = Toggle_Interruptable.IsOn;

        base.Update();
    }
}