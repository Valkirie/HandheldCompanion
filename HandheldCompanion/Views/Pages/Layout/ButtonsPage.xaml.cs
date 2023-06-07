using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for ButtonsPage.xaml
/// </summary>
public partial class ButtonsPage : ILayoutPage
{
    public static List<ButtonFlags> ABXY = new()
    {
        ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4, ButtonFlags.B5, ButtonFlags.B6, ButtonFlags.B7,
        ButtonFlags.B8
    };

    public static List<ButtonFlags> BUMPERS = new() { ButtonFlags.L1, ButtonFlags.R1 };
    public static List<ButtonFlags> MENU = new() { ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special };

    public static List<ButtonFlags> BACKGRIPS = new()
        { ButtonFlags.L4, ButtonFlags.R4, ButtonFlags.L5, ButtonFlags.R5 };

    public static List<ButtonFlags> OEM = new()
    {
        ButtonFlags.OEM1, ButtonFlags.OEM2, ButtonFlags.OEM3, ButtonFlags.OEM4, ButtonFlags.OEM5, ButtonFlags.OEM6,
        ButtonFlags.OEM7, ButtonFlags.OEM8, ButtonFlags.OEM9, ButtonFlags.OEM10
    };

    public ButtonsPage()
    {
        InitializeComponent();

        // draw UI
        foreach (var button in ABXY)
        {
            var buttonMapping = new ButtonMapping(button);
            ButtonsStackPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var button in BUMPERS)
        {
            var buttonMapping = new ButtonMapping(button);
            BumpersStackPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var button in MENU)
        {
            var buttonMapping = new ButtonMapping(button);
            MenuStackPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var button in BACKGRIPS)
        {
            var buttonMapping = new ButtonMapping(button);
            BACKGRIPSStackPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        foreach (var button in OEM)
        {
            if (!MainWindow.CurrentDevice.OEMButtons.Contains(button))
                continue;

            var buttonMapping = new ButtonMapping(button);
            buttonMapping.Visibility = Visibility.Visible;
            OEMStackPanel.Children.Add(buttonMapping);

            MappingButtons.Add(button, buttonMapping);
        }

        // manage layout pages visibility
        gridOEM.Visibility = MainWindow.CurrentDevice.OEMButtons.Any() ? Visibility.Visible : Visibility.Collapsed;
    }

    public ButtonsPage(string Tag) : this()
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

                // update icon
                var newIcon = Controller.GetFontIcon(button);
                var newLabel = Controller.GetButtonName(button);
                buttonMapping.UpdateIcon(newIcon, newLabel);

                // specific buttons are handled elsewhere
                if (OEM.Contains(button))
                {
                    buttonMapping.Name.Text = MainWindow.CurrentDevice.GetButtonName(button);
                    continue;
                }

                // update mapping visibility
                if (!Controller.HasSourceButton(button))
                    buttonMapping.Visibility = Visibility.Collapsed;
                else
                    buttonMapping.Visibility = Visibility.Visible;
            }

            // manage layout pages visibility
            var HasBackGrips = Controller.HasSourceButton(ButtonFlags.L4) ||
                               Controller.HasSourceButton(ButtonFlags.L5) ||
                               Controller.HasSourceButton(ButtonFlags.R4) || Controller.HasSourceButton(ButtonFlags.R5);
            gridBACKGRIPS.Visibility = HasBackGrips ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }
}