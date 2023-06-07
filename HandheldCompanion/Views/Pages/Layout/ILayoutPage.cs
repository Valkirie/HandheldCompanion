using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Actions;
using HandheldCompanion.Controls;

namespace HandheldCompanion.Views.Pages;

public class ILayoutPage : Page
{
    public Dictionary<AxisLayoutFlags, AxisMapping> MappingAxis = new();
    public Dictionary<ButtonFlags, ButtonMapping> MappingButtons = new();
    public Dictionary<AxisLayoutFlags, TriggerMapping> MappingTriggers = new();

    public virtual void UpdateController(IController controller)
    {
    }

    public void Refresh(SortedDictionary<ButtonFlags, IActions> buttonMapping,
        SortedDictionary<AxisLayoutFlags, IActions> axisMapping)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (var pair in MappingButtons)
            {
                var button = pair.Key;
                var mapping = pair.Value;

                if (buttonMapping.TryGetValue(button, out var actions))
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
                var axis = pair.Key;
                var mapping = pair.Value;

                if (axisMapping.TryGetValue(axis, out var actions))
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
                var axis = pair.Key;
                var mapping = pair.Value;

                if (axisMapping.TryGetValue(axis, out var actions))
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