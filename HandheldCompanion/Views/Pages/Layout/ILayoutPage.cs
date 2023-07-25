using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using Inkore.UI.WPF.Modern.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

public class ILayoutPage : Page
{
    public Dictionary<AxisLayoutFlags, AxisMapping> MappingAxis = new();
    public Dictionary<ButtonFlags, ButtonMapping> MappingButtons = new();
    public Dictionary<AxisLayoutFlags, TriggerMapping> MappingTriggers = new();

    public virtual void UpdateController(IController controller)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // controller based
            foreach (var mapping in MappingButtons)
            {
                ButtonFlags button = mapping.Key;
                ButtonMapping buttonMapping = mapping.Value;
                // update mapping visibility
                if (!controller.HasSourceButton(button))
                    buttonMapping.Visibility = Visibility.Collapsed;
                else
                {
                    buttonMapping.Visibility = Visibility.Visible;
                    // update icon
                    FontIcon newIcon = controller.GetFontIcon(button);
                    string newLabel = controller.GetButtonName(button);
                    buttonMapping.UpdateIcon(newIcon, newLabel);
                }
            }
            foreach (var mapping in MappingAxis)
            {
                AxisLayoutFlags flags = mapping.Key;
                AxisLayout layout = AxisLayout.Layouts[flags];
                AxisMapping axisMapping = mapping.Value;
                // update mapping visibility
                if (!controller.HasSourceAxis(flags))
                    axisMapping.Visibility = Visibility.Collapsed;
                else
                {
                    axisMapping.Visibility = Visibility.Visible;
                    // update icon
                    FontIcon newIcon = controller.GetFontIcon(flags);
                    string newLabel = controller.GetAxisName(flags);
                    axisMapping.UpdateIcon(newIcon, newLabel);
                }
            }
            foreach (var mapping in MappingTriggers)
            {
                AxisLayoutFlags flags = mapping.Key;
                AxisLayout layout = AxisLayout.Layouts[flags];
                TriggerMapping axisMapping = mapping.Value;
                // update mapping visibility
                if (!controller.HasSourceAxis(flags))
                    axisMapping.Visibility = Visibility.Collapsed;
                else
                {
                    axisMapping.Visibility = Visibility.Visible;
                    // update icon
                    FontIcon newIcon = controller.GetFontIcon(flags);
                    string newLabel = controller.GetAxisName(flags);
                    axisMapping.UpdateIcon(newIcon, newLabel);
                }
            }
        });
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