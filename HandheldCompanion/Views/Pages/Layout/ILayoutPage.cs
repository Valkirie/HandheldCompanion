using ControllerCommon.Actions;
using ControllerCommon.Controllers;
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

        public virtual void UpdateController(IController controller)
        {
        }

        public void Refresh(SortedDictionary<ButtonFlags, IActions> buttonMapping, SortedDictionary<AxisLayoutFlags, IActions> axisMapping)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var pair in MappingButtons)
                {
                    ButtonFlags button = pair.Key;
                    ButtonMapping mapping = pair.Value;

                    if (buttonMapping.TryGetValue(button, out IActions actions))
                    {
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

                    if (axisMapping.TryGetValue(axis, out IActions actions))
                    {
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

                    if (axisMapping.TryGetValue(axis, out IActions actions))
                    {
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
