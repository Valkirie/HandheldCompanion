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
    /// Interaction logic for DpadPage.xaml
    /// </summary>
    public partial class DpadPage : Page
    {
        // A,B,X,Y
        public static List<ButtonFlags> DPAD = new()
        {
            ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight
        };

        private Dictionary<ButtonFlags, ButtonMapping> Mapping = new();

        public DpadPage()
        {
            InitializeComponent();

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // draw UI
            foreach (ButtonFlags button in DPAD)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                DpadStackPanel.Children.Add(buttonMapping);

                Mapping.Add(button, buttonMapping);
            }
        }

        public DpadPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            // controller based
            foreach (var mapping in Mapping)
            {
                ButtonFlags button = mapping.Key;
                ButtonMapping buttonMapping = mapping.Value;

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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        public void Update()
        {
            Profile currentProfile = ProfilesPage.currentProfile;

            foreach (var pair in currentProfile.ButtonMapping)
            {
                ButtonFlags button = pair.Key;
                IActions actions = pair.Value;

                if (!Mapping.ContainsKey(button))
                    continue;

                ButtonMapping mapping = Mapping[button];

                // update actions
                mapping.Reset();
                mapping.SetIActions(actions);
            }
        }
    }
}