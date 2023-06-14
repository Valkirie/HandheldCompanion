using System.Threading;
using System.Windows.Controls;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
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
        prevValue = axis;

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
            case ActionType.Trigger:
            {
                var buttonLabel = TargetComboBox.SelectedItem as Label;
                ((TriggerActions)Actions).Axis = (AxisLayoutFlags)buttonLabel.Tag;
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
}