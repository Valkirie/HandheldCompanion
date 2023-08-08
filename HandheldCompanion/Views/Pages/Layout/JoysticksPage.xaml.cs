using System.Collections.Generic;
using System.Windows;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for JoysticksPage.xaml
/// </summary>
public partial class JoysticksPage : ILayoutPage
{
    public static List<ButtonFlags> LeftThumbButtons = new()
    {
        ButtonFlags.LeftStickClick, ButtonFlags.LeftStickTouch, ButtonFlags.LeftStickUp, ButtonFlags.LeftStickDown,
        ButtonFlags.LeftStickLeft, ButtonFlags.LeftStickRight
    };

    public static List<AxisLayoutFlags> LeftThumbAxis = new() { AxisLayoutFlags.LeftStick };

    public static List<ButtonFlags> RightThumbButtons = new()
    {
        ButtonFlags.RightStickClick, ButtonFlags.RightStickTouch, ButtonFlags.RightStickUp, ButtonFlags.RightStickDown,
        ButtonFlags.RightStickLeft, ButtonFlags.RightStickRight
    };

    public static List<AxisLayoutFlags> RightThumbAxis = new() { AxisLayoutFlags.RightStick };

    public JoysticksPage()
    {
        InitializeComponent();

        // draw UI
        foreach (ButtonFlags button in LeftThumbButtons)
        {
            ButtonStack panel = new(button);
            LeftJoystickButtonsPanel.Children.Add(panel);

            ButtonStacks.Add(button, panel);
        }

        foreach (AxisLayoutFlags axis in LeftThumbAxis)
        {
            AxisMapping axisMapping = new AxisMapping(axis);
            LeftJoystickPanel.Children.Add(axisMapping);

            AxisMappings.Add(axis, axisMapping);
        }

        foreach (ButtonFlags button in RightThumbButtons)
        {
            ButtonStack panel = new(button);
            RightJoystickButtonsPanel.Children.Add(panel);

            ButtonStacks.Add(button, panel);
        }

        foreach (AxisLayoutFlags axis in RightThumbAxis)
        {
            AxisMapping axisMapping = new AxisMapping(axis);
            RightJoystickPanel.Children.Add(axisMapping);

            AxisMappings.Add(axis, axisMapping);
        }
    }

    public override void UpdateController(IController controller)
    {
        base.UpdateController(controller);

        bool leftStick = CheckController(controller, LeftThumbAxis);
        bool rightStick = CheckController(controller, RightThumbAxis);

        gridLeftStick.Visibility = leftStick ? Visibility.Visible : Visibility.Collapsed;
        gridRightStick.Visibility = rightStick ? Visibility.Visible : Visibility.Collapsed;

        enabled = leftStick || rightStick;
    }

    public JoysticksPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // do something
    }

    public void Page_Closed()
    {
        // do something
    }
}