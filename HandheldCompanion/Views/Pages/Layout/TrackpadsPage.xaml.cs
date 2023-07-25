using System.Collections.Generic;
using System.Windows;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;

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
        foreach (var button in LeftButtons)
        {
            var buttonMapping = new ButtonMapping(button);
            LeftTrackpadButtonsPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var axis in LeftAxis)
        {
            var axisMapping = new AxisMapping(axis);
            LeftTrackpadPanel.Children.Add(axisMapping);

            MappingAxis.Add(axis, axisMapping);
        }

        foreach (var button in RightButtons)
        {
            var buttonMapping = new ButtonMapping(button);
            RightTrackpadButtonsPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var axis in RightAxis)
        {
            var axisMapping = new AxisMapping(axis);
            RightTrackpadPanel.Children.Add(axisMapping);

            MappingAxis.Add(axis, axisMapping);
        }
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