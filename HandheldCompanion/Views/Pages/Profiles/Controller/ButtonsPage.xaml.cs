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

        // BUMPERS
        public static List<ButtonFlags> BUMPERS = new()
        {
            ButtonFlags.L1, ButtonFlags.R1
        };

        // MENU
        public static List<ButtonFlags> MENU = new()
        {
            ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special
        };

        // OEM
        public static List<ButtonFlags> OEM = new()
        {
            ButtonFlags.OEM1, ButtonFlags.OEM2, ButtonFlags.OEM3, ButtonFlags.OEM4, ButtonFlags.OEM5,
            ButtonFlags.OEM6, ButtonFlags.OEM7, ButtonFlags.OEM8, ButtonFlags.OEM9, ButtonFlags.OEM10
        };

        public ButtonsPage()
        {
            InitializeComponent();

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            ProfileManager.Applied += ProfileManager_Applied;

            // draw UI
            foreach (ButtonFlags button in ABXY)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                ButtonsStackPanel.Children.Add(buttonMapping);
            }

            foreach (ButtonFlags button in BUMPERS)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                BumpersStackPanel.Children.Add(buttonMapping);
            }

            foreach (ButtonFlags button in MENU)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                MenuStackPanel.Children.Add(buttonMapping);
            }

            foreach (ButtonFlags button in OEM)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                OEMStackPanel.Children.Add(buttonMapping);
            }
        }

        private void ProfileManager_Applied(Profile profile)
        {
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            // controller based
            foreach (ButtonMapping mapping in ButtonsStackPanel.Children)
                mapping.SetController(Controller);
            foreach (ButtonMapping mapping in BumpersStackPanel.Children)
                mapping.SetController(Controller);
            foreach (ButtonMapping mapping in MenuStackPanel.Children)
                mapping.SetController(Controller);

            foreach (ButtonMapping mapping in OEMStackPanel.Children)
                mapping.SetController(Controller);
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
