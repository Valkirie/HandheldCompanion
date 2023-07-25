using System.Collections.Generic;
using System.Windows;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for TriggersPage.xaml
/// </summary>
public partial class TriggersPage : ILayoutPage
{
    public static List<ButtonFlags> LeftTrigger = new() { ButtonFlags.L2, ButtonFlags.L3 };
    public static List<AxisLayoutFlags> LeftTriggerAxis = new() { AxisLayoutFlags.L2 };
    public static List<ButtonFlags> RightTrigger = new() { ButtonFlags.R2, ButtonFlags.R3 };
    public static List<AxisLayoutFlags> RightTriggerAxis = new() { AxisLayoutFlags.R2 };

    public TriggersPage()
    {
        InitializeComponent();

        // draw UI
        foreach (var button in LeftTrigger)
        {
            var buttonMapping = new ButtonMapping(button);
            LeftTriggerButtonsPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var axis in LeftTriggerAxis)
        {
            var axisMapping = new TriggerMapping(axis);
            LeftTriggerPanel.Children.Add(axisMapping);

            MappingTriggers.Add(axis, axisMapping);
        }

        foreach (var button in RightTrigger)
        {
            var buttonMapping = new ButtonMapping(button);
            RightTriggerButtonsPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var axis in RightTriggerAxis)
        {
            var axisMapping = new TriggerMapping(axis);
            RightTriggerPanel.Children.Add(axisMapping);

            MappingTriggers.Add(axis, axisMapping);
        }
    }

    public TriggersPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }
}