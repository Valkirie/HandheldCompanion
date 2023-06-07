using System.Collections.Generic;
using System.Windows;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for DpadPage.xaml
/// </summary>
public partial class DpadPage : ILayoutPage
{
    public static List<ButtonFlags> DPAD = new()
        { ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight };

    public DpadPage()
    {
        InitializeComponent();

        // draw UI
        foreach (var button in DPAD)
        {
            var buttonMapping = new ButtonMapping(button);
            DpadStackPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }
    }

    public DpadPage(string Tag) : this()
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
                    buttonMapping.Visibility = Visibility.Collapsed;
                else
                    buttonMapping.Visibility = Visibility.Visible;

                // update icon
                var newIcon = Controller.GetFontIcon(button);
                var newLabel = Controller.GetButtonName(button);
                buttonMapping.UpdateIcon(newIcon, newLabel);
            }
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }
}