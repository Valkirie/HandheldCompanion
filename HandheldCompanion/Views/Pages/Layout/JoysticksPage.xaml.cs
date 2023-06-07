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
        foreach (var button in LeftThumbButtons)
        {
            var buttonMapping = new ButtonMapping(button);
            LeftJoystickButtonsPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var axis in LeftThumbAxis)
        {
            var axisMapping = new AxisMapping(axis);
            LeftJoystickPanel.Children.Add(axisMapping);

            MappingAxis.Add(axis, axisMapping);
        }

        foreach (var button in RightThumbButtons)
        {
            var buttonMapping = new ButtonMapping(button);
            RightJoystickButtonsPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var axis in RightThumbAxis)
        {
            var axisMapping = new AxisMapping(axis);
            RightJoystickPanel.Children.Add(axisMapping);

            MappingAxis.Add(axis, axisMapping);
        }
    }

    public JoysticksPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public override void UpdateController(IController Controller)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // controller based
            foreach (var mapping in MappingButtons)
            {
                var button = mapping.Key;
                var buttonMapping = mapping.Value;

                // update mapping visibility
                if (!Controller.HasSourceButton(button))
                {
                    buttonMapping.Visibility = Visibility.Collapsed;
                }
                else
                {
                    buttonMapping.Visibility = Visibility.Visible;

                    // update icon
                    var newIcon = Controller.GetFontIcon(button);
                    var newLabel = Controller.GetButtonName(button);

                    buttonMapping.UpdateIcon(newIcon, newLabel);
                }
            }

            foreach (var mapping in MappingAxis)
            {
                var flags = mapping.Key;
                var layout = AxisLayout.Layouts[flags];

                var axisMapping = mapping.Value;

                // update mapping visibility
                if (!Controller.HasSourceAxis(flags))
                {
                    axisMapping.Visibility = Visibility.Collapsed;
                }
                else
                {
                    axisMapping.Visibility = Visibility.Visible;

                    // update icon
                    var newIcon = Controller.GetFontIcon(flags);
                    var newLabel = Controller.GetAxisName(flags);
                    axisMapping.UpdateIcon(newIcon, newLabel);
                }
            }
        });
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