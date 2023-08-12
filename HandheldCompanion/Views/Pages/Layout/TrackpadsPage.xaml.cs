using System.Collections.Generic;
using System.Windows;
using HandheldCompanion.Controllers;

using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for TrackpadsPage.xaml
/// </summary>
public partial class TrackpadsPage : ILayoutPage
{
    public static List<ButtonFlags> LeftButtons = new()
    {
        ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClick, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown,
        ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight
    };

    public static List<AxisLayoutFlags> LeftAxis = new() { AxisLayoutFlags.LeftPad };

    public static List<ButtonFlags> RightButtons = new()
    {
        ButtonFlags.RightPadTouch, ButtonFlags.RightPadClick, ButtonFlags.RightPadClickUp,
        ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight
    };

    public static List<AxisLayoutFlags> RightAxis = new() { AxisLayoutFlags.RightPad };

    public TrackpadsPage()
    {
        InitializeComponent();

        // draw UI
        foreach (ButtonFlags button in LeftButtons)
        {
            ButtonStack panel = new(button);
            LeftTrackpadButtonsPanel.Children.Add(panel);

            ButtonStacks.Add(button, panel);
        }

        foreach (AxisLayoutFlags axis in LeftAxis)
        {
            AxisMapping axisMapping = new AxisMapping(axis);
            LeftTrackpadPanel.Children.Add(axisMapping);

            AxisMappings.Add(axis, axisMapping);
        }

        foreach (ButtonFlags button in RightButtons)
        {
            ButtonStack panel = new(button);
            RightTrackpadButtonsPanel.Children.Add(panel);

            ButtonStacks.Add(button, panel);
        }

        foreach (AxisLayoutFlags axis in RightAxis)
        {
            AxisMapping axisMapping = new AxisMapping(axis);
            RightTrackpadPanel.Children.Add(axisMapping);

            AxisMappings.Add(axis, axisMapping);
        }
    }

    public override void UpdateController(IController controller)
    {
        base.UpdateController(controller);

        bool leftPad = CheckController(controller, LeftAxis);
        bool rightPad = CheckController(controller, RightAxis);

        gridLeftPad.Visibility = leftPad ? Visibility.Visible : Visibility.Collapsed;
        gridRightPad.Visibility = rightPad ? Visibility.Visible : Visibility.Collapsed;

        enabled = leftPad || rightPad;
    }

    public TrackpadsPage(string Tag) : this()
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