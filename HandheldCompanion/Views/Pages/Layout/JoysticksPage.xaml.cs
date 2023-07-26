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
        ButtonFlags.LeftThumb, ButtonFlags.LeftThumbTouch, ButtonFlags.LeftThumbUp, ButtonFlags.LeftThumbDown,
        ButtonFlags.LeftThumbLeft, ButtonFlags.LeftThumbRight
    };

    public static List<AxisLayoutFlags> LeftThumbAxis = new() { AxisLayoutFlags.LeftThumb };

    public static List<ButtonFlags> RightThumbButtons = new()
    {
        ButtonFlags.RightThumb, ButtonFlags.RightThumbTouch, ButtonFlags.RightThumbUp, ButtonFlags.RightThumbDown,
        ButtonFlags.RightThumbLeft, ButtonFlags.RightThumbRight
    };

    public static List<AxisLayoutFlags> RightThumbAxis = new() { AxisLayoutFlags.RightThumb };

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