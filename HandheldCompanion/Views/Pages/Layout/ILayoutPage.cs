using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using Inkore.UI.WPF.Modern.Controls;
using System.Collections.Generic;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

public class ILayoutPage : Page
{
    protected bool enabled = false;

    public Dictionary<ButtonFlags, ButtonStack> ButtonStacks = new();
    public Dictionary<AxisLayoutFlags, AxisMapping> AxisMappings = new();
    public Dictionary<AxisLayoutFlags, TriggerMapping> TriggerMappings = new();

    protected bool CheckController(IController controller, List<ButtonFlags> buttons)
    {
        foreach (ButtonFlags button in buttons)
            if (controller.HasSourceButton(button))
                return true;
        return false;
    }

    protected bool CheckController(IController controller, List<AxisLayoutFlags> axes)
    {
        foreach (AxisLayoutFlags axis in axes)
            if (controller.HasSourceAxis(axis))
                return true;
        return false;
    }

    public bool IsEnabled()
    {
        return enabled;
    }

    public virtual void UpdateController(IController controller)
    {
        // controller based
        foreach (var pair in ButtonStacks)
        {
            ButtonFlags button = pair.Key;
            ButtonStack buttonStack = pair.Value;

            // update mapping visibility
            if (!controller.HasSourceButton(button))
                buttonStack.Visibility = Visibility.Collapsed;
            else
            {
                buttonStack.Visibility = Visibility.Visible;

                // update icon
                FontIcon newIcon = controller.GetFontIcon(button);
                string newLabel = controller.GetButtonName(button);
                buttonStack.UpdateIcon(newIcon, newLabel);
            }
        }

        foreach (var pair in AxisMappings)
        {
            AxisLayoutFlags flags = pair.Key;
            AxisLayout layout = AxisLayout.Layouts[flags];

            AxisMapping axisMapping = pair.Value;

            // update mapping visibility
            bool isVisible = controller.HasSourceAxis(flags);

            switch (flags)
            {
                case AxisLayoutFlags.Gyroscope:
                    isVisible |= MainWindow.CurrentDevice.HasMotionSensor();
                    break;
            }

            if (!isVisible)
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

        foreach (var pair in TriggerMappings)
        {
            AxisLayoutFlags flags = pair.Key;
            AxisLayout layout = AxisLayout.Layouts[flags];

            TriggerMapping axisMapping = pair.Value;

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
    }

    public virtual void UpdateSelections()
    {
        foreach (var pair in ButtonStacks)
            pair.Value.UpdateSelections();

        foreach (var pair in AxisMappings)
            pair.Value.UpdateSelections();

        foreach (var pair in TriggerMappings)
            pair.Value.UpdateSelections();
    }

    public void Update(Layout layout)
    {
        foreach (var pair in ButtonStacks)
        {
            ButtonFlags button = pair.Key;
            ButtonStack mappings = pair.Value;

            if (layout.ButtonLayout.TryGetValue(button, out List<IActions> actions))
            {
                mappings.SetActions(actions);
                continue;
            }

            mappings.Reset();
        }

        foreach (var pair in AxisMappings)
        {
            AxisLayoutFlags axis = pair.Key;
            AxisMapping mapping = pair.Value;

            if (layout.AxisLayout.TryGetValue(axis, out IActions actions))
            {
                mapping.SetIActions(actions);
                continue;
            }

            mapping.Reset();
        }

        foreach (var pair in TriggerMappings)
        {
            AxisLayoutFlags axis = pair.Key;
            TriggerMapping mapping = pair.Value;

            if (layout.AxisLayout.TryGetValue(axis, out IActions actions))
            {
                mapping.SetIActions(actions);
                continue;
            }

            mapping.Reset();
        }
    }
}