using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerService.Sensors;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages.Profiles.Controller
{
    /// <summary>
    /// Interaction logic for ButtonsPage.xaml
    /// </summary>
    public partial class ButtonsPage : Page
    {
        private Profile currentProfile;
        private Hotkey ProfilesPageHotkey;

        // A,B,X,Y
        public static List<ButtonFlags> ABXY = new()
        {
            ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4, ButtonFlags.B5, ButtonFlags.B6, ButtonFlags.B7
        };

        public ButtonsPage()
        {
            InitializeComponent();

            // draw UI
            foreach (ButtonFlags button in ABXY)
            {
                ButtonMapping buttonMapping = new ButtonMapping();
                buttonMapping.Icon.Glyph = XInputController.GetGlyph(button);
                buttonMapping.Icon.Foreground = XInputController.GetFontIcon(button).Foreground;

                ButtonsStackPanel.Children.Add(buttonMapping);
            }
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
