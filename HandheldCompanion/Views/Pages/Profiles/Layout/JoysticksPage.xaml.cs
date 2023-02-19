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
    /// Interaction logic for JoysticksPage.xaml
    /// </summary>
    public partial class JoysticksPage : Page
    {
        // LEFT JOYSTICK
        public static List<ButtonFlags> LeftThumbButtons = new()
        {
            ButtonFlags.LeftThumb, ButtonFlags.LeftThumbTouch,
        };

        public static List<AxisLayoutFlags> LeftThumbAxis = new()
        {
            AxisLayoutFlags.LeftThumb, AxisLayoutFlags.RightThumb,
            AxisLayoutFlags.LeftPad, AxisLayoutFlags.RightPad,
        };

        public Dictionary<ButtonFlags, ButtonMapping> MappingButtons = new();
        public Dictionary<AxisLayoutFlags, AxisMapping> MappingAxis = new();

        public JoysticksPage()
        {
            InitializeComponent();

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // draw UI
            foreach (ButtonFlags button in LeftThumbButtons)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                LeftJoystickStackPanel.Children.Add(buttonMapping);

                MappingButtons.Add(button, buttonMapping);
            }

            foreach (AxisLayoutFlags axis in LeftThumbAxis)
            {
                AxisMapping axisMapping = new AxisMapping(axis);
                LeftJoystickStackPanel.Children.Add(axisMapping);

                MappingAxis.Add(axis, axisMapping);
            }
        }

        public JoysticksPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            // controller based
            foreach (var mapping in MappingButtons)
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

                // unsupported button
                if (newIcon is null)
                    continue;

                buttonMapping.UpdateIcon(newIcon);
                buttonMapping.Update();
            }

            foreach (var mapping in MappingAxis)
            {
                AxisLayoutFlags flags = mapping.Key;
                AxisLayout layout = AxisLayout.Layouts[flags];

                AxisMapping axisMapping = mapping.Value;

                // update mapping visibility
                if (!Controller.IsAxisSupported(flags))
                    axisMapping.Visibility = Visibility.Collapsed;
                else
                    axisMapping.Visibility = Visibility.Visible;

                // update icon
                FontIcon newIcon = Controller.GetFontIcon(flags);

                // unsupported button
                if (newIcon is null)
                    continue;

                axisMapping.UpdateIcon(newIcon);
                axisMapping.Update();
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // do something
        }

        public void Page_Closed()
        {
            // do something
        }

        public void Refresh(Dictionary<ButtonFlags, IActions> buttonMapping, Dictionary<AxisLayoutFlags, IActions> axisMapping)
        {
            foreach (ButtonMapping mapping in MappingButtons.Values)
                mapping.Reset();

            foreach (AxisMapping mapping in MappingAxis.Values)
                mapping.Reset();

            foreach (var pair in buttonMapping)
            {
                ButtonFlags button = pair.Key;
                IActions actions = pair.Value;

                if (!MappingButtons.ContainsKey(button))
                    continue;

                // update actions
                ButtonMapping mapping = MappingButtons[button];
                mapping.SetIActions(actions);
            }

            foreach (var pair in axisMapping)
            {
                AxisLayoutFlags axis = pair.Key;
                IActions actions = pair.Value;

                if (!MappingAxis.ContainsKey(axis))
                    continue;

                // update actions
                AxisMapping mapping = MappingAxis[axis];
                mapping.SetIActions(actions);
            }
        }
    }
}
