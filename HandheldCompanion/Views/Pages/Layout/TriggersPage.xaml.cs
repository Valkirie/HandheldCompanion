using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Windows;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for TriggersPage.xaml
/// </summary>
public partial class TriggersPage : ILayoutPage
{
    public static List<ButtonFlags> LeftTrigger = new() { ButtonFlags.L2Soft, ButtonFlags.L2Full };
    public static List<AxisLayoutFlags> LeftTriggerAxis = new() { AxisLayoutFlags.L2 };
    public static List<ButtonFlags> RightTrigger = new() { ButtonFlags.R2Soft, ButtonFlags.R2Full };
    public static List<AxisLayoutFlags> RightTriggerAxis = new() { AxisLayoutFlags.R2 };

    public TriggersPage()
    {
        InitializeComponent();

        // draw UI
        foreach (ButtonFlags button in LeftTrigger)
        {
            ButtonStack panel = new(button);
            LeftTriggerButtonsPanel.Children.Add(panel);

            ButtonStacks.Add(button, panel);
        }

        foreach (AxisLayoutFlags axis in LeftTriggerAxis)
        {
            TriggerMapping axisMapping = new TriggerMapping(axis);
            LeftTriggerPanel.Children.Add(axisMapping);

            TriggerMappings.Add(axis, axisMapping);
        }

        foreach (ButtonFlags button in RightTrigger)
        {
            ButtonStack panel = new(button);
            RightTriggerButtonsPanel.Children.Add(panel);

            ButtonStacks.Add(button, panel);
        }

        foreach (AxisLayoutFlags axis in RightTriggerAxis)
        {
            TriggerMapping axisMapping = new TriggerMapping(axis);
            RightTriggerPanel.Children.Add(axisMapping);

            TriggerMappings.Add(axis, axisMapping);
        }
    }

    public override void UpdateController(IController controller)
    {
        base.UpdateController(controller);

        bool leftTrigger = CheckController(controller, LeftTriggerAxis);
        bool rightTrigger = CheckController(controller, RightTriggerAxis);

        gridLeftTrigger.Visibility = leftTrigger ? Visibility.Visible : Visibility.Collapsed;
        gridRightTrigger.Visibility = rightTrigger ? Visibility.Visible : Visibility.Collapsed;

        enabled = leftTrigger || rightTrigger;
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