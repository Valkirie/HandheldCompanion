using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages
{
    public class ILayoutPage : Page
    {
        public Dictionary<ButtonFlags, ButtonMapping> MappingButtons = new();
        public Dictionary<AxisLayoutFlags, AxisMapping> MappingAxis = new();
        public Dictionary<AxisLayoutFlags, TriggerMapping> MappingTriggers = new();

        public void Refresh(Dictionary<ButtonFlags, IActions> buttonMapping, Dictionary<AxisLayoutFlags, IActions> axisMapping)
        {
            // UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
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

                foreach (var pair in MappingTriggers)
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
            });
        }
    }
}
