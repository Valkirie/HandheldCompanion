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
        foreach (ButtonFlags button in DPAD)
        {
            ButtonStack panel = new(button);
            DpadStackPanel.Children.Add(panel);

            ButtonStacks.Add(button, panel);
        }
    }

    public DpadPage(string Tag) : this()
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