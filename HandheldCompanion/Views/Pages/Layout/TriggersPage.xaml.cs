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
    /// Interaction logic for TriggersPage.xaml
    /// </summary>
    public partial class TriggersPage : Page
    {
        public static List<ButtonFlags> LeftTrigger = new() { ButtonFlags.L2, ButtonFlags.L3 };
        public static List<AxisLayoutFlags> LeftTriggerAxis = new() { AxisLayoutFlags.L2 };
        public static List<ButtonFlags> RightTrigger = new() { ButtonFlags.R2, ButtonFlags.R3 };
        public static List<AxisLayoutFlags> RightTriggerAxis = new() { AxisLayoutFlags.R2 };

        public Dictionary<ButtonFlags, ButtonMapping> MappingButtons = new();
        public Dictionary<AxisLayoutFlags, TriggerMapping> MappingAxis = new();

        public TriggersPage()
        {
            InitializeComponent();

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // draw UI
            foreach (ButtonFlags button in LeftTrigger)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                LeftTriggerButtonsPanel.Children.Add(buttonMapping);

                MappingButtons.Add(button, buttonMapping);
            }

            foreach (AxisLayoutFlags axis in LeftTriggerAxis)
            {
                TriggerMapping axisMapping = new TriggerMapping(axis);
                LeftTriggerPanel.Children.Add(axisMapping);

                MappingAxis.Add(axis, axisMapping);
            }

            foreach (ButtonFlags button in RightTrigger)
            {
                ButtonMapping buttonMapping = new ButtonMapping(button);
                RightTriggerButtonsPanel.Children.Add(buttonMapping);

                MappingButtons.Add(button, buttonMapping);
            }

            foreach (AxisLayoutFlags axis in RightTriggerAxis)
            {
                TriggerMapping axisMapping = new TriggerMapping(axis);
                RightTriggerPanel.Children.Add(axisMapping);

                MappingAxis.Add(axis, axisMapping);
            }
        }

        public TriggersPage(string Tag) : this()
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

                TriggerMapping axisMapping = mapping.Value;

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
                TriggerMapping mapping = pair.Value;

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
        }

        public void Page_Closed()
        {
        }
    }
}
