using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers;
using Inkore.UI.WPF.Modern.Controls;

namespace HandheldCompanion.Controls;

/// <summary>
///     Interaction logic for AxisMapping.xaml
/// </summary>
public partial class AxisMapping : IMapping
{
    public AxisMapping()
    {
        InitializeComponent();
    }

    public AxisMapping(AxisLayoutFlags axis) : this()
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
        // update mapping IActions
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

        if (type == ActionType.Joystick)
        {
            if (Actions is null || Actions is not AxisActions)
                Actions = new AxisActions();

            // we need a controller to get compatible buttons
            if (controller is null)
                return;

            foreach (var axis in IController.GetTargetAxis())
            {
                // create a label, store ButtonFlags as Tag and Label as controller specific string
                var buttonLabel = new Label { Tag = axis, Content = controller.GetAxisName(axis) };
                TargetComboBox.Items.Add(buttonLabel);

                if (axis.Equals(((AxisActions)Actions).Axis))
                    TargetComboBox.SelectedItem = buttonLabel;
            }

            // settings
            Axis2AxisImproveCircularity.IsOn = ((AxisActions)Actions).ImproveCircularity;
            Axis2AxisAutoRotate.IsOn = ((AxisActions)Actions).AutoRotate;
            Axis2AxisRotation.Value = (((AxisActions)Actions).AxisInverted ? 180 : 0) +
                                      (((AxisActions)Actions).AxisRotated ? 90 : 0);
            Axis2AxisInnerDeadzone.Value = ((AxisActions)Actions).AxisDeadZoneInner;
            Axis2AxisOuterDeadzone.Value = ((AxisActions)Actions).AxisDeadZoneOuter;
            Axis2AxisAntiDeadzone.Value = ((AxisActions)Actions).AxisAntiDeadZone;
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
                    case MouseActionsType.LeftButton:
                    case MouseActionsType.RightButton:
                    case MouseActionsType.MiddleButton:
                    case MouseActionsType.ScrollUp:
                    case MouseActionsType.ScrollDown:
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
            Axis2MousePointerSpeed.Value = ((MouseActions)Actions).Sensivity;
            Axis2MouseAutoRotate.IsOn = ((MouseActions)Actions).AutoRotate;
            Axis2MouseRotation.Value = (((MouseActions)Actions).AxisInverted ? 180 : 0) +
                                       (((MouseActions)Actions).AxisRotated ? 90 : 0);
            Axis2MouseDeadzone.Value = ((MouseActions)Actions).Deadzone;
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
            case ActionType.Joystick:
            {
                var buttonLabel = TargetComboBox.SelectedItem as Label;
                ((AxisActions)Actions).Axis = (AxisLayoutFlags)buttonLabel.Tag;
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

    private void Axis2AxisAutoRotate_Toggled(object sender, RoutedEventArgs e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Joystick:
                ((AxisActions)Actions).AutoRotate = Axis2AxisAutoRotate.IsOn;
                break;
        }

        base.Update();
    }

    private void Axis_Rotation_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Joystick:
                ((AxisActions)Actions).AxisInverted = (((int)Axis2AxisRotation.Value / 90) & 2) == 2;
                ((AxisActions)Actions).AxisRotated = (((int)Axis2AxisRotation.Value / 90) & 1) == 1;
                break;
        }

        base.Update();
    }

    private void Axis_InnerDeadzone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Joystick:
                ((AxisActions)Actions).AxisDeadZoneInner = (int)Axis2AxisInnerDeadzone.Value;
                break;
        }

        base.Update();
    }

    private void Axis_OuterDeadzone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Joystick:
                ((AxisActions)Actions).AxisDeadZoneOuter = (int)Axis2AxisOuterDeadzone.Value;
                break;
        }

        base.Update();
    }

    private void Axis_AntiDeadZone_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Joystick:
                ((AxisActions)Actions).AxisAntiDeadZone = (int)Axis2AxisAntiDeadzone.Value;
                break;
        }

        base.Update();
    }

    private void Axis2AxisImproveCircularity_Toggled(object sender, RoutedEventArgs e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Joystick:
                ((AxisActions)Actions).ImproveCircularity = Axis2AxisImproveCircularity.IsOn;
                break;
        }

        base.Update();
    }

    private void Axis2MousePointerSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Mouse:
                ((MouseActions)Actions).Sensivity = (int)Axis2MousePointerSpeed.Value;
                break;
        }

        base.Update();
    }

    private void Axis2MouseAutoRotate_Toggled(object sender, RoutedEventArgs e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Mouse:
                ((MouseActions)Actions).AutoRotate = Axis2MouseAutoRotate.IsOn;
                break;
        }

        base.Update();
    }

    private void Axis2MouseRotation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Mouse:
                ((MouseActions)Actions).AxisInverted = (((int)Axis2MouseRotation.Value / 90) & 2) == 2;
                ((MouseActions)Actions).AxisRotated = (((int)Axis2MouseRotation.Value / 90) & 1) == 1;
                break;
        }

        base.Update();
    }

    private void Axis2MouseDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Actions is null)
            return;

        switch (Actions.ActionType)
        {
            case ActionType.Mouse:
                ((MouseActions)Actions).Deadzone = (int)Axis2MouseDeadzone.Value;
                break;
        }

        base.Update();
    }
}