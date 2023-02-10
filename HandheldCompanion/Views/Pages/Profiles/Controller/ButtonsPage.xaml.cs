using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerService.Sensors;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
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
        // A,B,X,Y
        public static List<ButtonFlags> ABXY = new()
        {
            ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4, ButtonFlags.B5, ButtonFlags.B6, ButtonFlags.B7
        };

        // BUMPERS
        public static List<ButtonFlags> BUMPERS = new()
        {
            ButtonFlags.L1, ButtonFlags.R1,
            ButtonFlags.L2, ButtonFlags.R2,
            ButtonFlags.L3, ButtonFlags.R3,
            ButtonFlags.L4, ButtonFlags.R4,
            ButtonFlags.L5, ButtonFlags.R5,
            ButtonFlags.LPadClick, ButtonFlags.RPadClick,
            ButtonFlags.LPadTouch, ButtonFlags.RPadTouch
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

        public Dictionary<ButtonFlags, ButtonMapping> Mapping = new();

        public ButtonsPage()
        {
            InitializeComponent();

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // draw UI
            foreach (ButtonFlags button in ABXY)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                ButtonsStackPanel.Children.Add(buttonMapping);

                Mapping.Add(button, buttonMapping);
            }

            foreach (ButtonFlags button in BUMPERS)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                BumpersStackPanel.Children.Add(buttonMapping);

                Mapping.Add(button, buttonMapping);
            }

            foreach (ButtonFlags button in MENU)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                MenuStackPanel.Children.Add(buttonMapping);

                Mapping.Add(button, buttonMapping);
            }

            foreach (ButtonFlags button in OEM)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                OEMStackPanel.Children.Add(buttonMapping);

                Mapping.Add(button, buttonMapping);

                // only draw OEM buttons that are supported by the current device
                if (MainWindow.handheldDevice.OEMButtons.Contains(button))
                    buttonMapping.Visibility = Visibility.Visible;
            }
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            // controller based
            foreach (var mapping in Mapping)
            {
                ButtonFlags button = mapping.Key;
                ButtonMapping buttonMapping = mapping.Value;

                // specific buttons are handled elsewhere
                if (OEM.Contains(button))
                    continue;

                // update mapping visibility
                if (!Controller.IsButtonSupported(button))
                    buttonMapping.Visibility = Visibility.Collapsed;
                else
                    buttonMapping.Visibility = Visibility.Visible;

                // update icon
                var newIcon = Controller.GetFontIcon(button);

                // unsupported button
                if (newIcon is null)
                    continue;

                buttonMapping.UpdateIcon(newIcon);
                buttonMapping.Update();
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

        public void Refresh(Dictionary<ButtonFlags, IActions> buttonMapping)
        {
            foreach (ButtonMapping mapping in Mapping.Values)
                mapping.Reset();

            foreach(var pair in buttonMapping)
            {
                ButtonFlags button = pair.Key;
                IActions actions = pair.Value;

                if (!Mapping.ContainsKey(button))
                    continue;

                // update actions
                ButtonMapping mapping = Mapping[button];
                mapping.SetIActions(actions);
            }
        }
    }
}
