using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System.Collections.Generic;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages.Profiles.Controller
{
    /// <summary>
    /// Interaction logic for DpadPage.xaml
    /// </summary>
    public partial class DpadPage : Page
    {
        public static List<ButtonFlags> DPAD = new() { ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight };

        public Dictionary<ButtonFlags, ButtonMapping> Mapping = new();

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
                FontIcon newIcon = Controller.GetFontIcon(button);
                string newLabel = Controller.GetButtonName(button);
                buttonMapping.UpdateIcon(newIcon, newLabel);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        public void Refresh(Dictionary<ButtonFlags, IActions> buttonMapping)
        {
            foreach (var pair in Mapping)
            {
                ButtonFlags button = pair.Key;
                ButtonMapping mapping = pair.Value;

                if (buttonMapping.ContainsKey(button))
                {
                    IActions actions = buttonMapping[button];
                    mapping.SetIActions(actions);
                    continue;
                }

                mapping.Reset();
            }
        }
    }
}
