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
    /// Interaction logic for TrackpadsPage.xaml
    /// </summary>
    public partial class TrackpadsPage : Page
    {
        public static List<ButtonFlags> LeftButtons = new() { ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClick, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown, ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight };
        public static List<AxisLayoutFlags> LeftAxis = new() { AxisLayoutFlags.LeftPad };
        public static List<ButtonFlags> RightButtons = new() { ButtonFlags.RightPadTouch, ButtonFlags.RightPadClick, ButtonFlags.RightPadClickUp, ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight };
        public static List<AxisLayoutFlags> RightAxis = new() { AxisLayoutFlags.RightPad };

        public Dictionary<ButtonFlags, ButtonMapping> MappingButtons = new();
        public Dictionary<AxisLayoutFlags, AxisMapping> MappingAxis = new();

        public TrackpadsPage()
        {
            InitializeComponent();

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // draw UI
            foreach (ButtonFlags button in LeftButtons)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                LeftTrackpadButtonsPanel.Children.Add(buttonMapping);

                MappingButtons.Add(button, buttonMapping);
            }

            foreach (AxisLayoutFlags axis in LeftAxis)
            {
                AxisMapping axisMapping = new AxisMapping(axis);
                LeftTrackpadPanel.Children.Add(axisMapping);

                MappingAxis.Add(axis, axisMapping);
            }

            foreach (ButtonFlags button in RightButtons)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                RightTrackpadButtonsPanel.Children.Add(buttonMapping);

                MappingButtons.Add(button, buttonMapping);
            }

            foreach (AxisLayoutFlags axis in RightAxis)
            {
                AxisMapping axisMapping = new AxisMapping(axis);
                RightTrackpadPanel.Children.Add(axisMapping);

                MappingAxis.Add(axis, axisMapping);
            }
        }

        public TrackpadsPage(string Tag) : this()
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
