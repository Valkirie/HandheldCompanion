using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for ButtonsPage.xaml
    /// </summary>
    public partial class ButtonsPage : ILayoutPage
    {
        public static List<ButtonFlags> ABXY = new() { ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4, ButtonFlags.B5, ButtonFlags.B6, ButtonFlags.B7, ButtonFlags.B8 };
        public static List<ButtonFlags> BUMPERS = new() { ButtonFlags.L1, ButtonFlags.R1 };
        public static List<ButtonFlags> MENU = new() { ButtonFlags.Back, ButtonFlags.Start, ButtonFlags.Special };
        public static List<ButtonFlags> BACKGRIPS = new() { ButtonFlags.L4, ButtonFlags.L5, ButtonFlags.R4, ButtonFlags.R5 };
        public static List<ButtonFlags> OEM = new() { ButtonFlags.OEM1, ButtonFlags.OEM2, ButtonFlags.OEM3, ButtonFlags.OEM4, ButtonFlags.OEM5, ButtonFlags.OEM6, ButtonFlags.OEM7, ButtonFlags.OEM8, ButtonFlags.OEM9, ButtonFlags.OEM10 };

        public ButtonsPage()
        {
            InitializeComponent();

            // draw UI
            foreach (ButtonFlags button in ABXY)
            {
                ButtonStack panel = new(button);
                ButtonsStackPanel.Children.Add(panel);

                ButtonStacks.Add(button, panel);
            }

            foreach (ButtonFlags button in BUMPERS)
            {
                ButtonStack panel = new(button);
                BumpersStackPanel.Children.Add(panel);

                ButtonStacks.Add(button, panel);
            }

            foreach (ButtonFlags button in MENU)
            {
                ButtonStack panel = new(button);
                MenuStackPanel.Children.Add(panel);

                ButtonStacks.Add(button, panel);
            }

            foreach (ButtonFlags button in BACKGRIPS)
            {
                ButtonStack panel = new(button);
                BackgripsStackPanel.Children.Add(panel);

                ButtonStacks.Add(button, panel);
            }

            foreach (ButtonFlags button in OEM)
            {
                if (!MainWindow.CurrentDevice.OEMButtons.Contains(button))
                    continue;

                ButtonStack panel = new(button);
                OEMStackPanel.Children.Add(panel);

                ButtonStacks.Add(button, panel);
            }

            // manage layout pages visibility
            gridOEM.Visibility = MainWindow.CurrentDevice.OEMButtons.Count() > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public override void UpdateController(IController controller)
        {
            // this will collapse all OEM buttons
            base.UpdateController(controller);

            // controller based
            foreach (var pair in ButtonStacks)
            {
                ButtonFlags button = pair.Key;
                ButtonStack buttonStack = pair.Value;

                // TODO: simplify or even completely remove OEMs from the mapper
                // specific buttons are handled elsewhere
                if (OEM.Contains(button))
                {
                    buttonStack.Visibility = Visibility.Visible;

                    // update icon
                    FontIcon newIcon = controller.GetFontIcon(button);
                    string newLabel = MainWindow.CurrentDevice.GetButtonName(button);
                    buttonStack.UpdateIcon(newIcon, newLabel);
                }
            }

            // manage layout pages visibility
            bool abxy = CheckController(controller, ABXY);
            bool bumpers = CheckController(controller, BUMPERS);
            bool menu = CheckController(controller, MENU);
            bool backgrips = CheckController(controller, BACKGRIPS);

            gridButtons.Visibility = abxy ? Visibility.Visible : Visibility.Collapsed;
            gridBumpers.Visibility = bumpers ? Visibility.Visible : Visibility.Collapsed;
            gridMenu.Visibility = menu ? Visibility.Visible : Visibility.Collapsed;
            gridBackgrips.Visibility = backgrips ? Visibility.Visible : Visibility.Collapsed;

            enabled = abxy || bumpers || menu || backgrips;
        }

        public ButtonsPage(string Tag) : this()
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
}