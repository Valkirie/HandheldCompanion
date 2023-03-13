using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Actions;
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
        public static List<ButtonFlags> LeftThumbButtons = new() { ButtonFlags.LeftThumb, ButtonFlags.LeftThumbTouch, ButtonFlags.LeftThumbUp, ButtonFlags.LeftThumbDown, ButtonFlags.LeftThumbLeft, ButtonFlags.LeftThumbRight };
        public static List<AxisLayoutFlags> LeftThumbAxis = new() { AxisLayoutFlags.LeftThumb };
        public static List<ButtonFlags> RightThumbButtons = new() { ButtonFlags.RightThumb, ButtonFlags.RightThumbTouch, ButtonFlags.RightThumbUp, ButtonFlags.RightThumbDown, ButtonFlags.RightThumbLeft, ButtonFlags.RightThumbRight };
        public static List<AxisLayoutFlags> RightThumbAxis = new() { AxisLayoutFlags.RightThumb };

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
                LeftJoystickButtonsPanel.Children.Add(buttonMapping);

                MappingButtons.Add(button, buttonMapping);
            }

            foreach (AxisLayoutFlags axis in LeftThumbAxis)
            {
                AxisMapping axisMapping = new AxisMapping(axis);
                LeftJoystickPanel.Children.Add(axisMapping);

                MappingAxis.Add(axis, axisMapping);
            }

            foreach (ButtonFlags button in RightThumbButtons)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                RightJoystickButtonsPanel.Children.Add(buttonMapping);

                MappingButtons.Add(button, buttonMapping);
            }

            foreach (AxisLayoutFlags axis in RightThumbAxis)
            {
                AxisMapping axisMapping = new AxisMapping(axis);
                RightJoystickPanel.Children.Add(axisMapping);

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
                {
                    buttonMapping.Visibility = Visibility.Visible;

                    // update icon
                    FontIcon newIcon = Controller.GetFontIcon(button);
                    string newLabel = Controller.GetButtonName(button);

                    buttonMapping.UpdateIcon(newIcon, newLabel);
                }
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
                {
                    axisMapping.Visibility = Visibility.Visible;

                    // update icon
                    FontIcon newIcon = Controller.GetFontIcon(flags);
                    string newLabel = Controller.GetAxisName(flags);
                    axisMapping.UpdateIcon(newIcon, newLabel);
                }
            }
        }

        public void Refresh(Dictionary<ButtonFlags, IActions> buttonMapping, Dictionary<AxisLayoutFlags, IActions> axisMapping)
        {
            foreach (var pair in MappingButtons)
            {
                ButtonFlags button = pair.Key;
                ButtonMapping mapping = pair.Value;

                if (buttonMapping.ContainsKey(button))
                {
                    IActions actions = buttonMapping[button];

                    if (actions is null)
                        actions = new EmptyActions();

                    mapping.SetIActions(actions);
                    continue;
                }

                mapping.Reset();
            }

            foreach (var pair in MappingAxis)
            {
                AxisLayoutFlags axis = pair.Key;
                AxisMapping mapping = pair.Value;

                if (axisMapping.ContainsKey(axis))
                {
                    IActions actions = axisMapping[axis];

                    if (actions is null)
                        actions = new EmptyActions();

                    mapping.SetIActions(actions);
                    continue;
                }

                mapping.Reset();
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
    }
}
