using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers;
using Inkore.UI.WPF.Modern.Controls;

namespace HandheldCompanion.Controls;

/// <summary>
///     Interaction logic for TriggerMapping.xaml
/// </summary>
public partial class TriggerMapping : IMapping
{
    public TriggerMapping()
    {
        InitializeComponent();
    }

    public TriggerMapping(AxisLayoutFlags axis) : this()
    {
        Value = axis;

        Icon.Glyph = axis.ToString();
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

        if (type == ActionType.Trigger)
        {
            if (Actions is null || Actions is not TriggerActions)
                Actions = new TriggerActions();

            // we need a controller to get compatible buttons
            if (controller is null)
                return;

            foreach (var axis in IController.GetTargetTriggers())
            {
                // create a label, store AxisLayoutFlags as Tag and Label as controller specific string
                var buttonLabel = new Label { Tag = axis, Content = controller.GetAxisName(axis) };
                TargetComboBox.Items.Add(buttonLabel);

                if (axis.Equals(((TriggerActions)Actions).Axis))
                    TargetComboBox.SelectedItem = buttonLabel;
            }
        }

        // settings
        Trigger2TriggerInnerDeadzone.Value = ((TriggerActions)this.Actions).AxisDeadZoneInner;
        Trigger2TriggerOuterDeadzone.Value = ((TriggerActions)this.Actions).AxisDeadZoneOuter;
        Trigger2TriggerAntiDeadzone.Value = ((TriggerActions)this.Actions).AxisAntiDeadZone;

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
            case ActionType.Trigger:
            {
                var buttonLabel = TargetComboBox.SelectedItem as Label;
                ((TriggerActions)Actions).Axis = (AxisLayoutFlags)buttonLabel.Tag;
            }
                break;
        }

        base.Update();
    }

    private void Trigger2TriggerInnerDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (this.Actions is null)
            return;

        switch (this.Actions.ActionType)
        {
            case ActionType.Trigger:
                ((TriggerActions)this.Actions).AxisDeadZoneInner = (int)Trigger2TriggerInnerDeadzone.Value;
                break;
        }

        base.Update();
    }

    private void Trigger2TriggerOuterDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (this.Actions is null)
            return;

        switch (this.Actions.ActionType)
        {
            case ActionType.Trigger:
                ((TriggerActions)this.Actions).AxisDeadZoneOuter = (int)Trigger2TriggerOuterDeadzone.Value;
                break;
        }

        base.Update();
    }

    private void Trigger2TriggerAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (this.Actions is null)
            return;

        switch (this.Actions.ActionType)
        {
            case ActionType.Trigger:
                ((TriggerActions)this.Actions).AxisAntiDeadZone = (int)Trigger2TriggerAntiDeadzone.Value;
                break;
        }

        base.Update();
    }

    public void Reset()
    {
        ActionComboBox.SelectedIndex = 0;
        TargetComboBox.SelectedItem = null;
    }
}